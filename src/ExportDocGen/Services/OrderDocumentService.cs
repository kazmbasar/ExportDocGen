using ExportDocGen.Data;
using ExportDocGen.Documents;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;

namespace ExportDocGen.Services;

/// <summary>Turns a saved order into a downloadable PDF document. Loads the
/// order, runs the shared <see cref="CalculationService"/>, reads the company
/// profile and letterhead, and renders the document.</summary>
public class OrderDocumentService(
    OrderService orders,
    CalculationService calculator,
    IOptions<CompanyProfile> company,
    IHostEnvironment environment)
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

        var profile = company.Value;
        var model = ProformaInvoiceModel.From(order, calculation, profile,
            letterhead: ReadAsset(profile.LetterheadPath),
            logo: ReadAsset(profile.LogoPath));

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
