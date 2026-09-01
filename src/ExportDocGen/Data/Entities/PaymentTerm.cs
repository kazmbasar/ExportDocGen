namespace ExportDocGen.Data.Entities;

/// <summary>Managed list of payment terms a customer can default to. Stored as
/// the enum name (string) so the list can be reordered without breaking data.
/// The proforma prints <see cref="PaymentTermText.Of"/>.</summary>
public enum PaymentTerm
{
    Prepayment100,
    Advance40Balance60,
    Advance50Balance50,
    CashAgainstDocuments,
    LetterOfCreditAtSight,
}

/// <summary>Human-readable text for a <see cref="PaymentTerm"/>.</summary>
public static class PaymentTermText
{
    public static string Of(PaymentTerm term) => term switch
    {
        PaymentTerm.Prepayment100 => "100% Prepayment",
        PaymentTerm.Advance40Balance60 => "40% advance, 60% against shipping documents",
        PaymentTerm.Advance50Balance50 => "50% advance, 50% against copy of B/L",
        PaymentTerm.CashAgainstDocuments => "Cash against documents (CAD)",
        PaymentTerm.LetterOfCreditAtSight => "L/C at sight",
        _ => term.ToString(),
    };

    public static string? Of(PaymentTerm? term) => term is { } t ? Of(t) : null;

    public static IReadOnlyList<PaymentTerm> All { get; } = Enum.GetValues<PaymentTerm>();
}
