using ExportDocGen.Data.Entities;
using ExportDocGen.Documents;
using Microsoft.Extensions.Hosting;
using QuestPDF.Fluent;

namespace ExportDocGen.Services;

/// <summary>Turns a saved order into a downloadable PDF document. Loads the
/// order (with its seller company), runs the shared
/// <see cref="CalculationService"/>, reads the seller's letterhead, and renders
/// the proforma with that company's template.</summary>
public class OrderDocumentService(
    OrderService orders,
    CalculationService calculator,
    IHostEnvironment environment)
{
    /// <summary>Builds the proforma invoice PDF for an order, or <c>null</c> if
    /// the order does not exist.</summary>
    public async Task<GeneratedDocument?> BuildProformaAsync(int orderId)
    {
        var order = await orders.GetAsync(orderId);
        if (order is null)
            return null;

        var seller = order.SellerCompany
            ?? throw new InvalidOperationException($"Order {orderId} has no seller company.");

        // Product is a restrict-delete FK, so every persisted line has one.
        // Keep this ordering identical to ProformaInvoiceModel.From.
        var calculation = calculator.CalculateOrder(order.Lines
            .OrderBy(l => l.LineNumber)
            .Where(l => l.Product is not null)
            .Select(l => (l.Quantity, l.UnitPrice, l.Product!)));

        var model = ProformaInvoiceModel.From(order, calculation, seller,
            letterhead: ReadAsset(seller.LetterheadPath));

        // Only the Filtorq template exists so far; IkilerGrid is added in M6.5c.
        var bytes = new ProformaInvoiceDocument(model).GeneratePdf();
        return new GeneratedDocument(bytes, $"{Sanitize(order.OrderNumber)}-proforma.pdf");
    }

    /// <summary>Reads a configured asset. Paths are resolved against the content
    /// root (so <c>wwwroot/...</c> works under <c>dotnet run</c> and when
    /// published); a missing file yields <c>null</c> and the document falls back.</summary>
    private byte[]? ReadAsset(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        var full = Path.IsPathRooted(path)
            ? path
            : Path.Combine(environment.ContentRootPath, path);

        return File.Exists(full) ? File.ReadAllBytes(full) : null;
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
