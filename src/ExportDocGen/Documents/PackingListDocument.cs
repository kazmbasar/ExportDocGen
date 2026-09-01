using ExportDocGen.Data.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ExportDocGen.Documents;

/// <summary>Renders a <see cref="PackingListModel"/> as an A4 packing list laid
/// out like the company's real issued document: a buyer box + invoice number,
/// then a 13-column bordered grid — product code, description, HS code, brand,
/// origin, quantity, and unit + total for volume, net weight and gross weight —
/// with an inline totals row, followed by the shipment totals box. The seller
/// company's letterhead is swapped in (Filtorq full-page background, İkiler
/// header band). Pure layout, no database access.</summary>
public sealed class PackingListDocument(PackingListModel model) : IDocument
{
    private static readonly Color Ink = Color.FromHex("#1A1A1A");
    private static readonly Color Rule = Color.FromHex("#5A5A5A");

    private bool HasLetterhead => model.Letterhead is { Length: > 0 };
    private bool FullPageLetterhead => HasLetterhead && model.Template == ProformaTemplate.FiltorqClassic;
    private bool BandLetterhead => HasLetterhead && model.Template == ProformaTemplate.IkilerGrid;

    public DocumentMetadata GetMetadata() => new()
    {
        Title = $"Packing list {model.InvoiceNumber}",
        Author = model.SellerName,
    };

    public void Compose(IDocumentContainer container) => container.Page(page =>
    {
        page.Size(PageSizes.A4);
        page.DefaultTextStyle(t => t.FontSize(8).FontColor(Ink).LineHeight(1.15f));

        if (FullPageLetterhead)
        {
            page.MarginTop(4.6f, Unit.Centimetre);
            page.MarginBottom(2.7f, Unit.Centimetre);
            page.MarginHorizontal(1.4f, Unit.Centimetre);
            page.Background().Image(model.Letterhead!).FitArea();
        }
        else
        {
            page.MarginHorizontal(1.2f, Unit.Centimetre);
            page.MarginVertical(1.0f, Unit.Centimetre);
            page.Header().Element(Header);
        }

        page.Content().Element(Body);
    });

    private void Header(IContainer container) => container.Column(col =>
    {
        if (BandLetterhead)
            col.Item().PaddingBottom(4).Image(model.Letterhead!).FitWidth();
        else
        {
            col.Item().Text(model.SellerName).FontSize(11).Bold();
            col.Item().PaddingTop(4).LineHorizontal(0.8f).LineColor(Rule);
        }
    });

    private void Body(IContainer container) => container.Column(col =>
    {
        col.Item().Element(TopBlock);
        col.Item().PaddingTop(8).PaddingBottom(10).AlignCenter()
            .Text("PACKING LIST").FontSize(16).Bold();

        col.Item().Element(LineItems);
        col.Item().PaddingTop(14).Element(Totals);

        if (!string.IsNullOrWhiteSpace(model.Notes))
            col.Item().PaddingTop(10).Text(t =>
            {
                t.Span("Notes: ").SemiBold();
                t.Span(model.Notes);
            });
    });

    private void TopBlock(IContainer container) => container.Row(row =>
    {
        row.RelativeItem().Border(0.8f).BorderColor(Rule).Padding(5).AlignCenter().Column(c =>
        {
            c.Item().Text(model.BuyerName).SemiBold().FontSize(9);
            if (!string.IsNullOrWhiteSpace(model.BuyerAddress))
                c.Item().Text(model.BuyerAddress).FontSize(8);
            if (!string.IsNullOrWhiteSpace(model.BuyerPhone))
                c.Item().Text(model.BuyerPhone).FontSize(8);
            if (!string.IsNullOrWhiteSpace(model.BuyerEmail))
                c.Item().Text(model.BuyerEmail).FontSize(8);
        });
        row.ConstantItem(14);
        row.ConstantItem(200).AlignMiddle().Table(t =>
        {
            t.ColumnsDefinition(c => { c.ConstantColumn(78); c.RelativeColumn(); });
            KeyValue(t, "INVOICE NO", model.InvoiceNumber);
            KeyValue(t, "INVOICE DATE", DocFormat.Date(model.InvoiceDate));
        });

        static void KeyValue(TableDescriptor t, string k, string v)
        {
            t.Cell().Border(0.7f).BorderColor(Rule).Padding(3).Text(k).SemiBold().FontSize(8);
            t.Cell().Border(0.7f).BorderColor(Rule).Padding(3).AlignRight().Text(v).SemiBold().FontSize(8);
        }
    });

    private void LineItems(IContainer container) => container.Table(table =>
    {
        table.ColumnsDefinition(c =>
        {
            c.RelativeColumn(1.5f);   // product code
            c.RelativeColumn(1.3f);   // description
            c.RelativeColumn(1.6f);   // HS codes
            c.RelativeColumn(0.9f);   // brand
            c.RelativeColumn(0.8f);   // origin
            c.RelativeColumn(0.6f);   // qty
            c.RelativeColumn(0.9f);   // unit volume
            c.RelativeColumn(1.0f);   // total volume
            c.RelativeColumn(0.6f);   // total qty
            c.RelativeColumn(0.9f);   // unit net weight
            c.RelativeColumn(1.0f);   // total net weight
            c.RelativeColumn(0.9f);   // unit gross weight
            c.RelativeColumn(1.05f);  // total gross weight
        });

        table.Header(h =>
        {
            Head(h, "PRODUCT CODE");
            Head(h, "DESCRIPTION");
            Head(h, "HS CODES");
            Head(h, "BRAND");
            Head(h, "ORIGIN");
            Head(h, "QTY");
            Head(h, "UNIT VOLUME");
            Head(h, "TOTAL VOLUME");
            Head(h, "TOTAL QTY");
            Head(h, "UNIT NET WEIGHT");
            Head(h, "TOTAL NET WEIGHT");
            Head(h, "UNIT GROSS WEIGHT");
            Head(h, "TOTAL GROSS WEIGHT");
        });

        foreach (var line in model.Lines)
        {
            var q = line.Quantity > 0 ? line.Quantity : 1;
            Cell(line.Code, left: true);
            Cell(line.Description, left: true);
            Cell(line.HsCode ?? "", left: true);
            Cell(line.Brand ?? "");
            Cell(line.Origin ?? "");
            Cell(DocFormat.Count(line.Quantity));
            Cell(DocFormat.Volume(line.VolumeM3 / q));
            Cell(DocFormat.Volume(line.VolumeM3));
            Cell(DocFormat.Count(line.Quantity));
            Cell(DocFormat.Weight(line.NetWeightKg / q));
            Cell(DocFormat.Weight(line.NetWeightKg));
            Cell(DocFormat.Weight(line.GrossWeightKg / q));
            Cell(DocFormat.Weight(line.GrossWeightKg));
        }

        // Inline totals row — quantity, total volume, total qty, total net & gross.
        Cell(""); Cell(""); Cell(""); Cell(""); Cell("");
        Cell(DocFormat.Count(model.TotalQuantity), bold: true);
        Cell("");
        Cell(DocFormat.Volume(model.TotalVolumeM3), bold: true);
        Cell(DocFormat.Count(model.TotalQuantity), bold: true);
        Cell("");
        Cell(DocFormat.Weight(model.TotalNetWeightKg), bold: true);
        Cell("");
        Cell(DocFormat.Weight(model.TotalGrossWeightKg), bold: true);

        void Head(TableCellDescriptor cells, string text) =>
            cells.Cell().Border(0.6f).BorderColor(Rule).PaddingVertical(3).PaddingHorizontal(2)
                .AlignCenter().Text(text).SemiBold().FontSize(6.5f);

        void Cell(string text, bool left = false, bool bold = false)
        {
            var c = table.Cell().Border(0.5f).BorderColor(Rule).PaddingVertical(2).PaddingHorizontal(2);
            c = left ? c.AlignLeft() : c.AlignRight();
            var t = c.Text(text).FontSize(7);
            if (bold) t.SemiBold();
        }
    });

    private void Totals(IContainer container) =>
        container.Width(260).Border(0.8f).BorderColor(Rule).Padding(6).Column(col =>
        {
            col.Spacing(2);
            Row("TOTAL GROSS WEIGHT", $"{DocFormat.Weight(model.TotalGrossWeightKg)} KGS");
            Row("TOTAL NET WEIGHT", $"{DocFormat.Weight(model.TotalNetWeightKg)} KGS");
            Row("TOTAL QUANTITY", $"{DocFormat.Count(model.TotalQuantity)} PCS");
            Row("TOTAL VOLUME", model.TotalVolumeText);

            void Row(string label, string value) => col.Item().Row(r =>
            {
                r.ConstantItem(150).Text(label).SemiBold().FontSize(8);
                r.ConstantItem(10).Text(":").SemiBold();
                r.RelativeItem().AlignRight().Text(value).SemiBold().FontSize(8);
            });
        });
}
