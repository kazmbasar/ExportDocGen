using System.Text;
using ExportDocGen.Data.Entities;
using ExportDocGen.Documents;
using ExportDocGen.Services;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using QuestPDF.Fluent;

namespace ExportDocGen.Tests;

public class PackingListTests
{
    private static Product Filter(string part, string type) => new()
    {
        Id = part.GetHashCode() & 0x7fffffff,
        PartNumber = part,
        Description = $"{part} filter",
        HsCode = "8421.31",
        FilterType = type,
        NetWeightKg = 0.8m,
        GrossWeightKg = 0.84m,
        UnitVolumeM3 = 0.0075m,
    };

    private static Order SampleOrder()
    {
        var air = Filter("A1209", "air");
        var oil = Filter("L3098", "oil");
        return new Order
        {
            OrderNumber = "EXP-2026-0007",
            OrderDate = new DateOnly(2026, 8, 31),
            Incoterm = "FOB Izmir",
            Currency = "USD",
            Customer = new Customer
            {
                Name = "Gulf Auto Spare Parts LLC",
                AddressLine1 = "Deira, Al Maktoum Road",
                City = "Dubai",
                Country = "United Arab Emirates",
            },
            Lines =
            {
                new OrderLine { LineNumber = 1, ProductId = air.Id, Product = air, Quantity = 100, UnitPrice = 7m },
                new OrderLine { LineNumber = 2, ProductId = oil.Id, Product = oil, Quantity = 55, UnitPrice = 3m },
            },
        };
    }

    private static SellerCompany Seller(ProformaTemplate template = ProformaTemplate.FiltorqClassic) => new()
    {
        Name = "Filtorq Filtre A.Ş.",
        ShortName = "Filtorq",
        ProformaTemplate = template,
        NumberFormat = SellerNumberFormat.ExpYearSeq,
        CountryOfOrigin = "Türkiye",
    };

    private static OrderCalculation Calculate(Order order) =>
        new CalculationService().CalculateOrder(order.Lines
            .OrderBy(l => l.LineNumber)
            .Select(l => (l.Quantity, l.UnitPrice, l.Product!)));

    [Fact]
    public void Model_maps_lines_and_totals_from_the_calculation()
    {
        var order = SampleOrder();
        var calc = Calculate(order);

        var model = PackingListModel.From(order, calc, Seller());

        Assert.Equal("EXP-2026-0007", model.Reference);
        Assert.Equal("Gulf Auto Spare Parts LLC", model.BuyerName);
        Assert.Contains("United Arab Emirates", model.BuyerAddress);
        Assert.Equal(2, model.Lines.Count);
        Assert.Equal("AIR FILTER", model.Lines[0].Description);
        Assert.Equal("8421.31", model.Lines[0].HsCode);
        Assert.Equal(1, model.Lines[0].No);

        // Totals come straight from CalculationService — guards the line pairing.
        Assert.Equal(155, model.TotalQuantity);
        Assert.Equal(calc.TotalNetWeightKg, model.TotalNetWeightKg);
        Assert.Equal(calc.TotalGrossWeightKg, model.TotalGrossWeightKg);
        Assert.Equal(calc.TotalVolumeM3, model.TotalVolumeM3);
        Assert.Equal(model.Lines.Sum(l => l.NetWeightKg), model.TotalNetWeightKg);
    }

    [Fact]
    public void Document_renders_a_pdf_with_and_without_a_letterhead()
    {
        var order = SampleOrder();
        var calc = Calculate(order);

        var plain = new PackingListDocument(PackingListModel.From(order, calc, Seller())).GeneratePdf();
        Assert.True(plain.Length > 1000);
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(plain, 0, 5));

        var ikiler = new PackingListDocument(
            PackingListModel.From(order, calc, Seller(ProformaTemplate.IkilerGrid), letterhead: FakePng()))
            .GeneratePdf();
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(ikiler, 0, 5));
    }

    [Fact]
    public async Task Service_builds_a_named_packing_list_for_a_saved_order()
    {
        using var factory = new SqliteTestFactory();
        var customers = new CustomerService(factory);
        var orders = new OrderService(factory, new OrderNumberGenerator(factory));
        var seller = await TestData.SeedSellerAsync(factory);

        var customer = await customers.CreateAsync(new Customer
        {
            Name = "Buyer", SellerCompanyId = seller.Id, AddressLine1 = "x",
            Country = "UAE", DefaultCurrency = "USD",
        });
        Product product;
        await using (var db = factory.CreateDbContext())
        {
            product = new Product
            {
                PartNumber = "A1209", Description = "Air filter", FilterType = "air",
                NetWeightKg = 0.8m, GrossWeightKg = 0.84m, UnitVolumeM3 = 0.01m,
            };
            db.Add(product);
            await db.SaveChangesAsync();
        }
        var id = await orders.CreateAsync(new Order
        {
            CustomerId = customer.Id, Currency = "USD", OrderDate = new DateOnly(2026, 8, 31),
            Lines = { new OrderLine { ProductId = product.Id, Quantity = 24, UnitPrice = 7m } },
        });

        var service = new OrderDocumentService(orders, new CalculationService(), new TestHostEnvironment());

        var doc = await service.BuildPackingListAsync(id);
        Assert.NotNull(doc);
        Assert.EndsWith("-packing-list.pdf", doc!.FileName);
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(doc.Bytes, 0, 5));

        Assert.Null(await service.BuildPackingListAsync(999));
    }

    private static byte[] FakePng() => Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string ApplicationName { get; set; } = "Tests";
        public string EnvironmentName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
