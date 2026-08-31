using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ExportDocGen.Documents;

/// <summary>Renders a <see cref="ProformaInvoiceModel"/> as an A4 proforma
/// invoice laid out like the company's own template: a full-page letterhead
/// background, the buyer / invoice box, delivery &amp; payment terms, the bank
/// block, then a page break and the line-items table with a highlighted grand
/// total. Pure layout — no database or configuration access.</summary>
public sealed class ProformaInvoiceDocument(ProformaInvoiceModel model) : IDocument
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    // "$3 624,88" — currency symbol, non-breaking-space groups, comma decimals,
    // matching the company's existing documents.
    private static readonly NumberFormatInfo Money = new()
    {
        NumberDecimalSeparator = ",",
        NumberGroupSeparator = " ",
        NumberDecimalDigits = 2,
    };

    private static readonly Color Ink = Color.FromHex("#1A1A1A");
    private static readonly Color Rule = Color.FromHex("#8A8A8A");
    private static readonly Color FaintRule = Color.FromHex("#CFCFCF");
    private static readonly Color HeadGreen = Color.FromHex("#5C8A2C");
    private static readonly Color Gold = Color.FromHex("#F2A900");

    private bool HasLetterhead => model.Letterhead is { Length: > 0 };

    private bool HasBank =>
        !string.IsNullOrWhiteSpace(model.Bank.Iban)
        || !string.IsNullOrWhiteSpace(model.Bank.Swift)
        || !string.IsNullOrWhiteSpace(model.Bank.BankName);

    public DocumentMetadata GetMetadata() => new()
    {
        Title = $"Proforma invoice {model.InvoiceNumber}",
        Author = model.SellerName,
    };

    public void Compose(IDocumentContainer container) => container.Page(page =>
    {
        page.Size(PageSizes.A4);
        page.DefaultTextStyle(t => t.FontSize(10).FontColor(Ink).LineHeight(1.2f));

        if (HasLetterhead)
        {
            // Clear the letterhead's header band (logo + tagline) and footer bar.
            page.MarginTop(4.6f, Unit.Centimetre);
            page.MarginBottom(2.7f, Unit.Centimetre);
            page.MarginHorizontal(1.9f, Unit.Centimetre);
            page.Background().Image(model.Letterhead!).FitArea();
        }
        else
        {
            page.Margin(1.8f, Unit.Centimetre);
            page.Header().Element(FallbackHeader);
            page.Footer().Element(FallbackFooter);
        }

        page.Content().Element(Body);
    });

    private void Body(IContainer container) => container.Column(col =>
    {
        col.Item().PaddingBottom(12).AlignCenter().Text("PROFORMA INVOICE").FontSize(22).Bold();

        col.Item().Element(BuyerBox);
        col.Item().PaddingTop(20).Element(TermsBlock);

        if (HasBank)
            col.Item().PaddingTop(20).Element(BankBlock);

        col.Item().PageBreak();
        col.Item().Element(LineItems);

        if (!string.IsNullOrWhiteSpace(model.Notes))
            col.Item().PaddingTop(14).Text(t =>
            {
                t.Span("Notes: ").SemiBold();
                t.Span(model.Notes);
            });
    });

    private void BuyerBox(IContainer container) => container.Table(table =>
    {
        table.ColumnsDefinition(c =>
        {
            c.ConstantColumn(74);
            c.RelativeColumn(3.2f);
            c.ConstantColumn(46);
            c.RelativeColumn(2f);
        });

        var taxSuffix = string.IsNullOrWhiteSpace(model.BuyerTaxId) ? "" : $" - {model.BuyerTaxId}";
        Row("Name:", model.BuyerName + taxSuffix, "Date:", model.InvoiceDate.ToString("dd.MM.yyyy", Inv));
        Row("P/I NO:", model.InvoiceNumber, "Tel :", model.BuyerPhone ?? "");
        Row("Email:", model.BuyerEmail ?? "", "Fax :", model.BuyerFax ?? "");
        Row("Address:", model.BuyerAddress, "", "");

        void Row(string l1, string v1, string l2, string v2)
        {
            Cell(l1, bold: true);
            Cell(v1, bold: false);
            Cell(l2, bold: true);
            Cell(v2, bold: false);
        }

        void Cell(string text, bool bold)
        {
            var c = table.Cell().Border(0.75f).BorderColor(Rule)
                .PaddingVertical(4).PaddingHorizontal(6);
            var t = c.Text(text).FontSize(9.5f);
            if (bold) t.SemiBold();
        }
    });

    private void TermsBlock(IContainer container) => container.PaddingHorizontal(24).Column(col =>
    {
        col.Spacing(5);
        Term("DELIVERY TERM", model.Incoterm);
        Term("PAYMENT", string.IsNullOrWhiteSpace(model.PaymentTerms) ? "-" : model.PaymentTerms!);

        void Term(string label, string value) => col.Item().Row(r =>
        {
            r.ConstantItem(150).Text(label).SemiBold();
            r.RelativeItem().Text(t =>
            {
                t.Span(": ").SemiBold();
                t.Span(value).SemiBold();
            });
        });
    });

    private void BankBlock(IContainer container) => container.Column(col =>
    {
        col.Spacing(6);
        col.Item().AlignCenter().PaddingBottom(2)
            .Text($"Bank Detail ({model.Currency}):").FontSize(11).Bold();

        Line("Company Name", string.IsNullOrWhiteSpace(model.Bank.BeneficiaryName)
            ? model.SellerName
            : model.Bank.BeneficiaryName);
        Line("Our Bank", model.Bank.BankName);
        Line("Swift Code", model.Bank.Swift);
        Line("IBAN NO", model.Bank.Iban);

        void Line(string label, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            col.Item().Text(t =>
            {
                t.Span($"{label} : ").SemiBold();
                t.Span(value).SemiBold();
            });
        }
    });

    private void LineItems(IContainer container) => container.Table(table =>
    {
        table.ColumnsDefinition(c =>
        {
            c.RelativeColumn(2.4f);
            c.RelativeColumn(1f);
            c.RelativeColumn(1.2f);
            c.RelativeColumn(1.3f);
        });

        table.Header(h =>
        {
            Head(h, "FILTORQ CODE");
            Head(h, "QUANTITY");
            Head(h, "PRICE");
            Head(h, "TOTAL");
        });

        foreach (var line in model.Lines)
        {
            Body(line.Code);
            Body(line.Quantity.ToString("N0", Inv));
            Body(FormatMoney(line.UnitPrice));
            Body(FormatMoney(line.Amount));
        }

        // A blank spacer row, then the grand total in a gold box spanning the
        // price + total columns (matching the company's template).
        for (var i = 0; i < 4; i++) table.Cell().Height(12);

        table.Cell().ColumnSpan(2);
        table.Cell().ColumnSpan(2).Background(Gold).Border(1).BorderColor(Ink)
            .PaddingVertical(5).AlignCenter()
            .Text(FormatMoney(model.TotalAmount)).Bold().FontSize(11).FontColor(Ink);

        void Head(TableCellDescriptor cells, string text) =>
            cells.Cell().Border(0.75f).BorderColor(Rule).PaddingVertical(5)
                .AlignCenter().Text(text).SemiBold().FontSize(9.5f).FontColor(HeadGreen);

        void Body(string text) =>
            table.Cell().BorderBottom(0.5f).BorderColor(FaintRule).PaddingVertical(3)
                .AlignCenter().Text(text).FontSize(9.5f);
    });

    private void FallbackHeader(IContainer container) => container.Column(col =>
    {
        if (model.Logo is { Length: > 0 } logo)
            col.Item().Height(40).AlignLeft().Image(logo).FitHeight();

        col.Item().Text(model.SellerName).FontSize(13).Bold();
        foreach (var l in model.SellerAddress)
            col.Item().Text(l).FontSize(9);
        col.Item().PaddingTop(6).LineHorizontal(1).LineColor(Rule);
    });

    private void FallbackFooter(IContainer container) => container.Column(col =>
    {
        col.Item().LineHorizontal(1).LineColor(Rule);
        if (model.SellerContactLine is { } line)
            col.Item().PaddingTop(3).AlignCenter().Text(line).FontSize(8).FontColor(Rule);
    });

    private string FormatMoney(decimal value)
    {
        var n = value.ToString("N2", Money);
        return model.Currency.ToUpperInvariant() switch
        {
            "USD" => $"${n}",
            "EUR" => $"€{n}",
            "GBP" => $"£{n}",
            "TRY" => $"₺{n}",
            var code => $"{n} {code}",
        };
    }
}
