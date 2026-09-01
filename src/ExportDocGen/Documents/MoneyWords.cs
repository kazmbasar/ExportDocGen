using System.Globalization;
using Humanizer;

namespace ExportDocGen.Documents;

/// <summary>Spells a money amount for the "amount in words" line some proforma
/// templates carry, e.g. 11904.00 USD →
/// "ELEVEN THOUSAND NINE HUNDRED FOUR UNITED STATES DOLLARS ONLY".</summary>
public static class MoneyWords
{
    private static readonly CultureInfo Words = CultureInfo.GetCultureInfo("en");

    public static string Of(decimal amount, string currency)
    {
        var whole = decimal.Truncate(Math.Abs(amount));
        var cents = (int)Math.Round((Math.Abs(amount) - whole) * 100m, MidpointRounding.AwayFromZero);

        var text = ((long)whole).ToWords(Words).ToUpperInvariant();
        if (cents > 0)
            text += $" AND {cents:00}/100";

        return $"{text} {CurrencyName(currency)} ONLY".Replace("  ", " ");
    }

    private static string CurrencyName(string currency) => currency.ToUpperInvariant() switch
    {
        "USD" => "UNITED STATES DOLLARS",
        "EUR" => "EURO",
        "GBP" => "POUNDS STERLING",
        "TRY" => "TURKISH LIRA",
        var code => code,
    };
}
