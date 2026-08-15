using OrderPlatform.Domain.Enums;

namespace OrderPlatform.Domain.Entities;

/// <summary>订单明细实体（订单中的每一行物料）。</summary>
public class OrderItem
{
    /// <summary>明细唯一标识。</summary>
    public Guid Id { get; set; }

    /// <summary>所属订单。</summary>
    public Guid OrderId { get; set; }

    /// <summary>行号（PDF 表体中的顺序）。</summary>
    public int LineNo { get; set; }

    /// <summary>存货编码（如 03.02.16*1.4*603/D97/1100 或 H110100030）。</summary>
    public string MaterialCode { get; set; } = string.Empty;

    /// <summary>存货名称。</summary>
    public string MaterialName { get; set; } = string.Empty;

    /// <summary>规格型号。</summary>
    public string Spec { get; set; } = string.Empty;

    /// <summary>匹配到的客户图号。</summary>
    public string CustomerPartNo { get; set; } = string.Empty;

    /// <summary>匹配到的 NEST 套图图号。</summary>
    public string NestPartNo { get; set; } = string.Empty;

    /// <summary>合金。</summary>
    public string Alloy { get; set; } = string.Empty;

    /// <summary>喷锌。</summary>
    public string Spray { get; set; } = string.Empty;

    /// <summary>长度（mm）。</summary>
    public decimal? Length { get; set; }

    /// <summary>数量。</summary>
    public decimal Quantity { get; set; }

    /// <summary>单位。</summary>
    public string Unit { get; set; } = string.Empty;

    /// <summary>单价。</summary>
    public decimal Price { get; set; }

    /// <summary>金额。</summary>
    public decimal Amount { get; set; }

    /// <summary>交货日期。</summary>
    public DateTime? ReceiveDate { get; set; }

    /// <summary>匹配状态：Matched / Partial / Unmatched。</summary>
    public MatchStatus MatchStatus { get; set; }

    /// <summary>创建时间。</summary>
    public DateTime CreatedAt { get; set; }
}