namespace ExportDocGen.Data.Entities;

/// <summary>Which proforma-invoice layout a seller company uses.</summary>
public enum ProformaTemplate
{
    /// <summary>Filtorq: full-page letterhead background, 4-column line table,
    /// gold grand-total box (the M6b layout).</summary>
    FiltorqClassic,

    /// <summary>İkiler: drawn header, 5-column bordered grid with an inline
    /// totals row and an amount-in-words line.</summary>
    IkilerGrid,
}

/// <summary>How a seller company's order / proforma numbers are formed.</summary>
public enum SellerNumberFormat
{
    /// <summary>"EXP-{year}-{seq:0000}", sequence per company per year.</summary>
    ExpYearSeq,

    /// <summary>"{yyMMdd}/{seq}", sequence per company per day.</summary>
    DateSlashSeq,
}

/// <summary>One of the group's exporting companies. The seller is chosen per
/// order and decides the proforma template, letterhead and default bank text.
/// Replaces the former single <c>CompanyProfile</c> configuration.</summary>
public class SellerCompany
{
    public int Id { get; set; }

    /// <summary>Full legal name — shown in the bank block and PDF metadata.</summary>
    public string Name { get; set; } = "";

    /// <summary>Short label for the order-form picker, e.g. "Filtorq".</summary>
    public string ShortName { get; set; } = "";

    public ProformaTemplate ProformaTemplate { get; set; } = ProformaTemplate.FiltorqClassic;

    public SellerNumberFormat NumberFormat { get; set; } = SellerNumberFormat.ExpYearSeq;

    /// <summary>Letterhead asset path relative to the content root
    /// (e.g. "wwwroot/proforma-letterhead.png"). Null → plain text header.</summary>
    public string? LetterheadPath { get; set; }

    /// <summary>Multi-line bank block text. Pre-fills <see cref="Order.BankDetails"/>
    /// on a new order; the proforma prints whichever text the order carries
    /// verbatim.</summary>
    public string? DefaultBankDetails { get; set; }

    /// <summary>Pre-fills <see cref="Order.DeliveryTime"/>, e.g. "6 WEEKS".</summary>
    public string? DefaultDeliveryTime { get; set; }

    /// <summary>Pre-fills <see cref="Order.Validity"/>,
    /// e.g. "2 WEEKS FROM PROFORMA DATE".</summary>
    public string? DefaultValidity { get; set; }

    /// <summary>Shown as "Country of origin" on export documents.</summary>
    public string CountryOfOrigin { get; set; } = "Türkiye";

    public bool IsActive { get; set; } = true;

    public List<Order> Orders { get; set; } = [];
}
