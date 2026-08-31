using ExportDocGen.Data;
using ExportDocGen.Data.Entities;
using ExportDocGen.Services;

namespace ExportDocGen.Documents;

/// <summary>Flat, self-contained data for one proforma invoice — everything the
/// <see cref="ProformaInvoiceDocument"/> needs, with no database or config
/// lookups left to do. Build it with <see cref="From"/>.</summary>
public sealed record ProformaInvoiceModel
{
    public required string SellerName { get; init; }
    public required IReadOnlyList<string> SellerAddress { get; init; }
    public required string SellerTaxId { get; init; }
    public required string SellerPhone { get; init; }
    public required string SellerEmail { get; init; }
    public string? SellerLogoPath { get; init; }

    public required string BuyerName { get; init; }
    public required IReadOnlyList<string> BuyerAddress { get; init; }
    public string? BuyerContact { get; init; }

    public required string InvoiceNumber { get; init; }
    public required DateOnly InvoiceDate { get; init; }
    public required string Incoterm { get; init; }
    public required string Currency { get; init; }
    public string? PaymentTerms { get; init; }
    public required string CountryOfOrigin { get; init; }
    public string? Notes { get; init; }

    public required IReadOnlyList<ProformaLine> Lines { get; init; }

    public required decimal TotalAmount { get; init; }
    public required decimal TotalNetWeightKg { get; init; }
    public required decimal TotalGrossWeightKg { get; init; }
    public required int TotalCartons { get; init; }
    public required decimal TotalVolumeM3 { get; init; }

    public required BankDetails Bank { get; init; }

    /// <summary>Combines a saved order, its computed figures and the company
    /// profile into a print-ready model. <paramref name="calculation"/> lines
    /// must line up with <paramref name="order"/>.Lines ordered by
    /// <see cref="OrderLine.LineNumber"/>.</summary>
    public static ProformaInvoiceModel From(
        Order order, OrderCalculation calculation, CompanyProfile company)
    {
        // Match OrderDocumentService: lines with a product, ordered by LineNumber,
        // paired positionally with calculation.Lines.
        var orderLines = order.Lines
            .Where(l => l.Product is not null)
            .OrderBy(l => l.LineNumber)
            .ToList();
        var lines = new List<ProformaLine>(orderLines.Count);

        for (var i = 0; i < orderLines.Count; i++)
        {
            var line = orderLines[i];
            var product = line.Product!;
            var amount = i < calculation.Lines.Count
                ? calculation.Lines[i].LineTotal
                : line.Quantity * line.UnitPrice;

            lines.Add(new ProformaLine(
                LineNumber: line.LineNumber,
                PartNumber: product.PartNumber,
                Description: product.Description,
                HsCode: product.HsCode,
                Quantity: line.Quantity,
                UnitPrice: line.UnitPrice,
                Amount: amount));
        }

        return new ProformaInvoiceModel
        {
            SellerName = company.Name,
            SellerAddress = company.AddressLines ?? [],
            SellerTaxId = company.TaxId,
            SellerPhone = company.Phone,
            SellerEmail = company.Email,
            SellerLogoPath = ResolveLogo(company.LogoPath),

            BuyerName = order.Customer?.Name ?? "",
            BuyerAddress = BuildBuyerAddress(order.Customer),
            BuyerContact = order.Customer?.ContactName,

            InvoiceNumber = order.OrderNumber,
            InvoiceDate = order.OrderDate,
            Incoterm = string.IsNullOrWhiteSpace(order.Incoterm) ? "—" : order.Incoterm,
            Currency = order.Currency,
            PaymentTerms = order.PaymentTerms,
            CountryOfOrigin = string.IsNullOrWhiteSpace(company.CountryOfOrigin)
                ? "Türkiye"
                : company.CountryOfOrigin,
            Notes = order.Notes,

            Lines = lines,

            TotalAmount = calculation.OrderTotal,
            TotalNetWeightKg = calculation.TotalNetWeightKg,
            TotalGrossWeightKg = calculation.TotalGrossWeightKg,
            TotalCartons = calculation.TotalCartons,
            TotalVolumeM3 = calculation.TotalVolumeM3,

            Bank = company.Bank ?? new BankDetails(),
        };
    }

    private static IReadOnlyList<string> BuildBuyerAddress(Customer? customer)
    {
        if (customer is null) return [];

        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(customer.AddressLine1)) lines.Add(customer.AddressLine1);
        if (!string.IsNullOrWhiteSpace(customer.AddressLine2)) lines.Add(customer.AddressLine2!);

        var cityLine = string.Join(" ", new[] { customer.PostalCode, customer.City }
            .Where(s => !string.IsNullOrWhiteSpace(s)));
        if (cityLine.Length > 0) lines.Add(cityLine);

        if (!string.IsNullOrWhiteSpace(customer.Country)) lines.Add(customer.Country);
        return lines;
    }

    private static string? ResolveLogo(string? logoPath)
    {
        if (string.IsNullOrWhiteSpace(logoPath)) return null;
        var full = Path.IsPathRooted(logoPath)
            ? logoPath
            : Path.Combine(AppContext.BaseDirectory, logoPath);
        return File.Exists(full) ? full : null;
    }
}

/// <summary>One line on the proforma invoice.</summary>
public sealed record ProformaLine(
    int LineNumber,
    string PartNumber,
    string Description,
    string? HsCode,
    int Quantity,
    decimal UnitPrice,
    decimal Amount);
