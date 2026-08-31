using ExportDocGen.Data;
using ExportDocGen.Data.Entities;
using ExportDocGen.Services;

namespace ExportDocGen.Documents;

/// <summary>Flat, self-contained data for one proforma invoice — everything the
/// <see cref="ProformaInvoiceDocument"/> needs, with no database or config
/// lookups left to do. Build it with <see cref="From"/>.</summary>
public sealed record ProformaInvoiceModel
{
    // Seller — used for the bank block and (when there is no letterhead) the
    // fallback text header/footer.
    public required string SellerName { get; init; }
    public IReadOnlyList<string> SellerAddress { get; init; } = [];
    public string? SellerContactLine { get; init; }

    /// <summary>Full-page A4 background (company letterhead). When present the
    /// document draws no header/footer of its own.</summary>
    public byte[]? Letterhead { get; init; }

    /// <summary>Standalone logo bytes for the fallback header only.</summary>
    public byte[]? Logo { get; init; }

    // Buyer
    public required string BuyerName { get; init; }
    public string? BuyerTaxId { get; init; }
    public string BuyerAddress { get; init; } = "";
    public string? BuyerPhone { get; init; }
    public string? BuyerFax { get; init; }
    public string? BuyerEmail { get; init; }

    // Invoice
    public required string InvoiceNumber { get; init; }
    public required DateOnly InvoiceDate { get; init; }
    public required string Incoterm { get; init; }
    public required string Currency { get; init; }
    public string? PaymentTerms { get; init; }
    public string? Notes { get; init; }

    public required IReadOnlyList<ProformaLine> Lines { get; init; }
    public required decimal TotalAmount { get; init; }

    public BankDetails Bank { get; init; } = new();

    /// <summary>Combines a saved order, its computed totals and the company
    /// profile into a print-ready model. <paramref name="calculation"/> lines
    /// must correspond to <paramref name="order"/>.Lines with a product,
    /// ordered by <see cref="OrderLine.LineNumber"/>.</summary>
    public static ProformaInvoiceModel From(
        Order order,
        OrderCalculation calculation,
        CompanyProfile company,
        byte[]? letterhead = null,
        byte[]? logo = null)
    {
        var orderLines = order.Lines
            .Where(l => l.Product is not null)
            .OrderBy(l => l.LineNumber)
            .ToList();

        var lines = new List<ProformaLine>(orderLines.Count);
        for (var i = 0; i < orderLines.Count; i++)
        {
            var line = orderLines[i];
            var amount = i < calculation.Lines.Count
                ? calculation.Lines[i].LineTotal
                : line.Quantity * line.UnitPrice;
            lines.Add(new ProformaLine(line.Product!.PartNumber, line.Quantity, line.UnitPrice, amount));
        }

        var customer = order.Customer;

        return new ProformaInvoiceModel
        {
            SellerName = company.Name,
            SellerAddress = company.AddressLines ?? [],
            SellerContactLine = BuildContactLine(company),
            Letterhead = letterhead,
            Logo = logo,

            BuyerName = customer?.Name ?? "",
            BuyerTaxId = customer?.TaxId,
            BuyerAddress = BuildBuyerAddress(customer),
            BuyerPhone = customer?.ContactPhone,
            BuyerEmail = customer?.ContactEmail,

            InvoiceNumber = order.OrderNumber,
            InvoiceDate = order.OrderDate,
            Incoterm = string.IsNullOrWhiteSpace(order.Incoterm) ? "-" : order.Incoterm,
            Currency = order.Currency,
            PaymentTerms = order.PaymentTerms,
            Notes = order.Notes,

            Lines = lines,
            TotalAmount = calculation.OrderTotal,

            Bank = company.Bank ?? new BankDetails(),
        };
    }

    private static string BuildBuyerAddress(Customer? customer)
    {
        if (customer is null) return "";

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(customer.AddressLine1)) parts.Add(customer.AddressLine1.Trim());
        if (!string.IsNullOrWhiteSpace(customer.AddressLine2)) parts.Add(customer.AddressLine2!.Trim());

        var cityLine = string.Join(" ", new[] { customer.PostalCode, customer.City }
            .Where(s => !string.IsNullOrWhiteSpace(s)));
        if (cityLine.Length > 0) parts.Add(cityLine);

        if (!string.IsNullOrWhiteSpace(customer.Country)) parts.Add(customer.Country.Trim());
        return string.Join(", ", parts);
    }

    private static string? BuildContactLine(CompanyProfile company)
    {
        var bits = new List<string>();
        if (!string.IsNullOrWhiteSpace(company.Phone)) bits.Add($"Tel: {company.Phone}");
        if (!string.IsNullOrWhiteSpace(company.Fax)) bits.Add($"Faks: {company.Fax}");
        if (!string.IsNullOrWhiteSpace(company.Email)) bits.Add(company.Email);
        if (!string.IsNullOrWhiteSpace(company.Website)) bits.Add(company.Website);
        return bits.Count > 0 ? string.Join("   -   ", bits) : null;
    }
}

/// <summary>One line on the proforma invoice — matches the columns of the
/// company's template: code, quantity, unit price, extended total.</summary>
public sealed record ProformaLine(string Code, int Quantity, decimal UnitPrice, decimal Amount);
