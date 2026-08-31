using System.Globalization;
using ClosedXML.Excel;

namespace ExportDocGen.Services;

/// <summary>
/// Reads a customer order spreadsheet (<c>.xlsx</c>) into a list of line rows.
/// Pure — no database access. Matching parsed codes to the catalog and creating
/// the order happen elsewhere (the import page + <see cref="OrderService"/>).
/// </summary>
/// <remarks>
/// Tuned to the layout the company actually receives: a header row with
/// <c>CODE</c>, <c>QTY</c>, <c>UNIT PRICE</c> (and an optional <c>TOTAL</c>), then
/// one line per row until the first blank code (which also skips a trailing
/// totals row). The header row is located by name and columns are mapped by
/// header text, so a slightly different file still imports.
/// </remarks>
public sealed class ExcelOrderImportParser
{
    private static readonly string[] CodeHeaders =
        ["CODE", "PART", "PARTNO", "PARTNUMBER", "ARTICLE", "REFERENCE", "REF"];
    private static readonly string[] QtyHeaders =
        ["QTY", "QUANTITY", "QUANTITIES", "PCS", "PIECES", "ADET"];
    private static readonly string[] PriceHeaders =
        ["UNITPRICE", "PRICE", "UPRICE", "FIYAT", "BIRIMFIYAT"];
    private static readonly string[] TotalHeaders =
        ["TOTAL", "TOTALPRICE", "AMOUNT", "LINETOTAL", "TUTAR"];

    public ImportedSheet Parse(Stream xlsx)
    {
        using var workbook = new XLWorkbook(xlsx);
        var sheet = workbook.Worksheets.FirstOrDefault()
            ?? throw new InvalidImportException("The workbook has no worksheets.");

        var (headerRow, map) = FindHeader(sheet)
            ?? throw new InvalidImportException(
                "Could not find a header row containing 'CODE' and 'QTY'. "
                + "The sheet needs columns for code, quantity and unit price.");
        var warnings = new List<string>();
        var rows = new List<ImportedRow>();

        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? headerRow;
        for (var r = headerRow + 1; r <= lastRow; r++)
        {
            var row = sheet.Row(r);
            var rawCode = CellText(row.Cell(map.CodeColumn)).Trim();

            if (rawCode.Length == 0)
            {
                var looksLikeTotals = map.TotalColumn is int tc
                    && CellText(row.Cell(tc)).Trim().Length > 0;
                warnings.Add(looksLikeTotals
                    ? $"Stopped at row {r} — looks like a totals row."
                    : $"Stopped at row {r} — blank code.");
                break;
            }

            var qty = TryDecimal(row.Cell(map.QtyColumn));
            var price = map.PriceColumn is int pc ? TryDecimal(row.Cell(pc)) : null;
            var stated = map.TotalColumn is int t ? TryDecimal(row.Cell(t)) : null;

            string? error = null;
            if (qty is null or <= 0)
                error = "Quantity is missing or not a positive number.";
            else if (price is null or < 0)
                error = "Unit price is missing or not a valid number.";

            rows.Add(new ImportedRow(r, rawCode, qty, price, stated, error));
        }

        if (rows.Count == 0)
            warnings.Add("No data rows were found under the header.");

        return new ImportedSheet(sheet.Name, rows, warnings);
    }

    /// <summary>Trim, upper-case and strip all whitespace — used to match a
    /// spreadsheet code against a catalog <c>PartNumber</c> (e.g. "F6167 G"
    /// and "F6167G" are the same part).</summary>
    public static string NormalizeCode(string? raw) => Collapse(raw ?? "");

    private static (int HeaderRow, ColumnMap Map)? FindHeader(IXLWorksheet sheet)
    {
        foreach (var row in sheet.RowsUsed())
        {
            int? code = null, qty = null, price = null, total = null;
            foreach (var cell in row.CellsUsed())
            {
                var norm = Collapse(CellText(cell));
                if (norm.Length == 0) continue;
                var col = cell.Address.ColumnNumber;

                if (code is null && StartsWithAny(norm, CodeHeaders)) code = col;
                else if (qty is null && StartsWithAny(norm, QtyHeaders)) qty = col;
                else if (total is null && StartsWithAny(norm, TotalHeaders)) total = col;
                else if (price is null && StartsWithAny(norm, PriceHeaders)) price = col;
            }

            if (code is not null && qty is not null)
                return (row.RowNumber(), new ColumnMap(code.Value, qty.Value, price, total));
        }

        return null;
    }

    private static string CellText(IXLCell cell) => cell.IsEmpty() ? "" : cell.GetString();

    private static decimal? TryDecimal(IXLCell cell)
    {
        if (cell.IsEmpty()) return null;

        var value = cell.Value;
        if (value.IsNumber)
            return (decimal)value.GetNumber();

        if (!value.IsText)
            return null;

        var text = value.GetText().Trim();
        if (text.Length == 0)
            return null;

        // A number typed as text: try "." decimals first, then the local format.
        return decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)
            || decimal.TryParse(text, NumberStyles.Any, CultureInfo.CurrentCulture, out d)
            ? d
            : null;
    }

    private static string Collapse(string s)
    {
        Span<char> buffer = s.Length <= 128 ? stackalloc char[s.Length] : new char[s.Length];
        var n = 0;
        foreach (var c in s)
            if (!char.IsWhiteSpace(c))
                buffer[n++] = char.ToUpperInvariant(c);
        return new string(buffer[..n]);
    }

    private static bool StartsWithAny(string normalized, string[] candidates) =>
        candidates.Any(c => normalized.StartsWith(c, StringComparison.Ordinal));

    private readonly record struct ColumnMap(
        int CodeColumn, int QtyColumn, int? PriceColumn, int? TotalColumn);
}

/// <summary>Result of parsing one order spreadsheet.</summary>
public sealed record ImportedSheet(
    string SheetName,
    IReadOnlyList<ImportedRow> Rows,
    IReadOnlyList<string> Warnings);

/// <summary>One parsed line row. <paramref name="Error"/> is non-null when the
/// row could not be read cleanly (bad quantity or price); such rows are shown
/// on the review screen but excluded from the order until fixed.</summary>
public sealed record ImportedRow(
    int ExcelRow,
    string RawCode,
    decimal? Quantity,
    decimal? UnitPrice,
    decimal? StatedTotal,
    string? Error);

/// <summary>The uploaded file is not a readable order spreadsheet.</summary>
public sealed class InvalidImportException(string message) : Exception(message);
