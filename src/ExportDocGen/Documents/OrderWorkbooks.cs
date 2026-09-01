using ClosedXML.Excel;

namespace ExportDocGen.Documents;

/// <summary>Builds the packing list and commercial invoice as editable
/// <c>.xlsx</c> workbooks (one worksheet each). Plain cell values — no formulas —
/// so the file opens clean and stays hand-editable. Mirrors the columns of the
/// matching QuestPDF document.</summary>
public static class OrderWorkbooks
{
    private static readonly XLColor Head = XLColor.FromHtml("#C6E0B4");
    private const string Money = "#,##0.00";
    private const string Qty = "#,##0";
    private const string Kg = "#,##0.000";
    private const string M3 = "#,##0.000000";

    public static byte[] CommercialInvoice(CommercialInvoiceModel m)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Commercial Invoice");
        var r = 1;

        ws.Cell(r++, 1).Value = m.SellerName;
        ws.Row(r - 1).Style.Font.Bold = true;

        ws.Cell(r, 1).Value = m.BuyerName;
        ws.Cell(r, 8).Value = "INVOICE NO";
        ws.Cell(r++, 9).Value = m.InvoiceNumber;
        if (!string.IsNullOrWhiteSpace(m.BuyerAddress)) ws.Cell(r, 1).Value = m.BuyerAddress;
        ws.Cell(r, 8).Value = "INVOICE DATE";
        ws.Cell(r++, 9).Value = m.InvoiceDate.ToDateTime(TimeOnly.MinValue);
        ws.Cell(r - 1, 9).Style.DateFormat.Format = "dd.MM.yyyy";
        if (!string.IsNullOrWhiteSpace(m.BuyerPhone)) ws.Cell(r++, 1).Value = m.BuyerPhone;
        if (!string.IsNullOrWhiteSpace(m.BuyerEmail)) ws.Cell(r++, 1).Value = m.BuyerEmail;
        r++;

        ws.Cell(r, 1).Value = "COMMERCIAL INVOICE";
        ws.Range(r, 1, r, 9).Merge().Style.Font.SetBold().Font.SetFontSize(14).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
        r += 2;

        var head = r;
        string[] cols = ["№", "PRODUCT CODE", "DESCRIPTION", "HS CODES", "BRAND", "ORIGIN", "QUANTITY", "UNIT PRICE", "TOTAL PRICE"];
        for (var c = 0; c < cols.Length; c++) ws.Cell(head, c + 1).Value = cols[c];
        ws.Range(head, 1, head, 9).Style.Font.SetBold().Fill.SetBackgroundColor(Head);
        r++;

        foreach (var line in m.Lines)
        {
            ws.Cell(r, 1).Value = line.No;
            ws.Cell(r, 2).Value = line.Code;
            ws.Cell(r, 3).Value = line.Description;
            ws.Cell(r, 4).Value = line.HsCode ?? "";
            ws.Cell(r, 5).Value = line.Brand ?? "";
            ws.Cell(r, 6).Value = line.Origin ?? "";
            ws.Cell(r, 7).Value = line.Quantity;
            ws.Cell(r, 8).Value = line.UnitPrice;
            ws.Cell(r, 9).Value = line.Amount;
            ws.Cell(r, 7).Style.NumberFormat.Format = Qty;
            ws.Cell(r, 8).Style.NumberFormat.Format = Money;
            ws.Cell(r, 9).Style.NumberFormat.Format = Money;
            r++;
        }

        ws.Cell(r, 6).Value = "TOTAL";
        ws.Cell(r, 7).Value = m.TotalQuantity;
        ws.Cell(r, 9).Value = m.TotalAmount;
        ws.Cell(r, 7).Style.NumberFormat.Format = Qty;
        ws.Cell(r, 9).Style.NumberFormat.Format = Money;
        ws.Range(r, 6, r, 9).Style.Font.Bold = true;
        var lastRow = r;
        r += 2;

        ws.Cell(r++, 1).Value = $"TERMS OF DELIVERY ({m.Incoterm})";
        if (!string.IsNullOrWhiteSpace(m.PaymentTerms))
            ws.Cell(r++, 1).Value = $"TERMS OF PAYMENT ({m.PaymentTerms})";
        r++;

        r = WeightTotals(ws, r, m.TotalGrossWeightKg, m.TotalNetWeightKg, m.TotalQuantity, m.TotalVolumeText);
        r++;
        BankBlock(ws, ref r, m.Currency, m.BankDetailsText);

        ws.Range(head, 1, lastRow, 9).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        ws.Range(head, 1, lastRow, 9).Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        ws.Columns(1, 9).AdjustToContents(8d, 42d);
        ws.Column(3).Width = Math.Max(ws.Column(3).Width, 22);

        return Save(wb);
    }

    public static byte[] PackingList(PackingListModel m)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Packing List");
        var r = 1;

        ws.Cell(r++, 1).Value = m.SellerName;
        ws.Row(r - 1).Style.Font.Bold = true;

        ws.Cell(r, 1).Value = m.BuyerName;
        ws.Cell(r, 11).Value = "INVOICE NO";
        ws.Cell(r++, 12).Value = m.InvoiceNumber;
        if (!string.IsNullOrWhiteSpace(m.BuyerAddress)) ws.Cell(r, 1).Value = m.BuyerAddress;
        ws.Cell(r, 11).Value = "INVOICE DATE";
        ws.Cell(r++, 12).Value = m.InvoiceDate.ToDateTime(TimeOnly.MinValue);
        ws.Cell(r - 1, 12).Style.DateFormat.Format = "dd.MM.yyyy";
        r++;

        ws.Cell(r, 1).Value = "PACKING LIST";
        ws.Range(r, 1, r, 12).Merge().Style.Font.SetBold().Font.SetFontSize(14).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
        r += 2;

        var head = r;
        string[] cols =
        [
            "PRODUCT CODE", "DESCRIPTION", "HS CODES", "BRAND", "ORIGIN", "QTY",
            "UNIT VOLUME", "TOTAL VOLUME", "UNIT NET WEIGHT", "TOTAL NET WEIGHT",
            "UNIT GROSS WEIGHT", "TOTAL GROSS WEIGHT", "TOTAL QTY",
        ];
        const int n = 13;
        for (var c = 0; c < cols.Length; c++) ws.Cell(head, c + 1).Value = cols[c];
        ws.Range(head, 1, head, n).Style.Font.SetBold().Fill.SetBackgroundColor(Head);
        r++;

        foreach (var line in m.Lines)
        {
            var unitVol = line.Quantity > 0 ? line.VolumeM3 / line.Quantity : 0m;
            var unitNet = line.Quantity > 0 ? line.NetWeightKg / line.Quantity : 0m;
            var unitGross = line.Quantity > 0 ? line.GrossWeightKg / line.Quantity : 0m;
            ws.Cell(r, 1).Value = line.Code;
            ws.Cell(r, 2).Value = line.Description;
            ws.Cell(r, 3).Value = line.HsCode ?? "";
            ws.Cell(r, 4).Value = line.Brand ?? "";
            ws.Cell(r, 5).Value = line.Origin ?? "";
            ws.Cell(r, 6).Value = line.Quantity;
            ws.Cell(r, 7).Value = unitVol;
            ws.Cell(r, 8).Value = line.VolumeM3;
            ws.Cell(r, 9).Value = unitNet;
            ws.Cell(r, 10).Value = line.NetWeightKg;
            ws.Cell(r, 11).Value = unitGross;
            ws.Cell(r, 12).Value = line.GrossWeightKg;
            ws.Cell(r, 13).Value = line.Quantity;
            ws.Cell(r, 6).Style.NumberFormat.Format = Qty;
            ws.Cell(r, 13).Style.NumberFormat.Format = Qty;
            ws.Range(r, 7, r, 8).Style.NumberFormat.Format = M3;
            ws.Range(r, 9, r, 12).Style.NumberFormat.Format = Kg;
            r++;
        }

        ws.Cell(r, 6).Value = m.TotalQuantity;
        ws.Cell(r, 8).Value = m.TotalVolumeM3;
        ws.Cell(r, 10).Value = m.TotalNetWeightKg;
        ws.Cell(r, 12).Value = m.TotalGrossWeightKg;
        ws.Cell(r, 13).Value = m.TotalQuantity;
        ws.Cell(r, 6).Style.NumberFormat.Format = Qty;
        ws.Cell(r, 13).Style.NumberFormat.Format = Qty;
        ws.Cell(r, 8).Style.NumberFormat.Format = M3;
        ws.Range(r, 10, r, 12).Style.NumberFormat.Format = Kg;
        ws.Range(r, 1, r, n).Style.Font.Bold = true;
        var lastRow = r;
        r += 2;

        r = WeightTotals(ws, r, m.TotalGrossWeightKg, m.TotalNetWeightKg, m.TotalQuantity, m.TotalVolumeText);

        ws.Range(head, 1, lastRow, n).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        ws.Range(head, 1, lastRow, n).Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        ws.Columns(1, n).AdjustToContents(8d, 30d);
        ws.Column(2).Width = Math.Max(ws.Column(2).Width, 20);

        return Save(wb);
    }

    private static int WeightTotals(IXLWorksheet ws, int r, decimal gross, decimal net, int qty, string volume)
    {
        void Line(string label, string value)
        {
            ws.Cell(r, 1).Value = label;
            ws.Cell(r, 3).Value = value;
            ws.Row(r).Style.Font.Bold = true;
            r++;
        }
        Line("TOTAL GROSS WEIGHT", $"{DocFormat.Weight(gross)} KGS");
        Line("TOTAL NET WEIGHT", $"{DocFormat.Weight(net)} KGS");
        Line("TOTAL QUANTITY", $"{DocFormat.Count(qty)} PCS");
        Line("TOTAL VOLUME", volume);
        return r;
    }

    private static void BankBlock(IXLWorksheet ws, ref int r, string currency, string? bankText)
    {
        if (string.IsNullOrWhiteSpace(bankText)) return;
        ws.Cell(r++, 1).Value = $"Bank Detail ({currency}):";
        ws.Row(r - 1).Style.Font.Bold = true;
        foreach (var line in bankText.ReplaceLineEndings("\n").Split('\n'))
            ws.Cell(r++, 1).Value = line;
    }

    private static byte[] Save(XLWorkbook wb)
    {
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
