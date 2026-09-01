using ExportDocGen.Data.Entities;
using ExportDocGen.Services;

namespace ExportDocGen.Documents;

/// <summary>Flat, print-ready data for one commercial invoice — the document
/// that travels with the shipment. Combines the proforma's line/price columns
/// with the packing list's weight totals. Build it with <see cref="From"/>.</summary>
public sealed record CommercialInvoiceModel
{
    public required string SellerName { get; init; }
    public required ProformaTemplate Template { get; init; }
    public string CountryOfOrigin { get; init; } = "Türkiye";
    public byte[]? Letterhead { get; init; }

    public required string BuyerName { get; init; }
    public string BuyerAddress { get; init; } = "";
    public string? BuyerPhone { get; init; }
    public string? BuyerEmail { get; init; }
    public string? BuyerTaxId { get; init; }

    public required string InvoiceNumber { get; init; }
    public required DateOnly InvoiceDate { get; init; }
    public required string Currency { get; init; }
    public required string Incoterm { get; init; }
    public string? PaymentTerms { get; init; }
    public string? BankDetailsText { get; init; }
    public string? Notes { get; init; }

    public required IReadOnlyList<CommercialLine> Lines { get; init; }

    public required decimal TotalAmount { get; init; }
    public required int TotalQuantity { get; init; }
    public required decimal TotalNetWeightKg { get; init; }
    public required decimal TotalGrossWeightKg { get; init; }
    public required decimal TotalVolumeM3 { get; init; }
    public int? Pallets { get; init; }

    public string TotalVolumeText => Pallets is { } p
        ? $"{p} PALLET{(p == 1 ? "" : "S")}"
        : $"{DocFormat.Weight(TotalVolumeM3)} CBM";

    /// <summary><paramref name="calculation"/> lines must correspond to
    /// <paramref name="order"/>.Lines with a product, ordered by
    /// <see cref="OrderLine.LineNumber"/> — same contract as
    /// <see cref="ProformaInvoiceModel.From"/>.</summary>
    public static CommercialInvoiceModel From(
        Order order,
        OrderCalculation calculation,
        SellerCompany seller,
        byte[]? letterhead = null)
    {
        var orderLines = order.Lines
            .Where(l => l.Product is not null)
            .OrderBy(l => l.LineNumber)
            .ToList();

        var lines = new List<CommercialLine>(orderLines.Count);
        for (var i = 0; i < orderLines.Count; i++)
        {
            var line = orderLines[i];
            var amount = i < calculation.Lines.Count
                ? calculation.Lines[i].LineTotal
                : line.Quantity * line.UnitPrice;
            lines.Add(new CommercialLine(
                No: i + 1,
                Code: line.Product!.PartNumber,
                Description: DocFormat.FilterDescription(line.Product!),
                HsCode: line.Product!.HsCode,
                Brand: line.Product!.Brand,
                Origin: line.Product!.Origin,
                Quantity: line.Quantity,
                UnitPrice: line.UnitPrice,
                Amount: amount));
        }

        var customer = order.Customer;

        return new CommercialInvoiceModel
        {
            SellerName = seller.Name,
            Template = seller.ProformaTemplate,
            CountryOfOrigin = seller.CountryOfOrigin,
            Letterhead = letterhead,

            BuyerName = customer?.Name ?? "",
            BuyerAddress = DocFormat.BuyerAddress(customer),
            BuyerPhone = customer?.ContactPhone,
            BuyerEmail = customer?.ContactEmail,
            BuyerTaxId = customer?.TaxId,

            InvoiceNumber = string.IsNullOrWhiteSpace(order.InvoiceNumber) ? order.OrderNumber : order.InvoiceNumber,
            InvoiceDate = order.InvoiceDate ?? order.OrderDate,
            Currency = order.Currency,
            Incoterm = string.IsNullOrWhiteSpace(order.Incoterm) ? "-" : order.Incoterm,
            PaymentTerms = order.PaymentTerms,
            BankDetailsText = string.IsNullOrWhiteSpace(order.BankDetails)
                ? seller.DefaultBankDetails
                : order.BankDetails,
            Notes = order.Notes,

            Lines = lines,
            TotalAmount = calculation.OrderTotal,
            TotalQuantity = lines.Sum(l => l.Quantity),
            TotalNetWeightKg = calculation.TotalNetWeightKg,
            TotalGrossWeightKg = calculation.TotalGrossWeightKg,
            TotalVolumeM3 = calculation.TotalVolumeM3,
            Pallets = order.Pallets,
        };
    }
}

/// <summary>One line on the commercial invoice.</summary>
public sealed record CommercialLine(
    int No,
    string Code,
    string Description,
    string? HsCode,
    string? Brand,
    string? Origin,
    int Quantity,
    decimal UnitPrice,
    decimal Amount);
