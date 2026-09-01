using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ExportDocGen.Documents;

/// <summary>Renders a <see cref="ProformaInvoiceModel"/> as İkiler Otomotiv's
/// proforma: a drawn letterhead header, a text office footer, the 3-row buyer
/// box, a 5-column bordered line grid with an inline totals row, the grand total
/// spelled out, the delivery / payment terms and the verbatim bank block.
/// Pure layout — no database or configuration access.</summary>
public sealed class IkilerProformaDocument(ProformaInvoiceModel model) : IDocument
{
    // "$11.904,00" — dot thousands.
    private const string Group = ".";

    private static readonly Color Ink = Color.FromHex("#1A1A1A");
    private static readonly Color Grid = Color.FromHex("#2E7D32");
    private static readonly Color HeadFill = Color.FromHex("#C6E0B4");

    private bool HasLetterhead => model.Letterhead is { Length: > 0 };
    private bool HasBankText => !string.IsNullOrWhiteSpace(model.BankDetailsText);

    public DocumentMetadata GetMetadata() => new()
    {
        Title = $"Proforma invoice {model.InvoiceNumber}",
        Author = model.SellerName,
    };

    public void Compose(IDocumentContainer container) => container.Page(page =>
    {
        page.Size(PageSizes.A4);
        page.MarginHorizontal(1.6f, Unit.Centimetre);
        page.MarginVertical(1.1f, Unit.Centimetre);
        page.DefaultTextStyle(t => t.FontSize(9.5f).FontColor(Ink).LineHeight(1.2f));

        page.Header().Element(Header);
        page.Footer().Element(Footer);
        page.Content().Element(Body);
    });

    private void Header(IContainer container) => container.Column(col =>
    {
        if (HasLetterhead)
            col.Item().PaddingBottom(6).Image(model.Letterhead!).FitWidth();
        else
            col.Item().PaddingBottom(6).Text(model.SellerName).FontSize(13).Bold();
    });

    private void Body(IContainer container) => container.Column(col =>
    {
        col.Item().PaddingTop(10).PaddingBottom(16).AlignCenter()
            .Text("PROFORMA INVOICE").FontFamily(Fonts.TimesNewRoman).FontSize(24).Bold();

        col.Item().Element(BuyerBox);
        col.Item().PaddingTop(16).Element(LineItems);
        col.Item().PaddingTop(10).Text(t =>
        {
            t.Span("TOTAL: ").Bold();
            t.Span(model.AmountInWords).Bold();
        });

        col.Item().PaddingTop(14).Element(TermsBlock);

        if (HasBankText)
            col.Item().PaddingTop(14).Element(BankBlock);

        if (!string.IsNullOrWhiteSpace(model.Notes))
            col.Item().PaddingTop(12).Text(t =>
            {
                t.Span("Notes: ").SemiBold();
                t.Span(model.Notes);
            });
    });

    private void BuyerBox(IContainer container) => container.Table(table =>
    {
        table.ColumnsDefinition(c =>
        {
            c.ConstantColumn(70);
            c.RelativeColumn(3f);
            c.ConstantColumn(52);
            c.RelativeColumn(3f);
        });

        Row("Name:", model.BuyerName, "Date:", DocFormat.Date(model.InvoiceDate));
        Row("P/I NO:", model.InvoiceNumber, "Tel :", model.BuyerPhone ?? "");
        Row("Address:", model.BuyerAddress, "E-Mail:", model.BuyerEmail ?? "");

        void Row(string l1, string v1, string l2, string v2)
        {
            Cell(l1, bold: true);
            Cell(v1, bold: false);
            Cell(l2, bold: true);
            Cell(v2, bold: false);
        }

        void Cell(string text, bool bold)
        {
            var c = table.Cell().Border(0.9f).BorderColor(Grid)
                .PaddingVertical(4).PaddingHorizontal(6);
            var t = c.Text(text).FontSize(9.5f);
            if (bold) t.SemiBold();
        }
    });

    private void LineItems(IContainer container) => container.Table(table =>
    {
        table.ColumnsDefinition(c =>
        {
            c.RelativeColumn(1.7f);   // product code
            c.RelativeColumn(1.9f);   // description
            c.RelativeColumn(1.1f);   // unit price
            c.RelativeColumn(1.1f);   // quantity
            c.RelativeColumn(1.2f);   // total price
        });

        table.Header(h =>
        {
            Head(h, "PRODUCT CODE");
            Head(h, "DESCRIPTION");
            Head(h, "UNIT PRICE");
            Head(h, "QUANTITY");
            Head(h, "TOTAL PRICE");
        });

        foreach (var line in model.Lines)
        {
            Cell(line.Code, Align.Left);
            Cell(line.Description, Align.Left);
            Cell(DocFormat.Money(line.UnitPrice, model.Currency, Group), Align.Right);
            Cell(DocFormat.Count(line.Quantity), Align.Right);
            Cell(DocFormat.Money(line.Amount, model.Currency, Group), Align.Right);
        }

        // Inline totals row — sum of quantity and sum of total, like the sample.
        Cell("", Align.Left);
        Cell("", Align.Left);
        Cell("", Align.Right);
        Cell(DocFormat.Count(model.TotalQuantity), Align.Right, bold: true);
        Cell(DocFormat.Money(model.TotalAmount, model.Currency, Group), Align.Right, bold: true);

        void Head(TableCellDescriptor cells, string text) =>
            cells.Cell().Background(HeadFill).Border(0.9f).BorderColor(Grid)
                .PaddingVertical(5).PaddingHorizontal(4).AlignCenter()
                .Text(text).SemiBold().FontSize(9.5f);

        void Cell(string text, Align align, bool bold = false)
        {
            var c = table.Cell().Border(0.7f).BorderColor(Grid)
                .PaddingVertical(3).PaddingHorizontal(4);
            c = align switch
            {
                Align.Right => c.AlignRight(),
                Align.Left => c.AlignLeft(),
                _ => c.AlignCenter(),
            };
            var t = c.Text(text).FontSize(9f);
            if (bold) t.SemiBold();
        }
    });

    private void TermsBlock(IContainer container) => container.Column(col =>
    {
        col.Spacing(4);
        Term("INCOTERMS", model.Incoterm);
        Term("DELIVERY TIME", model.DeliveryTime);
        Term("VALIDITY", model.Validity);
        Term("PAYMENT TERM", model.PaymentTerms);

        void Term(string label, string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            col.Item().Row(r =>
            {
                r.ConstantItem(120).Text(label).SemiBold();
                r.RelativeItem().Text(t =>
                {
                    t.Span(": ").SemiBold();
                    t.Span(value).SemiBold();
                });
            });
        }
    });

    private void BankBlock(IContainer container) => container.Column(col =>
    {
        col.Spacing(3);
        col.Item().PaddingBottom(2).Text($"Bank Detail ({model.Currency}) :").SemiBold();
        foreach (var line in model.BankDetailsText!.ReplaceLineEndings("\n").Split('\n'))
            col.Item().Text(line);
    });

    private void Footer(IContainer container) => container.Column(col =>
    {
        col.Item().PaddingTop(4).BorderTop(1.5f).BorderColor(Grid).PaddingTop(4).Row(r =>
        {
            r.RelativeItem().Column(o =>
            {
                o.Item().Text("HEAD OFFICE").Bold().FontSize(9);
                o.Item().Text("Sümer Mh. 3. San. Sit. 25. Cd. No:63  Tel :+90 258 268 87 87").FontSize(7);
                o.Item().Text("Merkezefendi / Denizli / TURKEY        :+90 258 268 21 91").FontSize(7);
                o.Item().Text("www.ikilerotomotiv.com                 Fax :+90 258 268 90 86").FontSize(7);
                o.Item().Text("info@ikilerotomotiv.com").FontSize(7);
            });
            r.RelativeItem().Column(o =>
            {
                o.Item().Text("IZMIR OFFICE").Bold().FontSize(9);
                o.Item().Text("Pınarbaşı Mh. Kemalpaşa Cd. 7419 Sk.  Tel :+90 232 478 38 53").FontSize(7);
                o.Item().Text("5. San. Sit. No:19 Bornova / IZMIR    Fax :+90 232 478 39 53").FontSize(7);
                o.Item().Text("www.ikilerotomotiv.com").FontSize(7);
                o.Item().Text("yusuf.ayvali@ikilerotomotiv.com").FontSize(7);
            });
        });
    });

    private enum Align { Left, Center, Right }
}
