namespace ExportDocGen.Data.Entities;

/// <summary>A single export order that documents are generated from.</summary>
public class Order
{
    public int Id { get; set; }

    /// <summary>Human-facing reference, e.g. "EXP-2026-0001".</summary>
    public string OrderNumber { get; set; } = "";

    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public DateOnly OrderDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    /// <summary>Copied from the customer default, editable per order.</summary>
    public string Incoterm { get; set; } = "";

    /// <summary>ISO 4217 code, copied from the customer default, editable.</summary>
    public string Currency { get; set; } = "USD";

    public string? PaymentTerms { get; set; }

    /// <summary>Free text shown on the proforma invoice.</summary>
    public string? Notes { get; set; }

    public DateTime CreatedUtc { get; set; }

    public List<OrderLine> Lines { get; set; } = [];
}
