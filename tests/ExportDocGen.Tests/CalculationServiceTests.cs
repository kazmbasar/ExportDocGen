using ExportDocGen.Data.Entities;
using ExportDocGen.Services;

namespace ExportDocGen.Tests;

public class CalculationServiceTests
{
    private readonly CalculationService _calc = new();

    private static Product ProductP() => new()
    {
        PartNumber = "P", Description = "P",
        NetWeightKg = 1.0m, GrossWeightKg = 1.2m,
        UnitsPerCarton = 10, CartonTareWeightKg = 0.5m,
        CartonLengthCm = 50, CartonWidthCm = 40, CartonHeightCm = 30,
    };

    private static Product ProductQ() => new()
    {
        PartNumber = "Q", Description = "Q",
        NetWeightKg = 0.5m, GrossWeightKg = 0.6m,
        UnitsPerCarton = 20, CartonTareWeightKg = 0.4m,
        CartonLengthCm = 40, CartonWidthCm = 30, CartonHeightCm = 20,
    };

    [Fact]
    public void Line_totals_match_the_documented_formulas()
    {
        var r = _calc.CalculateLine(quantity: 25, unitPrice: 4.00m, ProductP());

        Assert.Equal(100.00m, r.LineTotal);
        Assert.Equal(25.000m, r.NetWeightKg);
        Assert.Equal(3, r.Cartons);                 // ceil(25 / 10)
        Assert.Equal(31.500m, r.ShipGrossWeightKg); // 25*1.2 + 3*0.5
        Assert.Equal(0.180m, r.VolumeM3);           // 3 * 0.5 * 0.4 * 0.3
    }

    [Theory]
    [InlineData(20, 20, 1)]  // exact multiple -> not rounded up to 2
    [InlineData(21, 20, 2)]  // one over -> next carton
    [InlineData(1, 20, 1)]
    [InlineData(0, 20, 0)]
    public void Cartons_round_up_to_whole_cartons(int quantity, int unitsPerCarton, int expected)
    {
        var product = ProductP();
        product.UnitsPerCarton = unitsPerCarton;

        Assert.Equal(expected, _calc.CalculateLine(quantity, 1m, product).Cartons);
    }

    [Fact]
    public void UnitsPerCarton_of_zero_is_treated_as_one()
    {
        var product = ProductP();
        product.UnitsPerCarton = 0;

        Assert.Equal(7, _calc.CalculateLine(7, 1m, product).Cartons);
    }

    [Fact]
    public void Money_rounds_half_away_from_zero_to_two_decimals()
    {
        var product = ProductP();
        var r = _calc.CalculateLine(quantity: 1, unitPrice: 0.125m, product);

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

        Assert.Equal(160.00m, order.OrderTotal);
        Assert.Equal(35.000m, order.TotalNetWeightKg);
        Assert.Equal(43.900m, order.TotalGrossWeightKg); // 31.500 + 12.400
        Assert.Equal(4, order.TotalCartons);             // 3 + 1
        Assert.Equal(0.204m, order.TotalVolumeM3);       // 0.180 + 0.024
        Assert.Equal(2, order.Lines.Count);
    }

    [Fact]
    public void Empty_order_is_all_zero()
    {
        var order = _calc.CalculateOrder([]);

        Assert.Equal(0m, order.OrderTotal);
        Assert.Equal(0m, order.TotalNetWeightKg);
        Assert.Equal(0m, order.TotalGrossWeightKg);
        Assert.Equal(0, order.TotalCartons);
        Assert.Equal(0m, order.TotalVolumeM3);
        Assert.Empty(order.Lines);
    }
}
