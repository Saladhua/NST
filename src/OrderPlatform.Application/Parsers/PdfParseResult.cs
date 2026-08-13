namespace OrderPlatform.Application.Parsers;

/// <summary>
/// PDF 采购订单解析结果。
/// </summary>
public class PdfParseResult
{
    public string OrderNo { get; set; } = string.Empty;

    public DateTime? OrderDate { get; set; }

    public string BuyerName { get; set; } = string.Empty;

    public string SupplierName { get; set; } = string.Empty;

    public List<string> Headers { get; set; } = new();

    public List<PdfParseRow> Rows { get; set; } = new();

    public string RawText { get; set; } = string.Empty;
}

public class PdfParseRow
{
    public int LineNo { get; set; }

    /// <summary>存货编码（如 03.02.16*1.4*603/D97/1100 或 H110100030）。</summary>
    public string MaterialCode { get; set; } = string.Empty;

    public string MaterialName { get; set; } = string.Empty;

    public string Spec { get; set; } = string.Empty;

    public string Unit { get; set; } = string.Empty;

    public decimal Quantity { get; set; }

    public decimal Price { get; set; }

    public decimal Amount { get; set; }

    public string Remark { get; set; } = string.Empty;

    public DateTime? ReceiveDate { get; set; }

    /// <summary>原始行文本，用于人工核对。</summary>
    public string Raw { get; set; } = string.Empty;
}
