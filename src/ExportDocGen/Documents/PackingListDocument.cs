using ExportDocGen.Data.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ExportDocGen.Documents;

/// <summary>Renders a <see cref="PackingListModel"/> as an A4 portrait packing
/// list: the seller's letterhead, a buyer / reference block, then a bordered
/// line table (code, description, HS code, quantity, cartons, net &amp; gross
/// weight, CBM) and the shipment totals. One shared layout for both companies —
/// only the letterhead differs. Pure layout, no database access.
///
/// v1: to be tightened against a real issued packing list (see DECISIONS.md).</summary>
public sealed class PackingListDocument(PackingListModel model) : IDocument
{
    private static readonly Color Ink = Color.FromHex("#1A1A1A");
    private static readonly Color Grid = Color.FromHex("#5C8A2C");
    private static readonly Color HeadFill = Color.FromHex("#E6EFDD");
    private static readonly Color Rule = Color.FromHex("#8A8A8A");

    private bool HasLetterhead => model.Letterhead is { Length: > 0 };
    private bool FullPageLetterhead => HasLetterhead && model.Template == ProformaTemplate.FiltorqClassic;
    private bool BandLetterhead => HasLetterhead && model.Template == ProformaTemplate.IkilerGrid;

    public DocumentMetadata GetMetadata() => new()
    {
        Title = $"Packing list {model.Reference}",
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
            if (BandLetterhead) page.Footer().Element(IkilerFooter);
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
        col.Item().PaddingTop(6).PaddingBottom(14).AlignCenter()
            .Text("PACKING LIST").FontSize(20).Bold();

        col.Item().Element(RefBlock);
        col.Item().PaddingTop(12).Element(LineItems);
        col.Item().PaddingTop(12).Element(Totals);

        if (!string.IsNullOrWhiteSpace(model.Notes))
            col.Item().PaddingTop(12).Text(t =>
            {
                t.Span("Notes: ").SemiBold();
                t.Span(model.Notes);
            });
    });

    private void RefBlock(IContainer container) => container.Row(row =>
    {
        row.RelativeItem().Column(c =>
        {
            c.Item().Text(model.BuyerName).SemiBold();
            if (!string.IsNullOrWhiteSpace(model.BuyerAddress))
                c.Item().Text(model.BuyerAddress);
            c.Item().PaddingTop(4).Text($"Country of origin: {model.CountryOfOrigin}");
        });
        row.ConstantItem(220).Column(c =>
        {
            Line(c, "PROFORMA NO", model.Reference);
            Line(c, "PROFORMA DATE", DocFormat.Date(model.Date));
            Line(c, "INVOICE NO", model.InvoiceNumber);
            Line(c, "INVOICE DATE", DocFormat.Date(model.InvoiceDate));
            Line(c, "INCOTERMS", model.Incoterm);
        });

        static void Line(ColumnDescriptor c, string label, string value) => c.Item().Row(r =>
        {
            r.ConstantItem(90).Text(label).SemiBold().FontSize(8.5f);
            r.RelativeItem().Text(value).FontSize(8.5f);
        });
    });

    private void LineItems(IContainer container) => container.Table(table =>
    {
        table.ColumnsDefinition(c =>
        {
            c.ConstantColumn(22);      // #
            c.RelativeColumn(1.6f);    // code
            c.RelativeColumn(1.9f);    // description
            c.RelativeColumn(1.2f);    // HS code
            c.RelativeColumn(1.0f);    // origin
            c.RelativeColumn(0.9f);    // qty
            c.RelativeColumn(1.1f);    // net kg
            c.RelativeColumn(1.1f);    // gross kg
            c.RelativeColumn(1.1f);    // CBM
        });

        table.Header(h =>
        {
            Head(h, "#");
            Head(h, "PRODUCT CODE");
            Head(h, "DESCRIPTION");
            Head(h, "HS CODE");
            Head(h, "ORIGIN");
            Head(h, "QTY");
            Head(h, "NET KG");
            Head(h, "GROSS KG");
            Head(h, "CBM");
        });

        foreach (var line in model.Lines)
        {
            Cell(DocFormat.Count(line.No), Align.Right);
            Cell(line.Code, Align.Left);
            Cell(line.Description, Align.Left);
            Cell(line.HsCode ?? "", Align.Left);
            Cell(line.Origin ?? "", Align.Left);
            Cell(DocFormat.Count(line.Quantity), Align.Right);
            Cell(DocFormat.Weight(line.NetWeightKg), Align.Right);
            Cell(DocFormat.Weight(line.GrossWeightKg), Align.Right);
            Cell(DocFormat.Weight(line.VolumeM3), Align.Right);
        }

        // Totals row across the numeric columns.
        Cell("", Align.Left);
        Cell("", Align.Left);
        Cell("", Align.Left);
        Cell("", Align.Left);
        Cell("TOTAL", Align.Right, bold: true);
        Cell(DocFormat.Count(model.TotalQuantity), Align.Right, bold: true);
        Cell(DocFormat.Weight(model.TotalNetWeightKg), Align.Right, bold: true);
        Cell(DocFormat.Weight(model.TotalGrossWeightKg), Align.Right, bold: true);
        Cell(DocFormat.Weight(model.TotalVolumeM3), Align.Right, bold: true);

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

    private void Totals(IContainer container) => container.Column(col =>
    {
        col.Spacing(3);
        Row("TOTAL QUANTITY", $"{DocFormat.Count(model.TotalQuantity)} PCS");
        Row("TOTAL NET WEIGHT", $"{DocFormat.Weight(model.TotalNetWeightKg)} KGS");
        Row("TOTAL GROSS WEIGHT", $"{DocFormat.Weight(model.TotalGrossWeightKg)} KGS");
        Row("TOTAL VOLUME", model.TotalVolumeText);

        void Row(string label, string value) => col.Item().Row(r =>
        {
            r.ConstantItem(160).Text(label).SemiBold();
            r.RelativeItem().Text(t =>
            {
                t.Span(": ").SemiBold();
                t.Span(value).SemiBold();
            });
        });
    });

    private void IkilerFooter(IContainer container) => container.Column(col =>
    {
        col.Item().PaddingTop(4).BorderTop(1.2f).BorderColor(Grid).PaddingTop(3).Row(r =>
        {
            r.RelativeItem().Text("İkiler Otomotiv Filtre İth. İhr. San. ve Tic. A.Ş.").FontSize(7);
            r.RelativeItem().AlignRight().Text("www.ikilerotomotiv.com").FontSize(7);
        });
    });

    private enum Align { Left, Right }
}
