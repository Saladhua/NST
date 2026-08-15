namespace OrderPlatform.Application.Parsers;

/// <summary>
/// PDF 采购订单解析结果。
/// </summary>
public class PdfParseResult
{
    /// <summary>订单号。</summary>
    public string OrderNo { get; set; } = string.Empty;

    /// <summary>订单日期。</summary>
    public DateTime? OrderDate { get; set; }

    /// <summary>采购方（客户）名称。</summary>
    public string BuyerName { get; set; } = string.Empty;

    /// <summary>供应商名称。</summary>
    public string SupplierName { get; set; } = string.Empty;

    /// <summary>表头列表。</summary>
    public List<string> Headers { get; set; } = new();

    /// <summary>解析出的明细行。</summary>
    public List<PdfParseRow> Rows { get; set; } = new();

    /// <summary>全部页面的原始文本（供人工核对）。</summary>
    public string RawText { get; set; } = string.Empty;
}

/// <summary>PDF 明细行解析结果。</summary>
public class PdfParseRow
{
    /// <summary>行号。</summary>
    public int LineNo { get; set; }

    /// <summary>存货编码（如 03.02.16*1.4*603/D97/1100 或 H110100030）。</summary>
    public string MaterialCode { get; set; } = string.Empty;

    /// <summary>存货名称。</summary>
    public string MaterialName { get; set; } = string.Empty;

    /// <summary>规格型号。</summary>
    public string Spec { get; set; } = string.Empty;

    /// <summary>单位。</summary>
    public string Unit { get; set; } = string.Empty;

    /// <summary>数量。</summary>
    public decimal Quantity { get; set; }

    /// <summary>单价。</summary>
    public decimal Price { get; set; }

    /// <summary>金额。</summary>
    public decimal Amount { get; set; }

    /// <summary>备注。</summary>
    public string Remark { get; set; } = string.Empty;

    /// <summary>交货日期。</summary>
    public DateTime? ReceiveDate { get; set; }

    /// <summary>原始行文本，用于人工核对。</summary>
    public string Raw { get; set; } = string.Empty;
}