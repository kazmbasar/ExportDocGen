using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using ExportDocGen.Data;
using ExportDocGen.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExportDocGen.Services;

/// <summary>
/// Loads the company stock database (an <c>.xlsx</c> export of the ODS file) into
/// the product catalogue. The sheet has the columns
/// <c>Description · MENŞEİ · MARKA · CİNSİ · GTIP · Net weight · MU · m3</c>;
/// only rows whose type ("CİNSİ") contains "FILTER" are imported. Gross weight is
/// not in the file — it is set to net × 1.05.
/// </summary>
public sealed class StockCatalogImportService(IDbContextFactory<AppDbContext> dbFactory)
{
    private const decimal GrossUplift = 1.05m;
    private const int BatchSize = 1000;

    public StockImportResult Parse(Stream xlsx)
    {
        using var workbook = new XLWorkbook(xlsx);
        var sheet = workbook.Worksheets.FirstOrDefault()
            ?? throw new InvalidImportException("The workbook has no worksheets.");

        var (headerRow, map) = FindHeader(sheet)
            ?? throw new InvalidImportException(
                "Could not find the stock header row (needs 'Description', 'CİNSİ', "
                + "'Net weight' and 'm3' columns).");

        var rows = new List<StockRow>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int total = 0, blank = 0, dup = 0, nonFilter = 0, zeroWeight = 0, zeroVolume = 0;

        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? headerRow;
        for (var r = headerRow + 1; r <= lastRow; r++)
        {
            var row = sheet.Row(r);
            var code = Text(row.Cell(map.Code)).Trim();
            if (code.Length == 0) { blank++; continue; }

            total++;
            var cinsi = Text(row.Cell(map.Type)).Trim();
            if (!cinsi.Contains("FILTER", StringComparison.OrdinalIgnoreCase)) { nonFilter++; continue; }
            if (!seen.Add(code)) { dup++; continue; }

            var net = TryNumber(row.Cell(map.NetWeight)) ?? 0m;
            if (net <= 0m) zeroWeight++;
            var vol = map.Volume is int vc ? TryNumber(row.Cell(vc)) ?? 0m : 0m;
            if (vol <= 0m) zeroVolume++;

            rows.Add(new StockRow(
                Code: code,
                Description: cinsi.Length > 0 ? cinsi : code,
                Origin: Clean(map.Origin is int oc ? Text(row.Cell(oc)) : null),
                Brand: Clean(map.Brand is int bc ? Text(row.Cell(bc)) : null),
                HsCode: Clean(map.HsCode is int hc ? Text(row.Cell(hc)) : null),
                FilterType: DeriveType(cinsi),
                NetWeightKg: net,
                UnitVolumeM3: vol));
        }

        return new StockImportResult(rows, total, blank, dup, nonFilter, zeroWeight, zeroVolume);
    }

    /// <summary>Wipes the catalogue and inserts the parsed rows. Refuses if any
    /// existing product is referenced by an order line.</summary>
    public async Task<int> ReplaceCatalogueAsync(IReadOnlyList<StockRow> rows)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var inUseIds = await db.OrderLines.Select(l => l.ProductId).Distinct().ToListAsync();
        if (inUseIds.Count > 0)
        {
            var codes = await db.Products
                .Where(p => inUseIds.Contains(p.Id))
                .Select(p => p.PartNumber)
                .ToListAsync();
            throw new InvalidImportException(
                "Cannot replace the catalogue — these products are on an order: "
                + string.Join(", ", codes) + ". Delete those orders first.");
        }

        await using var tx = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlRawAsync("DELETE FROM Products");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM sqlite_sequence WHERE name = 'Products'");

        for (var i = 0; i < rows.Count; i += BatchSize)
        {
            db.Products.AddRange(rows.Skip(i).Take(BatchSize).Select(ToProduct));
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
        }

        await tx.CommitAsync();
        return rows.Count;
    }

    private static Product ToProduct(StockRow r) => new()
    {
        PartNumber = r.Code,
        Description = r.Description,
        Origin = r.Origin,
        Brand = r.Brand,
        HsCode = r.HsCode,
        FilterType = r.FilterType,
        NetWeightKg = r.NetWeightKg,
        GrossWeightKg = Math.Round(r.NetWeightKg * GrossUplift, 3, MidpointRounding.AwayFromZero),
        UnitVolumeM3 = r.UnitVolumeM3,
        IsActive = true,
    };

    private static string? DeriveType(string cinsi)
    {
        var u = cinsi.ToUpperInvariant();
        if (u.Contains("AIR")) return "air";
        if (u.Contains("OIL")) return "oil";
        if (u.Contains("FUEL")) return "fuel";
        if (u.Contains("CABIN")) return "cabin";
        if (u.Contains("WATER")) return "water";
        return null;
    }

    /// <summary>Blank, "-" and spreadsheet errors ("#N/A", "#VALUE!") → null.</summary>
    private static string? Clean(string? value)
    {
        var v = value?.Trim();
        if (string.IsNullOrEmpty(v) || v == "-" || v.StartsWith('#')) return null;
        return v;
    }

    private static string Text(IXLCell cell) => cell.IsEmpty() ? "" : cell.GetString();

    private static decimal? TryNumber(IXLCell cell)
    {
        if (cell.IsEmpty()) return null;
        var value = cell.Value;
        if (value.IsNumber) return (decimal)value.GetNumber();
        if (!value.IsText) return null;

        var text = value.GetText().Trim();
        if (text.Length == 0) return null;

        // Turkish format ("1.305,98") first, then invariant.
        return decimal.TryParse(text, NumberStyles.Any, CultureInfo.GetCultureInfo("tr-TR"), out var d)
            || decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out d)
            ? d
            : null;
    }

    private static (int HeaderRow, ColumnMap Map)? FindHeader(IXLWorksheet sheet)
    {
        foreach (var row in sheet.RowsUsed())
        {
            int? code = null, origin = null, brand = null, type = null, hs = null, net = null, vol = null;
            foreach (var cell in row.CellsUsed())
            {
                var key = Fold(Text(cell));
                var col = cell.Address.ColumnNumber;
                switch (key)
                {
                    case "DESCRIPTION" when code is null: code = col; break;
                    case "MENSEI" when origin is null: origin = col; break;
                    case "MARKA" when brand is null: brand = col; break;
                    case "CINSI" when type is null: type = col; break;
                    case "GTIP" when hs is null: hs = col; break;
                    case "NETWEIGHT" when net is null: net = col; break;
                    case "M3" when vol is null: vol = col; break;
                }
            }

            if (code is not null && type is not null && net is not null)
                return (row.RowNumber(), new ColumnMap(code.Value, origin, brand, type.Value, hs, net.Value, vol));
        }
        return null;
    }

    /// <summary>Upper-case, strip whitespace, fold Turkish letters to ASCII —
    /// so "MENŞEİ" matches "MENSEI" and "CİNSİ" matches "CINSI".</summary>
    private static string Fold(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
        {
            if (char.IsWhiteSpace(c)) continue;
            var u = char.ToUpperInvariant(c);
            sb.Append(u switch
            {
                'İ' or 'I' or 'Ì' or 'Í' => 'I',
                'Ş' => 'S',
                'Ç' => 'C',
                'Ö' => 'O',
                'Ü' => 'U',
                'Ğ' => 'G',
                _ => u,
            });
        }
        return sb.ToString();
    }

    private readonly record struct ColumnMap(
        int Code, int? Origin, int? Brand, int Type, int? HsCode, int NetWeight, int? Volume);
}

/// <summary>One parsed stock row, ready to become a <see cref="Product"/>.</summary>
public sealed record StockRow(
    string Code,
    string Description,
    string? Origin,
    string? Brand,
    string? HsCode,
    string? FilterType,
    decimal NetWeightKg,
    decimal UnitVolumeM3);

/// <summary>Outcome of parsing the stock file.</summary>
public sealed record StockImportResult(
    IReadOnlyList<StockRow> Rows,
    int DataRows,
    int SkippedBlankCode,
    int SkippedDuplicate,
    int SkippedNonFilter,
    int ZeroWeight,
    int ZeroVolume)
{
    public int Kept => Rows.Count;

    public string Summary() => string.Join('\n',
        $"Data rows            {DataRows,7}",
        $"Kept (filters)       {Kept,7}",
        $"Skipped, blank code  {SkippedBlankCode,7}",
        $"Skipped, duplicate   {SkippedDuplicate,7}",
        $"Skipped, non-filter  {SkippedNonFilter,7}",
        $"Zero net weight      {ZeroWeight,7}  (imported anyway)",
        $"Zero volume          {ZeroVolume,7}  (imported anyway)");
}
