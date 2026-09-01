using System.Text;
using ExportDocGen.Data.Entities;
using ExportDocGen.Documents;
using ExportDocGen.Services;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using QuestPDF.Fluent;

namespace ExportDocGen.Tests;

public class CommercialInvoiceTests
{
    private static Product Filter(string part, string type, string brand) => new()
    {
        Id = part.GetHashCode() & 0x7fffffff,
        PartNumber = part,
        Description = $"{part} filter",
        HsCode = "8421.31.00.90.00",
        FilterType = type,
        Brand = brand,
        Origin = "TURKEY",
        NetWeightKg = 2.5m,
        GrossWeightKg = 2.625m,
        UnitVolumeM3 = 0.047m,
    };

    private static Order SampleOrder()
    {
        var a = Filter("A2043", "air", "FILTORQ");
        var o = Filter("L5098", "oil", "FILTORQ");
        return new Order
        {
            OrderNumber = "EXP-2026-0011",
            OrderDate = new DateOnly(2026, 8, 20),
            InvoiceNumber = "EFA2026000038518",
            InvoiceDate = new DateOnly(2026, 8, 28),
            Pallets = 11,
            Incoterm = "FCA Istanbul",
            Currency = "USD",
            PaymentTerms = "100% ADVANCE",
            BankDetails = "Company Name : İkiler Otomotiv\nOur Bank : VAKIFBANK",
            Customer = new Customer
            {
                Name = "LLC GLOBAL EXPO", AddressLine1 = "Zhumabeka 105/1",
                City = "Bishkek", Country = "Kyrgyzstan",
                ContactEmail = "buyer@example.com", ContactPhone = "00996 555 551 490",
            },
            Lines =
            {
                new OrderLine { LineNumber = 1, ProductId = a.Id, Product = a, Quantity = 300, UnitPrice = 11.85m },
                new OrderLine { LineNumber = 2, ProductId = o.Id, Product = o, Quantity = 9, UnitPrice = 5.90m },
            },
        };
    }

    private static SellerCompany Seller(ProformaTemplate t = ProformaTemplate.IkilerGrid) => new()
    {
        Name = "İkiler Otomotiv Filtre A.Ş.", ShortName = "İkiler",
        ProformaTemplate = t, NumberFormat = SellerNumberFormat.DateSlashSeq,
        CountryOfOrigin = "Türkiye",
    };

    private static OrderCalculation Calc(Order o) =>
        new CalculationService().CalculateOrder(o.Lines
            .OrderBy(l => l.LineNumber).Select(l => (l.Quantity, l.UnitPrice, l.Product!)));

    [Fact]
    public void Model_maps_lines_totals_and_the_invoice_number()
    {
        var order = SampleOrder();
        var calc = Calc(order);

        var m = CommercialInvoiceModel.From(order, calc, Seller());

        Assert.Equal("EFA2026000038518", m.InvoiceNumber);
        Assert.Equal(new DateOnly(2026, 8, 28), m.InvoiceDate);
        Assert.Equal(2, m.Lines.Count);
        Assert.Equal("FILTORQ", m.Lines[0].Brand);
        Assert.Equal("TURKEY", m.Lines[0].Origin);
        Assert.Equal(300 * 11.85m + 9 * 5.90m, m.TotalAmount);
        Assert.Equal(309, m.TotalQuantity);
        Assert.Equal(calc.TotalGrossWeightKg, m.TotalGrossWeightKg);
        Assert.Equal("11 PALLETS", m.TotalVolumeText);
    }

    [Fact]
    public void Invoice_number_and_date_fall_back_to_the_order()
    {
        var order = SampleOrder();
        order.InvoiceNumber = null;
        order.InvoiceDate = null;
        order.Pallets = null;

        var m = CommercialInvoiceModel.From(order, Calc(order), Seller());

        Assert.Equal("EXP-2026-0011", m.InvoiceNumber);
        Assert.Equal(new DateOnly(2026, 8, 20), m.InvoiceDate);
        Assert.EndsWith("CBM", m.TotalVolumeText);
    }

    [Fact]
    public void Document_renders_a_pdf_with_and_without_a_letterhead()
    {
        var order = SampleOrder();
        var calc = Calc(order);

        var plain = new CommercialInvoiceDocument(
            CommercialInvoiceModel.From(order, calc, Seller())).GeneratePdf();
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(plain, 0, 5));
        Assert.True(plain.Length > 1000);

        var filtorq = new CommercialInvoiceDocument(
            CommercialInvoiceModel.From(order, calc, Seller(ProformaTemplate.FiltorqClassic), letterhead: FakePng()))
            .GeneratePdf();
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(filtorq, 0, 5));
    }

    [Fact]
    public async Task Service_builds_a_named_commercial_invoice_for_a_saved_order()
    {
        using var factory = new SqliteTestFactory();
        var customers = new CustomerService(factory);
        var orders = new OrderService(factory, new OrderNumberGenerator(factory));
        var seller = await TestData.SeedSellerAsync(factory);
        var customer = await customers.CreateAsync(new Customer
        {
            Name = "Buyer", SellerCompanyId = seller.Id, AddressLine1 = "x",
            Country = "KG", DefaultCurrency = "USD",
        });
        Product product;
        await using (var db = factory.CreateDbContext())
        {
            product = new Product
            {
                PartNumber = "A2043", Description = "Air filter", FilterType = "air",
                Brand = "FILTORQ", Origin = "TURKEY",
                NetWeightKg = 2.5m, GrossWeightKg = 2.625m, UnitVolumeM3 = 0.047m,
            };
            db.Add(product);
            await db.SaveChangesAsync();
        }
        var id = await orders.CreateAsync(new Order
        {
            CustomerId = customer.Id, Currency = "USD", OrderDate = new DateOnly(2026, 8, 20),
            Lines = { new OrderLine { ProductId = product.Id, Quantity = 300, UnitPrice = 11.85m } },
        });

        var service = new OrderDocumentService(orders, new CalculationService(), new TestHostEnvironment());

        var doc = await service.BuildCommercialInvoiceAsync(id);
        Assert.NotNull(doc);
        Assert.EndsWith("-commercial-invoice.pdf", doc!.FileName);
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(doc.Bytes, 0, 5));
        Assert.Null(await service.BuildCommercialInvoiceAsync(999));
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
