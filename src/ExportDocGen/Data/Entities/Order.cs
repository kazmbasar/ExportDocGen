namespace ExportDocGen.Data.Entities;

/// <summary>A single export order that documents are generated from.</summary>
public class Order
{
    public int Id { get; set; }

    /// <summary>Human-facing reference, e.g. "EXP-2026-0001".</summary>
    public string OrderNumber { get; set; } = "";

    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }

    /// <summary>The group company issuing this order — decides the proforma
    /// template, letterhead and number format.</summary>
    public int SellerCompanyId { get; set; }
    public SellerCompany? SellerCompany { get; set; }

    public DateOnly OrderDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    /// <summary>Copied from the customer default, editable per order.</summary>
    public string Incoterm { get; set; } = "";

    /// <summary>ISO 4217 code, copied from the customer default, editable.</summary>
    public string Currency { get; set; } = "USD";

    /// <summary>Payment terms text for the proforma. Pre-filled from the
    /// customer's <see cref="Customer.PaymentType"/>.</summary>
    public string? PaymentTerms { get; set; }

    /// <summary>Free-text bank block, typed per order and printed verbatim on the
    /// proforma (line breaks preserved). Pre-filled from the seller company's
    /// <see cref="SellerCompany.DefaultBankDetails"/>.</summary>
    public string? BankDetails { get; set; }

    /// <summary>Optional — shown as "DELIVERY TIME" on templates that have that
    /// row (İkiler). Pre-filled from the seller company default.</summary>
    public string? DeliveryTime { get; set; }

    /// <summary>Optional — shown as "VALIDITY" on templates that have that row
    /// (İkiler). Pre-filled from the seller company default.</summary>
    public string? Validity { get; set; }

    /// <summary>Free text shown on the proforma invoice.</summary>
    public string? Notes { get; set; }

    public DateTime CreatedUtc { get; set; }

    public List<OrderLine> Lines { get; set; } = [];
}
