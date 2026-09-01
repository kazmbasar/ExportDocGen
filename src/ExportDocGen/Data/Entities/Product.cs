namespace ExportDocGen.Data.Entities;

/// <summary>A filter in the catalog that can be added to an order. Fields come
/// from the company's stock database (see <c>StockCatalogImportService</c>).</summary>
public class Product
{
    public int Id { get; set; }

    /// <summary>Stock code — the "Description" column of the stock database.</summary>
    public string PartNumber { get; set; } = "";

    /// <summary>What it is — the "CİNSİ" column, e.g. "AIR FILTER".</summary>
    public string Description { get; set; } = "";

    /// <summary>Country of manufacture — the "MENŞEİ" column.</summary>
    public string? Origin { get; set; }

    /// <summary>Brand — the "MARKA" column, e.g. "FLEETGUARD".</summary>
    public string? Brand { get; set; }

    /// <summary>Turkish customs code (GTİP), e.g. "8421.23.00.00.00".</summary>
    public string? HsCode { get; set; }

    /// <summary>air / oil / fuel / cabin / water — derived from the description;
    /// drives the "&lt;TYPE&gt; FILTER" label on the proforma.</summary>
    public string? FilterType { get; set; }

    /// <summary>Net weight per unit (kg).</summary>
    public decimal NetWeightKg { get; set; }

    /// <summary>Gross weight per unit (kg). Not in the stock file — set to
    /// net × 1.05 on import.</summary>
    public decimal GrossWeightKg { get; set; }

    /// <summary>Volume per unit (m³) — the "m3" column of the stock database.</summary>
    public decimal UnitVolumeM3 { get; set; }

    /// <summary>Optional catalog price; pre-fills an order line.</summary>
    public decimal? DefaultUnitPrice { get; set; }

    /// <summary>Discontinued parts are hidden from the order builder.</summary>
    public bool IsActive { get; set; } = true;
}
