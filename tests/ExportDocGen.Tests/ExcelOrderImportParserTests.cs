using ClosedXML.Excel;
using ExportDocGen.Data.Entities;
using ExportDocGen.Services;

namespace ExportDocGen.Tests;

public class ExcelOrderImportParserTests
{
    private static readonly ExcelOrderImportParser Parser = new();

    private static Stream Sample(string name)
        => File.OpenRead(Path.Combine(AppContext.BaseDirectory, "TestFiles", name));

    [Fact]
    public void Parses_every_line_of_the_AUG_sample()
    {
        using var file = Sample("FILTORQ - AUG 2026.xlsx");

        var sheet = Parser.Parse(file);

        Assert.Equal(48, sheet.Rows.Count);
        Assert.All(sheet.Rows, r => Assert.Null(r.Error));

        var first = sheet.Rows[0];
        Assert.Equal("AD206", first.RawCode);
        Assert.Equal(50m, first.Quantity);
        Assert.Equal(10.11m, Math.Round(first.UnitPrice!.Value, 2));

        Assert.Equal("F6380", sheet.Rows[^1].RawCode);
    }

    [Fact]
    public void Stops_at_the_trailing_totals_row_of_the_TENDER_sample()
    {
        using var file = Sample("FILTORQ TENDER.xlsx");

        var sheet = Parser.Parse(file);

        Assert.Equal(40, sheet.Rows.Count);
        Assert.Equal("CK8695", sheet.Rows[^1].RawCode);
        Assert.DoesNotContain(sheet.Rows, r => r.ExcelRow == 42);
        Assert.Contains(sheet.Warnings, w => w.Contains("row 42") && w.Contains("totals row"));
    }

    [Theory]
    [InlineData("F6167 G", "F6167G")]
    [InlineData("a2669 h", "A2669H")]
    [InlineData("  u405 kit  ", "U405KIT")]
    public void NormalizeCode_ignores_spacing_and_case(string raw, string expected)
        => Assert.Equal(expected, ExcelOrderImportParser.NormalizeCode(raw));

    [Fact]
    public void Spreadsheet_codes_match_catalog_part_numbers_ignoring_spacing()
    {
        var catalog = new[]
        {
            new Product { Id = 7, PartNumber = "F6167G", Description = "Fuel filter" },
            new Product { Id = 9, PartNumber = "A1029", Description = "Air filter" },
        }.ToDictionary(p => ExcelOrderImportParser.NormalizeCode(p.PartNumber), p => p.Id);

        Assert.Equal(7, catalog[ExcelOrderImportParser.NormalizeCode("F6167 G")]);
        Assert.False(catalog.ContainsKey(ExcelOrderImportParser.NormalizeCode("NOPE-1")));
    }

    [Fact]
    public void A_row_with_a_non_numeric_quantity_is_flagged_and_parsing_continues()
    {
        using var file = MakeSheet(
            ("CODE", "QTY", "UNIT PRICE"),
            ("X-1", "abc", "5"),
            ("X-2", "10", "2.5"));

        var sheet = Parser.Parse(file);

        Assert.Equal(2, sheet.Rows.Count);
        Assert.NotNull(sheet.Rows[0].Error);
        Assert.Null(sheet.Rows[1].Error);
        Assert.Equal(10m, sheet.Rows[1].Quantity);
    }

    [Fact]
    public void A_sheet_without_a_recognisable_header_is_rejected()
    {
        using var file = MakeSheet(
            ("Item", "Amount owed", "Notes"),
            ("X-1", "10", "n/a"));

        Assert.Throws<InvalidImportException>(() => Parser.Parse(file));
    }

    [Fact]
    public void Header_row_is_found_below_a_title_row_and_columns_mapped_by_name()
    {
        using var file = MakeSheet(
            ("Purchase order from ACME", "", ""),
            ("Part No", "Quantity", "Unit Price"),
            ("AB-1", "3", "1.25"));

        var sheet = Parser.Parse(file);

        var row = Assert.Single(sheet.Rows);
        Assert.Equal("AB-1", row.RawCode);
        Assert.Equal(3m, row.Quantity);
        Assert.Equal(1.25m, row.UnitPrice);
    }

    private static Stream MakeSheet(params (string A, string B, string C)[] rows)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.AddWorksheet("Sheet1");
        for (var i = 0; i < rows.Length; i++)
        {
            ws.Cell(i + 1, 1).Value = rows[i].A;
            if (rows[i].B.Length > 0) ws.Cell(i + 1, 2).Value = rows[i].B;
            if (rows[i].C.Length > 0) ws.Cell(i + 1, 3).Value = rows[i].C;
        }

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }
}
