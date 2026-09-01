using ExportDocGen.Data.Entities;

namespace ExportDocGen.Services;

/// <summary>
/// Pure calculation of the money, weight and volume figures shown on the order
/// screen and the generated documents. No database access. Every figure is a
/// simple per-unit multiplication — the stock catalogue carries per-unit net
/// weight and per-unit volume (m³); gross weight is net × 1.05 (set on import).
/// </summary>
public class CalculationService
{
    private const int MoneyDecimals = 2;
    private const int WeightDecimals = 3;
    private const int VolumeDecimals = 6;

    public LineCalculation CalculateLine(int quantity, decimal unitPrice, Product product)
    {
        var q = quantity <= 0 ? 0 : quantity;

        var lineTotal = Round(q * unitPrice, MoneyDecimals);
        var netWeight = Round(q * product.NetWeightKg, WeightDecimals);
        var grossWeight = Round(q * product.GrossWeightKg, WeightDecimals);
        var volume = Round(q * product.UnitVolumeM3, VolumeDecimals);

        return new LineCalculation(q, lineTotal, netWeight, grossWeight, volume);
    }

    public OrderCalculation CalculateOrder(
        IEnumerable<(int Quantity, decimal UnitPrice, Product Product)> lines)
    {
        var results = lines
            .Select(l => CalculateLine(l.Quantity, l.UnitPrice, l.Product))
            .ToList();

        return new OrderCalculation(
            OrderTotal: results.Sum(r => r.LineTotal),
            TotalNetWeightKg: Round(results.Sum(r => r.NetWeightKg), WeightDecimals),
            TotalGrossWeightKg: Round(results.Sum(r => r.ShipGrossWeightKg), WeightDecimals),
            TotalVolumeM3: Round(results.Sum(r => r.VolumeM3), VolumeDecimals),
            Lines: results);
    }

    private static decimal Round(decimal value, int decimals) =>
        Math.Round(value, decimals, MidpointRounding.AwayFromZero);
}

/// <param name="Quantity">Units on the line.</param>
/// <param name="LineTotal">quantity × unit price (money).</param>
/// <param name="NetWeightKg">quantity × product net weight.</param>
/// <param name="ShipGrossWeightKg">quantity × product gross weight.</param>
/// <param name="VolumeM3">quantity × product unit volume (CBM).</param>
public record LineCalculation(
    int Quantity,
    decimal LineTotal,
    decimal NetWeightKg,
    decimal ShipGrossWeightKg,
    decimal VolumeM3);

public record OrderCalculation(
    decimal OrderTotal,
    decimal TotalNetWeightKg,
    decimal TotalGrossWeightKg,
    decimal TotalVolumeM3,
    IReadOnlyList<LineCalculation> Lines);
