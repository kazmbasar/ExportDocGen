using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ExportDocGen.Documents;

/// <summary>Renders a <see cref="ProformaInvoiceModel"/> as an A4 proforma
/// invoice. Pure layout — no database or configuration access, so it can be
/// unit-tested and previewed directly.</summary>
public sealed class ProformaInvoiceDocument(ProformaInvoiceModel model) : IDocument
{
    // Export documents are in English regardless of the server's locale.
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private static readonly Color Rule = Colors.Grey.Lighten1;
    private static readonly Color FaintRule = Colors.Grey.Lighten2;
    private static readonly Color HeadFill = Colors.Grey.Lighten3;
    private static readonly Color Muted = Colors.Grey.Darken1;

    public DocumentMetadata GetMetadata() => new()
    {
        Title = $"Proforma invoice {model.InvoiceNumber}",
        Author = model.SellerName,
    };

    public void Compose(IDocumentContainer container) => container.Page(page =>
    {
        page.Size(PageSizes.A4);
        page.Margin(1.6f, Unit.Centimetre);
        page.DefaultTextStyle(t => t.FontSize(9).LineHeight(1.25f));

        page.Header().Element(Header);
        page.Content().PaddingVertical(14).Element(Body);
        page.Footer().Element(Footer);
    });

    private void Header(IContainer container) => container.Column(col =>
    {
        col.Item().Row(row =>
        {
            row.RelativeItem().Column(seller =>
            {
                if (model.SellerLogoPath is { } logo)
                    seller.Item().PaddingBottom(6).Height(42).Image(logo).FitHeight();

                seller.Item().Text(model.SellerName).FontSize(13).Bold();
                foreach (var l in model.SellerAddress)
                    seller.Item().Text(l);
                seller.Item().PaddingTop(2).Text(t =>
                {
                    t.Span("Tax ID: ").SemiBold();
                    t.Span(model.SellerTaxId);
                });
                seller.Item().Text($"{model.SellerPhone}   {model.SellerEmail}");
            });

            row.ConstantItem(210).Column(box =>
            {
                box.Item().AlignRight().Text("PROFORMA INVOICE").FontSize(16).Bold();
                box.Item().PaddingTop(6).Border(1).BorderColor(Rule).Padding(6).Column(meta =>
                {
                    meta.Item().Text(t => { t.Span("No.  ").SemiBold(); t.Span(model.InvoiceNumber); });
                    meta.Item().Text(t =>
                    {
                        t.Span("Date  ").SemiBold();
                        t.Span(model.InvoiceDate.ToString("dd MMM yyyy", Inv));
                    });
                });
            });
        });

        col.Item().PaddingTop(10).LineHorizontal(1).LineColor(Rule);
    });

    private void Body(IContainer container) => container.Column(col =>
    {
        col.Spacing(12);

        col.Item().Row(row =>
        {
            row.RelativeItem().Column(buyer =>
            {
                buyer.Item().Text("Buyer").FontColor(Muted).FontSize(8);
                buyer.Item().Text(model.BuyerName).SemiBold();
                foreach (var l in model.BuyerAddress)
                    buyer.Item().Text(l);
                if (!string.IsNullOrWhiteSpace(model.BuyerContact))
                    buyer.Item().PaddingTop(2).Text($"Attn: {model.BuyerContact}");
            });

            row.ConstantItem(230).Border(1).BorderColor(Rule).Padding(8).Column(terms =>
            {
                terms.Spacing(2);
                TermRow(terms, "Incoterm", model.Incoterm);
                TermRow(terms, "Currency", model.Currency);
                TermRow(terms, "Payment terms",
                    string.IsNullOrWhiteSpace(model.PaymentTerms) ? "—" : model.PaymentTerms!);
                TermRow(terms, "Country of origin", model.CountryOfOrigin);
            });
        });

        col.Item().Element(LineItems);

        col.Item().Row(row =>
        {
            row.RelativeItem();
            row.ConstantItem(250).Border(1).BorderColor(Rule).Padding(8).Column(totals =>
            {
                totals.Spacing(2);
                TotalRow(totals, "Total amount",
                    $"{model.TotalAmount.ToString("N2", Inv)} {model.Currency}", bold: true);
                TotalRow(totals, "Net weight", $"{model.TotalNetWeightKg.ToString("0.###", Inv)} kg");
                TotalRow(totals, "Gross weight", $"{model.TotalGrossWeightKg.ToString("0.###", Inv)} kg");
                TotalRow(totals, "Cartons", model.TotalCartons.ToString(Inv));
                TotalRow(totals, "Volume", $"{model.TotalVolumeM3.ToString("0.###", Inv)} m³");
            });
        });

        if (!string.IsNullOrWhiteSpace(model.Notes))
        {
            col.Item().Column(notes =>
            {
                notes.Item().Text("Notes").FontColor(Muted).FontSize(8);
                notes.Item().Text(model.Notes);
            });
        }

        col.Item().Element(BankBox);
    });

    private void LineItems(IContainer container) => container.Table(table =>
    {
        table.ColumnsDefinition(c =>
        {
            c.ConstantColumn(22);
            c.RelativeColumn(2.2f);
            c.RelativeColumn(3.4f);
            c.RelativeColumn(1.3f);
            c.RelativeColumn(1f);
            c.RelativeColumn(1.5f);
            c.RelativeColumn(1.7f);
        });

        table.Header(h =>
        {
            HeadCell(h, "#");
            HeadCell(h, "Part No.");
            HeadCell(h, "Description");
            HeadCell(h, "HS code");
            HeadCell(h, "Qty", right: true);
            HeadCell(h, "Unit price", right: true);
            HeadCell(h, "Amount", right: true);
        });

        foreach (var line in model.Lines)
        {
            BodyCell(table, line.LineNumber.ToString());
            BodyCell(table, line.PartNumber);
            BodyCell(table, line.Description);
            BodyCell(table, line.HsCode ?? "");
            BodyCell(table, line.Quantity.ToString("N0", Inv), right: true);
            BodyCell(table, line.UnitPrice.ToString("0.###", Inv), right: true);
            BodyCell(table, line.Amount.ToString("N2", Inv), right: true);
        }
    });

    private void BankBox(IContainer container) => container.Column(col =>
    {
        col.Item().Text("Bank details").FontColor(Muted).FontSize(8);
        col.Item().Text(t =>
        {
            Field(t, "Beneficiary", model.Bank.BeneficiaryName);
            Field(t, "    Bank", model.Bank.BankName);
        });
        col.Item().Text(t =>
        {
            Field(t, "IBAN", model.Bank.Iban);
            Field(t, "    SWIFT", model.Bank.Swift);
        });
    });

    private static void Footer(IContainer container) => container.Column(col =>
    {
        col.Item().LineHorizontal(1).LineColor(Rule);
        col.Item().PaddingTop(4).Row(row =>
        {
            row.RelativeItem().Text("This is a proforma invoice and not a demand for payment.")
                .FontSize(7.5f).FontColor(Muted);
            row.ConstantItem(120).AlignRight().Text(t =>
            {
                t.DefaultTextStyle(s => s.FontSize(7.5f).FontColor(Muted));
                t.Span("Page ");
                t.CurrentPageNumber();
                t.Span(" / ");
                t.TotalPages();
            });
        });
    });

    private static void TermRow(ColumnDescriptor col, string label, string value) =>
        col.Item().Row(r =>
        {
            r.ConstantItem(110).Text(label).FontColor(Muted);
            r.RelativeItem().Text(value).SemiBold();
        });

    private static void TotalRow(ColumnDescriptor col, string label, string value, bool bold = false) =>
        col.Item().Row(r =>
        {
            r.RelativeItem().Text(label).FontColor(Muted);
            var v = r.ConstantItem(130).AlignRight().Text(value);
            if (bold) v.Bold();
        });

    private static void Field(TextDescriptor text, string label, string value)
    {
        text.Span($"{label}: ").SemiBold();
        text.Span(string.IsNullOrWhiteSpace(value) ? "—" : value);
    }

    private static void HeadCell(TableCellDescriptor cells, string text, bool right = false)
    {
        var c = cells.Cell().Background(HeadFill).BorderBottom(1).BorderColor(Rule)
            .PaddingVertical(4).PaddingHorizontal(4);
        (right ? c.AlignRight() : c).Text(text).SemiBold().FontSize(8.5f);
    }

    private static void BodyCell(TableDescriptor table, string text, bool right = false)
    {
        var c = table.Cell().BorderBottom(0.5f).BorderColor(FaintRule)
            .PaddingVertical(3).PaddingHorizontal(4);
        (right ? c.AlignRight() : c).Text(text);
    }
}
