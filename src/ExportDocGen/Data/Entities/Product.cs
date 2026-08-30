namespace ExportDocGen.Data.Entities;

/// <summary>A filter in the catalog that can be added to an order.</summary>
public class Product
{
    public int Id { get; set; }

    public string PartNumber { get; set; } = "";
    public string Description { get; set; } = "";

    /// <summary>Harmonized System customs code.</summary>
    public string? HsCode { get; set; }

    /// <summary>air / oil / fuel / cabin — free text for now.</summary>
    public string? FilterType { get; set; }

    // Per-unit weights (kg).
    public decimal NetWeightKg { get; set; }
    public decimal GrossWeightKg { get; set; }

    // Outer carton packing.
    public int UnitsPerCarton { get; set; } = 1;
    public decimal CartonLengthCm { get; set; }
    public decimal CartonWidthCm { get; set; }
    public decimal CartonHeightCm { get; set; }

    /// <summary>Empty carton weight (kg), added to shipped gross weight.</summary>
    public decimal CartonTareWeightKg { get; set; }

    /// <summary>Optional catalog price; pre-fills an order line.</summary>
    public decimal? DefaultUnitPrice { get; set; }

    /// <summary>Discontinued parts are hidden from the order builder.</summary>
    public bool IsActive { get; set; } = true;
}
