using System.Globalization;
using ExportDocGen.Data.Entities;

namespace ExportDocGen.Documents;

/// <summary>Formatting shared by the generated documents so the proforma and the
/// packing list read the same way (the company's own conventions, not invariant
/// English).</summary>
public static class DocFormat
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>Dates as the company writes them: <c>24.08.2026</c>.</summary>
    public static string Date(DateOnly value) => value.ToString("dd.MM.yyyy", Inv);

    /// <summary>Weights / volumes: up to 3 decimals, no thousands separator.</summary>
    public static string Weight(decimal value) => value.ToString("0.###", Inv);

    /// <summary>Whole counts (quantities, cartons).</summary>
    public static string Count(int value) => value.ToString("N0", Inv);

    /// <summary>Money the company's way: a currency-symbol prefix, comma
    /// decimals, and the given thousands separator (Filtorq uses a space —
    /// <c>$3 624,88</c>; İkiler a dot — <c>$11.904,00</c>).</summary>
    public static string Money(decimal value, string currency, string groupSeparator = " ")
    {
        var nfi = new NumberFormatInfo
        {
            NumberDecimalSeparator = ",",
            NumberGroupSeparator = groupSeparator,
            NumberDecimalDigits = 2,
        };
        var n = value.ToString("N2", nfi);
        return currency.ToUpperInvariant() switch
        {
            "USD" => $"${n}",
            "EUR" => $"€{n}",
            "GBP" => $"£{n}",
            "TRY" => $"₺{n}",
            var code => $"{n} {code}",
        };
    }

    /// <summary>Buyer address as one line, e.g.
    /// "Khoravs N4, 4600 Kutaisi, Georgia".</summary>
    public static string BuyerAddress(Customer? customer)
    {
        if (customer is null) return "";

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(customer.AddressLine1)) parts.Add(customer.AddressLine1.Trim());
        if (!string.IsNullOrWhiteSpace(customer.AddressLine2)) parts.Add(customer.AddressLine2!.Trim());

        var cityLine = string.Join(" ", new[] { customer.PostalCode, customer.City }
            .Where(s => !string.IsNullOrWhiteSpace(s)));
        if (cityLine.Length > 0) parts.Add(cityLine);

        if (!string.IsNullOrWhiteSpace(customer.Country)) parts.Add(customer.Country.Trim());
        return string.Join(", ", parts);
    }

    /// <summary>Line description as the documents show it — the filter category
    /// ("AIR FILTER"), falling back to the catalog description.</summary>
    public static string FilterDescription(Product product) =>
        string.IsNullOrWhiteSpace(product.FilterType)
            ? product.Description
            : $"{product.FilterType.Trim().ToUpperInvariant()} FILTER";
}
