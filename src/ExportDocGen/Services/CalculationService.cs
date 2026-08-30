using ExportDocGen.Data.Entities;

namespace ExportDocGen.Services;

/// <summary>
/// Pure calculation of the money, weight, carton and volume figures that appear
/// on the order screen and on the generated documents. No database access.
/// Formulas and rounding are documented in <c>docs/DATA-MODEL.md</c>.
/// </summary>
public class CalculationService
{
    private const int MoneyDecimals = 2;
    private const int WeightDecimals = 3;
    private const int VolumeDecimals = 3;

    public LineCalculation CalculateLine(int quantity, decimal unitPrice, Product product)
    {
        var unitsPerCarton = product.UnitsPerCarton > 0 ? product.UnitsPerCarton : 1;
        var cartons = quantity <= 0 ? 0 : (quantity + unitsPerCarton - 1) / unitsPerCarton;

        var lineTotal = Round(quantity * unitPrice, MoneyDecimals);
        var netWeight = Round(quantity * product.NetWeightKg, WeightDecimals);
        var cartonTare = cartons * product.CartonTareWeightKg;
        var shipGross = Round(quantity * product.GrossWeightKg + cartonTare, WeightDecimals);

        var volume = Round(
            cartons
            * (product.CartonLengthCm / 100m)
            * (product.CartonWidthCm / 100m)
            * (product.CartonHeightCm / 100m),
            VolumeDecimals);

        return new LineCalculation(quantity, lineTotal, netWeight, shipGross, cartons, volume);
    }

    public OrderCalculation CalculateOrder(
        IEnumerable<(int Quantity, decimal UnitPrice, Product Product)> lines)
    {
        var results = lines
            .Select(l => CalculateLine(l.Quantity, l.UnitPrice, l.Product))
            .ToList();

        return new OrderCalculation(
            OrderTotal: results.Sum(r => r.LineTotal),
            TotalNetWeightKg: results.Sum(r => r.NetWeightKg),
            TotalGrossWeightKg: results.Sum(r => r.ShipGrossWeightKg),
            TotalCartons: results.Sum(r => r.Cartons),
            TotalVolumeM3: Round(results.Sum(r => r.VolumeM3), VolumeDecimals),
            Lines: results);
    }

    private static decimal Round(decimal value, int decimals) =>
        Math.Round(value, decimals, MidpointRounding.AwayFromZero);
}

/// <param name="Quantity">Units on the line.</param>
/// <param name="LineTotal">quantity × unit price (money).</param>
/// <param name="NetWeightKg">quantity × product net weight.</param>
/// <param name="ShipGrossWeightKg">quantity × product gross weight + carton tare.</param>
/// <param name="Cartons">whole cartons, partial rounded up.</param>
/// <param name="VolumeM3">carton outer volume in cubic metres (CBM).</param>
public record LineCalculation(
    int Quantity,
    decimal LineTotal,
    decimal NetWeightKg,
    decimal ShipGrossWeightKg,
    int Cartons,
    decimal VolumeM3);

public record OrderCalculation(
    decimal OrderTotal,
    decimal TotalNetWeightKg,
    decimal TotalGrossWeightKg,
    int TotalCartons,
    decimal TotalVolumeM3,
    IReadOnlyList<LineCalculation> Lines);
