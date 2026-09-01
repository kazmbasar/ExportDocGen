using ExportDocGen.Data.Entities;
using ExportDocGen.Documents;
using Microsoft.Extensions.Hosting;
using QuestPDF.Fluent;

namespace ExportDocGen.Services;

/// <summary>Turns a saved order into a downloadable PDF document. Loads the
/// order (with its seller company), runs the shared
/// <see cref="CalculationService"/>, reads the seller's letterhead, and renders
/// the proforma / packing list with that company's template.</summary>
public class OrderDocumentService(
    OrderService orders,
    CalculationService calculator,
    IHostEnvironment environment)
{
    /// <summary>Builds the proforma invoice PDF for an order, or <c>null</c> if
    /// the order does not exist.</summary>
    public async Task<GeneratedDocument?> BuildProformaAsync(int orderId)
    {
        if (await PrepareAsync(orderId) is not { } p)
            return null;
        var (order, calculation, seller, letterhead) = p;

        var model = ProformaInvoiceModel.From(order, calculation, seller, letterhead);

        QuestPDF.Infrastructure.IDocument document = model.Template switch
        {
            ProformaTemplate.IkilerGrid => new IkilerProformaDocument(model),
            _ => new ProformaInvoiceDocument(model),
        };
        return new GeneratedDocument(
            document.GeneratePdf(), $"{Sanitize(order.OrderNumber)}-proforma.pdf");
    }

    /// <summary>Builds the packing list PDF for an order, or <c>null</c> if the
    /// order does not exist.</summary>
    public async Task<GeneratedDocument?> BuildPackingListAsync(int orderId)
    {
        if (await PrepareAsync(orderId) is not { } p)
            return null;
        var (order, calculation, seller, letterhead) = p;

        var model = PackingListModel.From(order, calculation, seller, letterhead);
        var bytes = new PackingListDocument(model).GeneratePdf();
        return new GeneratedDocument(bytes, $"{Sanitize(order.OrderNumber)}-packing-list.pdf");
    }

    /// <summary>Loads the order with its seller, runs the calculation and reads
    /// the letterhead — the shared front half of every document build. The line
    /// ordering here must match <c>*Model.From</c>.</summary>
    private async Task<(Order Order, OrderCalculation Calculation, SellerCompany Seller, byte[]? Letterhead)?>
        PrepareAsync(int orderId)
    {
        var order = await orders.GetAsync(orderId);
        if (order is null)
            return null;

        var seller = order.SellerCompany
            ?? throw new InvalidOperationException($"Order {orderId} has no seller company.");

        // Product is a restrict-delete FK, so every persisted line has one.
        var calculation = calculator.CalculateOrder(order.Lines
            .OrderBy(l => l.LineNumber)
            .Where(l => l.Product is not null)
            .Select(l => (l.Quantity, l.UnitPrice, l.Product!)));

        return (order, calculation, seller, ReadAsset(seller.LetterheadPath));
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
