using ExportDocGen.Data.Entities;
using ExportDocGen.Services;

namespace ExportDocGen.Documents;

/// <summary>Flat, print-ready data for one packing list — everything
/// <see cref="PackingListDocument"/> needs, no database or config lookups left.
/// Mirrors <see cref="ProformaInvoiceModel"/>; build it with <see cref="From"/>.</summary>
public sealed record PackingListModel
{
    public required string SellerName { get; init; }
    public required string SellerShortName { get; init; }
    public required ProformaTemplate Template { get; init; }
    public string CountryOfOrigin { get; init; } = "Türkiye";
    public byte[]? Letterhead { get; init; }

    public required string BuyerName { get; init; }
    public string BuyerAddress { get; init; } = "";

    /// <summary>Order number — shown as both the proforma and invoice reference
    /// until those are separate numbers.</summary>
    public required string Reference { get; init; }
    public required DateOnly Date { get; init; }
    public required string Incoterm { get; init; }
    public string? Notes { get; init; }

    public required IReadOnlyList<PackingLine> Lines { get; init; }

    public required int TotalQuantity { get; init; }
    public required decimal TotalNetWeightKg { get; init; }
    public required decimal TotalGrossWeightKg { get; init; }
    public required decimal TotalVolumeM3 { get; init; }

    /// <summary><paramref name="calculation"/> lines must correspond to
    /// <paramref name="order"/>.Lines with a product, ordered by
    /// <see cref="OrderLine.LineNumber"/> — same contract as
    /// <see cref="ProformaInvoiceModel.From"/>.</summary>
    public static PackingListModel From(
        Order order,
        OrderCalculation calculation,
        SellerCompany seller,
        byte[]? letterhead = null)
    {
        var orderLines = order.Lines
            .Where(l => l.Product is not null)
            .OrderBy(l => l.LineNumber)
            .ToList();

        var lines = new List<PackingLine>(orderLines.Count);
        for (var i = 0; i < orderLines.Count; i++)
        {
            var line = orderLines[i];
            var calc = i < calculation.Lines.Count ? calculation.Lines[i] : null;
            lines.Add(new PackingLine(
                No: i + 1,
                Code: line.Product!.PartNumber,
                Description: DocFormat.FilterDescription(line.Product!),
                HsCode: line.Product!.HsCode,
                Origin: line.Product!.Origin,
                Quantity: line.Quantity,
                NetWeightKg: calc?.NetWeightKg ?? line.Quantity * line.Product!.NetWeightKg,
                GrossWeightKg: calc?.ShipGrossWeightKg ?? line.Quantity * line.Product!.GrossWeightKg,
                VolumeM3: calc?.VolumeM3 ?? line.Quantity * line.Product!.UnitVolumeM3));
        }

        return new PackingListModel
        {
            SellerName = seller.Name,
            SellerShortName = seller.ShortName,
            Template = seller.ProformaTemplate,
            CountryOfOrigin = seller.CountryOfOrigin,
            Letterhead = letterhead,

            BuyerName = order.Customer?.Name ?? "",
            BuyerAddress = DocFormat.BuyerAddress(order.Customer),

            Reference = order.OrderNumber,
            Date = order.OrderDate,
            Incoterm = string.IsNullOrWhiteSpace(order.Incoterm) ? "-" : order.Incoterm,
            Notes = order.Notes,

            Lines = lines,
            TotalQuantity = lines.Sum(l => l.Quantity),
            TotalNetWeightKg = calculation.TotalNetWeightKg,
            TotalGrossWeightKg = calculation.TotalGrossWeightKg,
            TotalVolumeM3 = calculation.TotalVolumeM3,
        };
    }
}

/// <summary>One line on the packing list.</summary>
public sealed record PackingLine(
    int No,
    string Code,
    string Description,
    string? HsCode,
    string? Origin,
    int Quantity,
    decimal NetWeightKg,
    decimal GrossWeightKg,
    decimal VolumeM3);
