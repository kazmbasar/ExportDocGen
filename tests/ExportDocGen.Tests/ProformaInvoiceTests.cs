using System.Text;
using ExportDocGen.Data;
using ExportDocGen.Data.Entities;
using ExportDocGen.Documents;
using ExportDocGen.Services;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;

namespace ExportDocGen.Tests;

public class ProformaInvoiceTests
{
    private static Product Filter(string part) => new()
    {
        Id = part.GetHashCode() & 0x7fffffff,
        PartNumber = part,
        Description = $"{part} filter",
        NetWeightKg = 0.5m,
        GrossWeightKg = 0.6m,
        UnitsPerCarton = 10,
        CartonLengthCm = 40,
        CartonWidthCm = 30,
        CartonHeightCm = 20,
        CartonTareWeightKg = 0.4m,
    };

    private static Order SampleOrder()
    {
        var af = Filter("AF-1");
        var of = Filter("OF-2");
        return new Order
        {
            OrderNumber = "EXP-2026-0007",
            OrderDate = new DateOnly(2026, 8, 31),
            Incoterm = "EXW Denizli",
            Currency = "USD",
            PaymentTerms = "100% Prepayment",
            Customer = new Customer
            {
                Name = "LTD LEOMOTORS",
                TaxId = "412745187",
                AddressLine1 = "Khoravs N4",
                City = "Kutaisi",
                Country = "Georgia",
                ContactName = "L. Lekvinadze",
                ContactEmail = "buyer@example.com",
                ContactPhone = "+995 598 77 67 99",
            },
            Lines =
            {
                new OrderLine { LineNumber = 1, ProductId = af.Id, Product = af, Quantity = 100, UnitPrice = 7.4m },
                new OrderLine { LineNumber = 2, ProductId = of.Id, Product = of, Quantity = 250, UnitPrice = 2.1m },
            },
        };
    }

    private static CompanyProfile Company() => new()
    {
        Name = "Filtorq Filtre A.Ş.",
        Bank = new BankDetails
        {
            BeneficiaryName = "Filtorq Filtre A.Ş.",
            BankName = "Ziraat Bankası",
            Swift = "TCZBTR2AXXX",
            Iban = "TR62 0001 0020 6383 4792 2750 06",
        },
    };

    private static OrderCalculation Calculate(Order order) =>
        new CalculationService().CalculateOrder(order.Lines
            .OrderBy(l => l.LineNumber)
            .Select(l => (l.Quantity, l.UnitPrice, l.Product!)));

    [Fact]
    public void Model_maps_buyer_invoice_and_lines()
    {
        var order = SampleOrder();

        var model = ProformaInvoiceModel.From(order, Calculate(order), Company());

        Assert.Equal("EXP-2026-0007", model.InvoiceNumber);
        Assert.Equal("LTD LEOMOTORS", model.BuyerName);
        Assert.Equal("412745187", model.BuyerTaxId);
        Assert.Equal("+995 598 77 67 99", model.BuyerPhone);
        Assert.Contains("Georgia", model.BuyerAddress);
        Assert.Equal("EXW Denizli", model.Incoterm);
        Assert.Equal(2, model.Lines.Count);
        Assert.Equal("AF-1", model.Lines[0].Code);
        Assert.Equal(100 * 7.4m + 250 * 2.1m, model.TotalAmount);
        Assert.Equal("TCZBTR2AXXX", model.Bank.Swift);
    }

    [Fact]
    public void Document_renders_a_pdf_with_and_without_a_letterhead()
    {
        var order = SampleOrder();
        var calc = Calculate(order);

        var plain = new ProformaInvoiceDocument(
            ProformaInvoiceModel.From(order, calc, Company())).GeneratePdf();
        Assert.True(plain.Length > 1000);
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(plain, 0, 5));

        var withLetterhead = new ProformaInvoiceDocument(
            ProformaInvoiceModel.From(order, calc, Company(), letterhead: FakePng())).GeneratePdf();
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(withLetterhead, 0, 5));
    }

    [Fact]
    public async Task Service_builds_a_named_proforma_for_a_saved_order()
    {
        using var factory = new SqliteTestFactory();
        var customers = new CustomerService(factory);
        var orders = new OrderService(factory, new OrderNumberGenerator(factory));
        var seller = await TestData.SeedSellerAsync(factory);

        var customer = await customers.CreateAsync(new Customer
        {
            Name = "Muster GmbH", TaxId = "DE123", AddressLine1 = "Industriestr. 1",
            City = "Hamburg", Country = "Germany", DefaultCurrency = "EUR",
        });
        Product product;
        await using (var db = factory.CreateDbContext())
        {
            product = new Product
            {
                PartNumber = "AF-1042", Description = "Air filter",
                NetWeightKg = 0.8m, GrossWeightKg = 0.9m, UnitsPerCarton = 12,
                CartonLengthCm = 60, CartonWidthCm = 40, CartonHeightCm = 45,
                CartonTareWeightKg = 0.9m,
            };
            db.Add(product);
            await db.SaveChangesAsync();
        }
        var id = await orders.CreateAsync(new Order
        {
            CustomerId = customer.Id, SellerCompanyId = seller.Id, Currency = "EUR",
            OrderDate = new DateOnly(2026, 8, 31),
            Lines = { new OrderLine { ProductId = product.Id, Quantity = 24, UnitPrice = 7.4m } },
        });

        var service = new OrderDocumentService(
            orders, new CalculationService(), Options.Create(new CompanyProfile()),
            new TestHostEnvironment());

        var doc = await service.BuildProformaAsync(id);

        Assert.NotNull(doc);
        Assert.EndsWith("-proforma.pdf", doc!.FileName);
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(doc.Bytes, 0, 5));

        Assert.Null(await service.BuildProformaAsync(999));
    }

    // A 1x1 transparent PNG.
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
