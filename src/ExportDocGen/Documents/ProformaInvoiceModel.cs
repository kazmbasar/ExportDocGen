using ExportDocGen.Data.Entities;
using ExportDocGen.Services;

namespace ExportDocGen.Documents;

/// <summary>Flat, self-contained data for one proforma invoice — everything a
/// proforma document needs, with no database or configuration lookups left to
/// do. Build it with <see cref="From"/>.</summary>
public sealed record ProformaInvoiceModel
{
    // Seller
    public required string SellerName { get; init; }
    public required string SellerShortName { get; init; }
    public required ProformaTemplate Template { get; init; }
    public string CountryOfOrigin { get; init; } = "Türkiye";

    /// <summary>Full-page A4 background (company letterhead). When present the
    /// Filtorq template draws no header/footer of its own.</summary>
    public byte[]? Letterhead { get; init; }

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
    public string? DeliveryTime { get; init; }
    public string? Validity { get; init; }
    public string? Notes { get; init; }

    public required IReadOnlyList<ProformaLine> Lines { get; init; }
    public required decimal TotalAmount { get; init; }
    public required int TotalQuantity { get; init; }

    /// <summary>Grand total spelled out, e.g. "ELEVEN THOUSAND … DOLLARS ONLY".</summary>
    public required string AmountInWords { get; init; }

    /// <summary>Bank block, printed verbatim (line breaks preserved). Comes from
    /// the order, falling back to the seller company's default text.</summary>
    public string? BankDetailsText { get; init; }

    /// <summary>Combines a saved order, its computed totals and the issuing
    /// seller company into a print-ready model. <paramref name="calculation"/>
    /// lines must correspond to <paramref name="order"/>.Lines with a product,
    /// ordered by <see cref="OrderLine.LineNumber"/>.</summary>
    public static ProformaInvoiceModel From(
        Order order,
        OrderCalculation calculation,
        SellerCompany seller,
        byte[]? letterhead = null)
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
            lines.Add(new ProformaLine(
                line.Product!.PartNumber,
                Describe(line.Product!),
                line.Quantity,
                line.UnitPrice,
                amount));
        }

        var customer = order.Customer;

        return new ProformaInvoiceModel
        {
            SellerName = seller.Name,
            SellerShortName = seller.ShortName,
            Template = seller.ProformaTemplate,
            CountryOfOrigin = seller.CountryOfOrigin,
            Letterhead = letterhead,

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
            DeliveryTime = order.DeliveryTime,
            Validity = order.Validity,
            Notes = order.Notes,

            Lines = lines,
            TotalAmount = calculation.OrderTotal,
            TotalQuantity = lines.Sum(l => l.Quantity),
            AmountInWords = MoneyWords.Of(calculation.OrderTotal, order.Currency),

            BankDetailsText = string.IsNullOrWhiteSpace(order.BankDetails)
                ? seller.DefaultBankDetails
                : order.BankDetails,
        };
    }

    /// <summary>Line description as the templates show it — the filter category
    /// ("AIR FILTER"), falling back to the catalog description.</summary>
    private static string Describe(Product product) =>
        string.IsNullOrWhiteSpace(product.FilterType)
            ? product.Description
            : $"{product.FilterType.Trim().ToUpperInvariant()} FILTER";

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
}

/// <summary>One line on the proforma invoice — code, filter description,
/// quantity, unit price, extended total. Templates use the columns they need.</summary>
public sealed record ProformaLine(string Code, string Description, int Quantity, decimal UnitPrice, decimal Amount);
