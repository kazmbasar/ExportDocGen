namespace ExportDocGen.Data.Entities;

/// <summary>A buyer that export orders are issued to.</summary>
public class Customer
{
    public int Id { get; set; }

    public string Name { get; set; } = "";
    public string AddressLine1 { get; set; } = "";
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? PostalCode { get; set; }
    public string Country { get; set; } = "";

    /// <summary>e.g. "FOB Istanbul", "CIF Hamburg". Pre-fills new orders.</summary>
    public string? DefaultIncoterm { get; set; }

    /// <summary>ISO 4217 code, e.g. "USD", "EUR". Pre-fills new orders.</summary>
    public string DefaultCurrency { get; set; } = "USD";

    public string? ContactName { get; set; }
    public string? ContactEmail { get; set; }

    public List<Order> Orders { get; set; } = [];
}
