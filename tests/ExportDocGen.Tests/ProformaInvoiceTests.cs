using System.Text;
using ExportDocGen.Data;
using ExportDocGen.Data.Entities;
using ExportDocGen.Documents;
using ExportDocGen.Services;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;

namespace ExportDocGen.Tests;

public class ProformaInvoiceTests
{
    private static Product Filter(string part, string hs) => new()
    {
        Id = part.GetHashCode() & 0x7fffffff,
        PartNumber = part,
        Description = $"{part} filter",
        HsCode = hs,
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
        var af = Filter("AF-1", "8421.31");
        var of = Filter("OF-2", "8421.23");
        return new Order
        {
            OrderNumber = "EXP-2026-0007",
            OrderDate = new DateOnly(2026, 8, 31),
            Incoterm = "FOB Izmir",
            Currency = "USD",
            PaymentTerms = "30% advance, 70% before shipment",
            Customer = new Customer
            {
                Name = "Gulf Auto Spare Parts LLC",
                AddressLine1 = "Deira",
                City = "Dubai",
                Country = "United Arab Emirates",
                ContactName = "A. Rahman",
            },
            Lines =
            {
                new OrderLine { LineNumber = 1, ProductId = af.Id, Product = af, Quantity = 100, UnitPrice = 7.4m },
                new OrderLine { LineNumber = 2, ProductId = of.Id, Product = of, Quantity = 250, UnitPrice = 2.1m },
            },
        };
    }

    private static OrderCalculation Calculate(Order order) =>
        new CalculationService().CalculateOrder(order.Lines
            .OrderBy(l => l.LineNumber)
            .Select(l => (l.Quantity, l.UnitPrice, l.Product!)));

    [Fact]
    public void Model_maps_order_customer_and_totals()
    {
        var order = SampleOrder();
        var calc = Calculate(order);

        var model = ProformaInvoiceModel.From(order, calc, new CompanyProfile());

        Assert.Equal("EXP-2026-0007", model.InvoiceNumber);
        Assert.Equal("Gulf Auto Spare Parts LLC", model.BuyerName);
        Assert.Contains("Dubai", model.BuyerAddress);
        Assert.Equal("FOB Izmir", model.Incoterm);
        Assert.Equal(2, model.Lines.Count);
        Assert.Equal("8421.31", model.Lines[0].HsCode);
        Assert.Equal(100 * 7.4m + 250 * 2.1m, model.TotalAmount);
        Assert.Equal(calc.TotalGrossWeightKg, model.TotalGrossWeightKg);
        Assert.Equal("Türkiye", model.CountryOfOrigin);
    }

    [Fact]
    public void Document_renders_a_pdf()
    {
        var order = SampleOrder();
        var model = ProformaInvoiceModel.From(order, Calculate(order), new CompanyProfile());

        var bytes = new ProformaInvoiceDocument(model).GeneratePdf();

        Assert.True(bytes.Length > 1000);
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(bytes, 0, 5));
    }

    [Fact]
    public async Task Service_builds_a_named_proforma_for_a_saved_order()
    {
        using var factory = new SqliteTestFactory();
        var customers = new CustomerService(factory);
        var orders = new OrderService(factory, new OrderNumberGenerator(factory));

        var customer = await customers.CreateAsync(new Customer
        {
            Name = "Muster GmbH", AddressLine1 = "Industriestr. 1",
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
            CustomerId = customer.Id, Currency = "EUR", OrderDate = new DateOnly(2026, 8, 31),
            Lines = { new OrderLine { ProductId = product.Id, Quantity = 24, UnitPrice = 7.4m } },
        });

        var service = new OrderDocumentService(
            orders, new CalculationService(), Options.Create(new CompanyProfile()));

        var doc = await service.BuildProformaAsync(id);

        Assert.NotNull(doc);
        Assert.EndsWith("-proforma.pdf", doc!.FileName);
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(doc.Bytes, 0, 5));

        Assert.Null(await service.BuildProformaAsync(999));
    }
}
