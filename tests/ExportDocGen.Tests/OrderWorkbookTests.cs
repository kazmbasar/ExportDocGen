using System.Text;
using ClosedXML.Excel;
using ExportDocGen.Data.Entities;
using ExportDocGen.Documents;
using ExportDocGen.Services;

namespace ExportDocGen.Tests;

public class OrderWorkbookTests
{
    private static Product Filter(string part) => new()
    {
        Id = part.GetHashCode() & 0x7fffffff,
        PartNumber = part, Description = "AIR FILTER", HsCode = "8421.31.00.90.00",
        FilterType = "air", Brand = "FILTORQ", Origin = "TURKEY",
        NetWeightKg = 2.5m, GrossWeightKg = 2.625m, UnitVolumeM3 = 0.047m,
    };

    private static Order Order()
    {
        var p = Filter("A2043");
        return new Order
        {
            OrderNumber = "EXP-2026-0011", OrderDate = new DateOnly(2026, 8, 20),
            InvoiceNumber = "EFA-123", Currency = "USD", Incoterm = "FCA Istanbul",
            PaymentTerms = "100% ADVANCE", Pallets = 11,
            BankDetails = "Our Bank : VAKIFBANK",
            Customer = new Customer { Name = "LLC GLOBAL EXPO", AddressLine1 = "x", Country = "KG" },
            Lines = { new OrderLine { LineNumber = 1, ProductId = p.Id, Product = p, Quantity = 300, UnitPrice = 11.85m } },
        };
    }

    private static SellerCompany Seller() => new()
    {
        Name = "İkiler Otomotiv A.Ş.", ShortName = "İkiler",
        ProformaTemplate = ProformaTemplate.IkilerGrid, NumberFormat = SellerNumberFormat.DateSlashSeq,
    };

    private static OrderCalculation Calc(Order o) =>
        new CalculationService().CalculateOrder(o.Lines
            .OrderBy(l => l.LineNumber).Select(l => (l.Quantity, l.UnitPrice, l.Product!)));

    [Fact]
    public void Commercial_invoice_workbook_is_a_valid_xlsx_with_the_totals()
    {
        var order = Order();
        var bytes = OrderWorkbooks.CommercialInvoice(
            CommercialInvoiceModel.From(order, Calc(order), Seller()));

        Assert.Equal("PK", Encoding.ASCII.GetString(bytes, 0, 2));   // ZIP magic

        using var wb = new XLWorkbook(new MemoryStream(bytes));
        var ws = wb.Worksheet(1);
        Assert.Contains(ws.CellsUsed(), c => c.GetString() == "COMMERCIAL INVOICE");
        Assert.Contains(ws.CellsUsed(), c => c.GetString().Contains("EFA-123"));
        Assert.Contains(ws.CellsUsed(), c => c.GetString().Contains("11 PALLETS"));
        // grand total 300 × 11.85
        Assert.Contains(ws.CellsUsed(), c => c.DataType == XLDataType.Number && c.GetDouble() == 3555d);
    }

    [Fact]
    public void Packing_list_workbook_is_a_valid_xlsx_with_the_headers()
    {
        var order = Order();
        var bytes = OrderWorkbooks.PackingList(
            PackingListModel.From(order, Calc(order), Seller()));

        Assert.Equal("PK", Encoding.ASCII.GetString(bytes, 0, 2));

        using var wb = new XLWorkbook(new MemoryStream(bytes));
        var ws = wb.Worksheet(1);
        Assert.Contains(ws.CellsUsed(), c => c.GetString() == "PACKING LIST");
        Assert.Contains(ws.CellsUsed(), c => c.GetString() == "TOTAL GROSS WEIGHT");
        Assert.Contains(ws.CellsUsed(), c => c.GetString() == "A2043");
    }
}
