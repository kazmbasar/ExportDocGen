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
    public string TaxId { get; set; } = "«tax id»";
    public string Phone { get; set; } = "«phone»";
    public string Email { get; set; } = "«email»";

    /// <summary>Shown as "Country of origin" on export documents.</summary>
    public string CountryOfOrigin { get; set; } = "Türkiye";

    public BankDetails Bank { get; set; } = new();

    /// <summary>Path relative to the app root, e.g. "wwwroot/company-logo.png".</summary>
    public string? LogoPath { get; set; }
}

public class BankDetails
{
    public string BeneficiaryName { get; set; } = "";
    public string Iban { get; set; } = "";
    public string Swift { get; set; } = "";
    public string BankName { get; set; } = "";
}
