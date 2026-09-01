using ClosedXML.Excel;
using ExportDocGen.Data.Entities;
using ExportDocGen.Services;

namespace ExportDocGen.Tests;

public class StockCatalogImportTests
{
    /// <summary>A workbook with the real stock-file header and a handful of rows
    /// covering the cases the parser has to handle.</summary>
    private static MemoryStream MakeWorkbook()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Sheet1");
        ws.Cell(1, 1).Value = "Description";
        ws.Cell(1, 2).Value = "MENŞEİ";
        ws.Cell(1, 3).Value = "MARKA";
        ws.Cell(1, 4).Value = "CİNSİ";
        ws.Cell(1, 5).Value = "GTIP";
        ws.Cell(1, 9).Value = "Net weight";
        ws.Cell(1, 10).Value = "MU";
        ws.Cell(1, 11).Value = "m3";

        Row(2, "A1209", "TURKEY", "FILTORQ", "AIR FILTER", "8421.31.00.90.00", 0.65, 0.004129);
        Row(3, "L3098", "USA", "FLEETGUARD", "OIL FILTER", "8421.23.00.00.00", 1.305, 0.00775);
        Row(4, "A1209", "TURKEY", "FILTORQ", "AIR FILTER", "8421.31.00.90.00", 0.65, 0.004129); // duplicate code
        Row(5, "W-1", "#N/A", "-", "WATER FILTER", "", 0.5, 0.001);                             // origin/brand blank
        Row(6, "X-9", "GERMANY", "BOSCH", "WIPER", "", 0.01, 0.0002);                           // not a filter
        Row(7, "P101271", "", "DONALDSON", "AIR FILTER", "8421.31.00.90.00", 1.625, null);      // volume error / blank
        Row(8, "", "TURKEY", "MANN", "OIL FILTER", "", 1, 0.01);                                // blank code

        var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;
        return ms;

        void Row(int r, string code, string origin, string brand, string cinsi, string gtip, double net, double? m3)
        {
            ws.Cell(r, 1).Value = code;
            ws.Cell(r, 2).Value = origin;
            ws.Cell(r, 3).Value = brand;
            ws.Cell(r, 4).Value = cinsi;
            ws.Cell(r, 5).Value = gtip;
            ws.Cell(r, 9).Value = net;
            ws.Cell(r, 10).Value = "KG";
            if (m3 is { } v) ws.Cell(r, 11).Value = v;
            else ws.Cell(r, 11).Value = "#VALUE!";
        }
    }

    [Fact]
    public void Parse_keeps_filters_only_and_flags_the_rest()
    {
        using var file = MakeWorkbook();
        var result = new StockCatalogImportService(new SqliteTestFactory()).Parse(file);

        Assert.Equal(new[] { "A1209", "L3098", "W-1", "P101271" }, result.Rows.Select(r => r.Code));
        Assert.Equal(1, result.SkippedBlankCode);
        Assert.Equal(1, result.SkippedDuplicate);
        Assert.Equal(1, result.SkippedNonFilter);
        Assert.Equal(1, result.ZeroVolume);   // P101271 (#VALUE!)
    }

    [Fact]
    public void Parse_reads_values_the_stock_file_way()
    {
        using var file = MakeWorkbook();
        var rows = new StockCatalogImportService(new SqliteTestFactory()).Parse(file).Rows
            .ToDictionary(r => r.Code);

        Assert.Equal("air", rows["A1209"].FilterType);
        Assert.Equal("water", rows["W-1"].FilterType);
        Assert.Equal(1.305m, rows["L3098"].NetWeightKg);
        Assert.Equal(0.004129m, rows["A1209"].UnitVolumeM3, 6);
        Assert.Equal("8421.31.00.90.00", rows["A1209"].HsCode);
        Assert.Equal("8421.23.00.00.00", rows["L3098"].HsCode);
        Assert.Null(rows["W-1"].Origin);   // "#N/A" → null
        Assert.Null(rows["W-1"].Brand);    // "-" → null
        Assert.Null(rows["W-1"].HsCode);   // blank → null
        Assert.Equal(0m, rows["P101271"].UnitVolumeM3);   // "#VALUE!" → 0
    }

    [Fact]
    public async Task ReplaceCatalogue_wipes_and_inserts_with_gross_uplift()
    {
        using var factory = new SqliteTestFactory();
        await using (var db = factory.CreateDbContext())
        {
            db.Products.Add(new Product { PartNumber = "OLD", Description = "old" });
            await db.SaveChangesAsync();
        }
        var service = new StockCatalogImportService(factory);

        using var file = MakeWorkbook();
        var count = await service.ReplaceCatalogueAsync(service.Parse(file).Rows);

        Assert.Equal(4, count);
        await using var check = factory.CreateDbContext();
        Assert.DoesNotContain(check.Products, p => p.PartNumber == "OLD");
        var a1209 = check.Products.Single(p => p.PartNumber == "A1209");
        Assert.Equal(0.65m, a1209.NetWeightKg);
        Assert.Equal(0.683m, a1209.GrossWeightKg);   // 0.65 × 1.05 = 0.6825 → 0.683 (half away from zero)
    }

    [Fact]
    public async Task ReplaceCatalogue_refuses_when_a_product_is_on_an_order()
    {
        using var factory = new SqliteTestFactory();
        var seller = await TestData.SeedSellerAsync(factory);
        var customerId = await TestData.SeedCustomerAsync(factory, seller.Id);
        var orders = new OrderService(factory, new OrderNumberGenerator(factory));
        int productId;
        await using (var db = factory.CreateDbContext())
        {
            var p = new Product { PartNumber = "IN-USE", Description = "x" };
            db.Products.Add(p);
            await db.SaveChangesAsync();
            productId = p.Id;
        }
        await orders.CreateAsync(new Order
        {
            CustomerId = customerId, Currency = "USD", OrderDate = new DateOnly(2026, 1, 1),
            Lines = { new OrderLine { ProductId = productId, Quantity = 1, UnitPrice = 1m } },
        });

        var service = new StockCatalogImportService(factory);
        using var file = MakeWorkbook();
        var rows = service.Parse(file).Rows;

        var ex = await Assert.ThrowsAsync<InvalidImportException>(() => service.ReplaceCatalogueAsync(rows));
        Assert.Contains("IN-USE", ex.Message);
    }
}
