using ExportDocGen.Data.Entities;
using ExportDocGen.Services;

namespace ExportDocGen.Tests;

public class CalculationServiceTests
{
    private readonly CalculationService _calc = new();

    private static Product ProductP() => new()
    {
        PartNumber = "P", Description = "P",
        NetWeightKg = 1.0m, GrossWeightKg = 1.05m, UnitVolumeM3 = 0.006m,
    };

    private static Product ProductQ() => new()
    {
        PartNumber = "Q", Description = "Q",
        NetWeightKg = 0.5m, GrossWeightKg = 0.525m, UnitVolumeM3 = 0.002m,
    };

    [Fact]
    public void Line_totals_are_quantity_times_the_per_unit_figures()
    {
        var r = _calc.CalculateLine(quantity: 25, unitPrice: 4.00m, ProductP());

        Assert.Equal(100.00m, r.LineTotal);          // 25 * 4
        Assert.Equal(25.000m, r.NetWeightKg);        // 25 * 1.0
        Assert.Equal(26.250m, r.ShipGrossWeightKg);  // 25 * 1.05
        Assert.Equal(0.150000m, r.VolumeM3);         // 25 * 0.006
    }

    [Fact]
    public void Non_positive_quantity_is_zero()
    {
        var r = _calc.CalculateLine(quantity: 0, unitPrice: 5m, ProductP());

        Assert.Equal(0, r.Quantity);
        Assert.Equal(0m, r.NetWeightKg);
        Assert.Equal(0m, r.ShipGrossWeightKg);
        Assert.Equal(0m, r.VolumeM3);
    }

    [Fact]
    public void Money_rounds_half_away_from_zero_to_two_decimals()
    {
        var r = _calc.CalculateLine(quantity: 1, unitPrice: 0.125m, ProductP());

        Assert.Equal(0.13m, r.LineTotal);
    }

    [Fact]
    public void Order_totals_are_the_sum_of_the_lines()
    {
        var order = _calc.CalculateOrder(
        [
            (25, 4.00m, ProductP()),
            (20, 3.00m, ProductQ()),
        ]);

        Assert.Equal(160.00m, order.OrderTotal);            // 100 + 60
        Assert.Equal(35.000m, order.TotalNetWeightKg);      // 25 + 10
        Assert.Equal(36.750m, order.TotalGrossWeightKg);    // 26.25 + 10.5
        Assert.Equal(0.190000m, order.TotalVolumeM3);       // 0.15 + 0.04
        Assert.Equal(2, order.Lines.Count);
    }

    [Fact]
    public void Empty_order_is_all_zero()
    {
        var order = _calc.CalculateOrder([]);

        Assert.Equal(0m, order.OrderTotal);
        Assert.Equal(0m, order.TotalNetWeightKg);
        Assert.Equal(0m, order.TotalGrossWeightKg);
        Assert.Equal(0m, order.TotalVolumeM3);
        Assert.Empty(order.Lines);
    }
}
