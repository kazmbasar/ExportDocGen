using ExportDocGen.Data;
using ExportDocGen.Documents;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;

namespace ExportDocGen.Services;

/// <summary>Turns a saved order into a downloadable PDF document. Loads the
/// order, runs the shared <see cref="CalculationService"/>, and renders it with
/// the company profile from configuration.</summary>
public class OrderDocumentService(
    OrderService orders,
    CalculationService calculator,
    IOptions<CompanyProfile> company)
{
    /// <summary>Builds the proforma invoice PDF for an order, or <c>null</c> if
    /// the order does not exist.</summary>
    public async Task<GeneratedDocument?> BuildProformaAsync(int orderId)
    {
        var order = await orders.GetAsync(orderId);
        if (order is null)
            return null;

        // Product is a restrict-delete FK, so every persisted line has one.
        // Keep this ordering identical to ProformaInvoiceModel.From.
        var calculation = calculator.CalculateOrder(order.Lines
            .OrderBy(l => l.LineNumber)
            .Where(l => l.Product is not null)
            .Select(l => (l.Quantity, l.UnitPrice, l.Product!)));

        var model = ProformaInvoiceModel.From(order, calculation, company.Value);
        var bytes = new ProformaInvoiceDocument(model).GeneratePdf();

        return new GeneratedDocument(bytes, $"{Sanitize(order.OrderNumber)}-proforma.pdf");
    }

    private static string Sanitize(string value)
    {
        var clean = new string(value.Select(c =>
            char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '-').ToArray());
        return string.IsNullOrWhiteSpace(clean) ? "order" : clean;
    }
}

/// <summary>A rendered document ready to stream to the browser.</summary>
public sealed record GeneratedDocument(byte[] Bytes, string FileName)
{
    public const string PdfContentType = "application/pdf";
}
