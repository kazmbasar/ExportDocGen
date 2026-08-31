namespace ExportDocGen.Data;

/// <summary>
/// Company header details shown on generated documents. Bound from the
/// "CompanyProfile" section of appsettings.json — fill in the real values there.
/// </summary>
public class CompanyProfile
{
    public const string SectionName = "CompanyProfile";

    public string Name { get; set; } = "«Company name»";

    // Empty by default: the .NET configuration binder *appends* to a non-empty
    // array, so a default here would duplicate the appsettings.json values.
    public string[] AddressLines { get; set; } = [];
    public string TaxId { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Fax { get; set; } = "";
    public string Email { get; set; } = "";
    public string Website { get; set; } = "";

    /// <summary>Shown as "Country of origin" on export documents.</summary>
    public string CountryOfOrigin { get; set; } = "Türkiye";

    public BankDetails Bank { get; set; } = new();

    /// <summary>Full-page A4 background image for the proforma invoice (the
    /// company letterhead — logo, branding, footer). Path relative to the
    /// content root, e.g. "wwwroot/proforma-letterhead.png". When null the
    /// document falls back to a plain text header/footer.</summary>
    public string? LetterheadPath { get; set; }

    /// <summary>Optional standalone logo, used only by the fallback header when
    /// there is no <see cref="LetterheadPath"/>.</summary>
    public string? LogoPath { get; set; }
}

public class BankDetails
{
    public string BeneficiaryName { get; set; } = "";
    public string Iban { get; set; } = "";
    public string Swift { get; set; } = "";
    public string BankName { get; set; } = "";
}
