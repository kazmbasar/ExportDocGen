using ExportDocGen.Data.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ExportDocGen.Documents;

/// <summary>Renders a <see cref="CommercialInvoiceModel"/> as an A4 commercial
/// invoice: the seller's letterhead, a buyer box with the invoice number/date,
/// a bordered line table (code, description, HS code, brand, origin, quantity,
/// unit &amp; total price), the delivery / payment terms, the grand total, the
/// shipment weight totals and the verbatim bank block. One shared layout for
/// both companies — only the letterhead differs. Pure layout, no DB access.</summary>
public sealed class CommercialInvoiceDocument(CommercialInvoiceModel model) : IDocument
{
    private const string Group = ".";   // "$3.555,00"

    private static readonly Color Ink = Color.FromHex("#1A1A1A");
    private static readonly Color Grid = Color.FromHex("#2E7D32");
    private static readonly Color HeadFill = Color.FromHex("#C6E0B4");
    private static readonly Color Rule = Color.FromHex("#8A8A8A");

    private bool HasLetterhead => model.Letterhead is { Length: > 0 };
    private bool FullPageLetterhead => HasLetterhead && model.Template == ProformaTemplate.FiltorqClassic;
    private bool BandLetterhead => HasLetterhead && model.Template == ProformaTemplate.IkilerGrid;
    private bool HasBankText => !string.IsNullOrWhiteSpace(model.BankDetailsText);

    public DocumentMetadata GetMetadata() => new()
    {
        Title = $"Commercial invoice {model.InvoiceNumber}",
        Author = model.SellerName,
    };

    public void Compose(IDocumentContainer container) => container.Page(page =>
    {
        page.Size(PageSizes.A4);
        page.DefaultTextStyle(t => t.FontSize(9).FontColor(Ink).LineHeight(1.2f));

        if (FullPageLetterhead)
        {
            page.MarginTop(4.6f, Unit.Centimetre);
            page.MarginBottom(2.7f, Unit.Centimetre);
            page.MarginHorizontal(1.9f, Unit.Centimetre);
            page.Background().Image(model.Letterhead!).FitArea();
        }
        else
        {
            page.MarginHorizontal(1.6f, Unit.Centimetre);
            page.MarginVertical(1.1f, Unit.Centimetre);
            page.Header().Element(Header);
        }

        page.Content().Element(Body);
    });

    private void Header(IContainer container) => container.Column(col =>
    {
        if (BandLetterhead)
            col.Item().PaddingBottom(6).Image(model.Letterhead!).FitWidth();
        else
        {
            col.Item().Text(model.SellerName).FontSize(12).Bold();
            col.Item().PaddingTop(6).LineHorizontal(1).LineColor(Rule);
        }
    });

    private void Body(IContainer container) => container.Column(col =>
    {
        col.Item().Element(TopBlock);
        col.Item().PaddingTop(10).PaddingBottom(12).AlignCenter()
            .Text("COMMERCIAL INVOICE").FontFamily(Fonts.TimesNewRoman).FontSize(22).Bold();

        col.Item().Element(LineItems);
        col.Item().PaddingTop(10).Element(TermsBlock);
        col.Item().PaddingTop(12).Element(WeightTotals);

        if (HasBankText)
            col.Item().PaddingTop(12).Element(BankBlock);

        if (!string.IsNullOrWhiteSpace(model.Notes))
            col.Item().PaddingTop(10).Text(t =>
            {
                t.Span("Notes: ").SemiBold();
                t.Span(model.Notes);
            });
    });

    private void TopBlock(IContainer container) => container.Row(row =>
    {
        row.RelativeItem().Border(0.9f).BorderColor(Grid).Padding(6).Column(c =>
        {
            c.Item().Text(model.BuyerName).SemiBold();
            if (!string.IsNullOrWhiteSpace(model.BuyerTaxId))
                c.Item().Text($"Tax / reg. no.: {model.BuyerTaxId}").FontSize(8);
            if (!string.IsNullOrWhiteSpace(model.BuyerAddress))
                c.Item().Text(model.BuyerAddress);
            if (!string.IsNullOrWhiteSpace(model.BuyerPhone))
                c.Item().Text(model.BuyerPhone);
            if (!string.IsNullOrWhiteSpace(model.BuyerEmail))
                c.Item().Text(model.BuyerEmail);
        });
        row.ConstantItem(12);
        row.ConstantItem(190).Column(c =>
        {
            Line(c, "INVOICE NO", model.InvoiceNumber);
            Line(c, "INVOICE DATE", DocFormat.Date(model.InvoiceDate));
        });

        static void Line(ColumnDescriptor c, string label, string value) => c.Item().Row(r =>
        {
            r.ConstantItem(78).Border(0.7f).BorderColor(Grid).Padding(3).Text(label).SemiBold().FontSize(8);
            r.RelativeItem().Border(0.7f).BorderColor(Grid).Padding(3).Text(value).FontSize(8);
        });
    });

    private void LineItems(IContainer container) => container.Table(table =>
    {
        table.ColumnsDefinition(c =>
        {
            c.ConstantColumn(20);      // №
            c.RelativeColumn(1.5f);    // code
            c.RelativeColumn(1.5f);    // description
            c.RelativeColumn(1.7f);    // HS code
            c.RelativeColumn(0.9f);    // brand
            c.RelativeColumn(0.9f);    // origin
            c.RelativeColumn(1.1f);    // quantity
            c.RelativeColumn(1.0f);    // unit price
            c.RelativeColumn(1.2f);    // total price
        });

        table.Header(h =>
        {
            Head(h, "№");
            Head(h, "PRODUCT CODE");
            Head(h, "DESCRIPTION");
            Head(h, "HS CODES");
            Head(h, "BRAND");
            Head(h, "ORIGIN");
            Head(h, "QUANTITY");
            Head(h, "UNIT PRICE");
            Head(h, "TOTAL PRICE");
        });

        foreach (var line in model.Lines)
        {
            Cell(DocFormat.Count(line.No), Align.Right);
            Cell(line.Code, Align.Left);
            Cell(line.Description, Align.Left);
            Cell(line.HsCode ?? "", Align.Left);
            Cell(line.Brand ?? "", Align.Left);
            Cell(line.Origin ?? "", Align.Left);
            Cell($"{DocFormat.Count(line.Quantity)} PCS", Align.Right);
            Cell(DocFormat.Money(line.UnitPrice, model.Currency, Group), Align.Right);
            Cell(DocFormat.Money(line.Amount, model.Currency, Group), Align.Right);
        }

        // Grand-total row: Σ quantity and Σ total price.
        Cell("", Align.Left);
        Cell("", Align.Left);
        Cell("", Align.Left);
        Cell("", Align.Left);
        Cell("", Align.Left);
        Cell("TOTAL", Align.Right, bold: true);
        Cell($"{DocFormat.Count(model.TotalQuantity)} PCS", Align.Right, bold: true);
        Cell("", Align.Right);
        Cell(DocFormat.Money(model.TotalAmount, model.Currency, Group), Align.Right, bold: true);

        void Head(TableCellDescriptor cells, string text) =>
            cells.Cell().Background(HeadFill).Border(0.8f).BorderColor(Grid)
                .PaddingVertical(4).PaddingHorizontal(3).AlignCenter()
                .Text(text).SemiBold().FontSize(8);

        void Cell(string text, Align align, bool bold = false)
        {
            var c = table.Cell().Border(0.6f).BorderColor(Grid)
                .PaddingVertical(3).PaddingHorizontal(3);
            c = align == Align.Right ? c.AlignRight() : c.AlignLeft();
            var t = c.Text(text).FontSize(8);
            if (bold) t.SemiBold();
        }
    });

    private void TermsBlock(IContainer container) => container.Column(col =>
    {
        col.Spacing(3);
        col.Item().Text($"TERMS OF DELIVERY ({model.Incoterm})").SemiBold();
        if (!string.IsNullOrWhiteSpace(model.PaymentTerms))
            col.Item().Text($"TERMS OF PAYMENT ({model.PaymentTerms})").SemiBold();
    });

    private void WeightTotals(IContainer container) =>
        container.Border(0.8f).BorderColor(Grid).Padding(6).Column(col =>
        {
            col.Spacing(2);
            Row("TOTAL GROSS WEIGHT", $"{DocFormat.Weight(model.TotalGrossWeightKg)} KGS");
            Row("TOTAL NET WEIGHT", $"{DocFormat.Weight(model.TotalNetWeightKg)} KGS");
            Row("TOTAL QUANTITY", $"{DocFormat.Count(model.TotalQuantity)} PCS");
            Row("TOTAL VOLUME", model.TotalVolumeText);

            void Row(string label, string value) => col.Item().Row(r =>
            {
                r.ConstantItem(160).Text(label).SemiBold();
                r.RelativeItem().Text(t => { t.Span(": ").SemiBold(); t.Span(value).SemiBold(); });
            });
        });

    private void BankBlock(IContainer container) => container.Column(col =>
    {
        col.Spacing(2);
        col.Item().Text($"Bank Detail ({model.Currency}) :").SemiBold();
        foreach (var line in model.BankDetailsText!.ReplaceLineEndings("\n").Split('\n'))
            col.Item().Text(line).Italic();
    });

    private enum Align { Left, Right }
}
