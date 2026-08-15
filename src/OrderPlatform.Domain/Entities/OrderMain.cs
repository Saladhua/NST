using OrderPlatform.Domain.Enums;

namespace OrderPlatform.Domain.Entities;

/// <summary>订单主表实体，由 PDF 采购订单解析生成。</summary>
public class OrderMain
{
    /// <summary>订单唯一标识。</summary>
    public Guid Id { get; set; }

    /// <summary>订单号（来自 PDF，唯一，重复时跳过导入）。</summary>
    public string OrderNo { get; set; } = string.Empty;

    /// <summary>关联客户（按 PDF 采购方名识别）。</summary>
    public Guid CustomerId { get; set; }

    /// <summary>订单日期。</summary>
    public DateTime? OrderDate { get; set; }

    /// <summary>总数量（明细数量之和）。</summary>
    public decimal TotalQuantity { get; set; }

    /// <summary>总金额（明细金额之和）。</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>来源上传批次（即 PDF 批次）。</summary>
    public Guid? SourceFileId { get; set; }

    /// <summary>关联状态：Matched / Partial / Unmatched。</summary>
    public MatchStatus ParseStatus { get; set; }

    /// <summary>推送状态：NotPushed / Pushed / Failed。</summary>
    public PushStatus PushStatus { get; set; }

    /// <summary>PDF 解析结果原始 JSON（供补匹配时重新使用）。</summary>
    public string? PdfRawJson { get; set; }

    /// <summary>创建时间。</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>订单明细。</summary>
    public List<OrderItem> Items { get; set; } = new();
}