using OrderPlatform.Domain.Enums;

namespace OrderPlatform.Application.Upload.Dtos;

/// <summary>上传 Excel 后注册的客户资料。</summary>
public class CustomerImportDto
{
    /// <summary>客户 ID。</summary>
    public Guid CustomerId { get; set; }

    /// <summary>客户名称。</summary>
    public string CustomerName { get; set; } = string.Empty;

    /// <summary>已匹配的去重图号数量（按订单明细统计）。</summary>
    public int PartCount { get; set; }
}

/// <summary>上传 PDF 后自动关联生成的订单。</summary>
public class OrderGeneratedDto
{
    /// <summary>订单 ID。</summary>
    public Guid OrderId { get; set; }

    /// <summary>订单号。</summary>
    public string OrderNo { get; set; } = string.Empty;

    /// <summary>客户 ID。</summary>
    public Guid? CustomerId { get; set; }

    /// <summary>客户名称。</summary>
    public string? CustomerName { get; set; }

    /// <summary>明细行数。</summary>
    public int ItemCount { get; set; }

    /// <summary>关联状态。</summary>
    public MatchStatus ParseStatus { get; set; }

    /// <summary>明细行（含匹配结果）。</summary>
    public List<MatchResultItem> Items { get; set; } = new();
}

/// <summary>PDF 明细行的匹配结果。</summary>
public class MatchResultItem
{
    /// <summary>行号。</summary>
    public int LineNo { get; set; }

    /// <summary>物料编码。</summary>
    public string MaterialCode { get; set; } = string.Empty;

    /// <summary>规格。</summary>
    public string Spec { get; set; } = string.Empty;

    /// <summary>数量。</summary>
    public decimal Quantity { get; set; }

    /// <summary>单位。</summary>
    public string Unit { get; set; } = string.Empty;

    /// <summary>匹配到的客户图号。</summary>
    public string? CustomerPartNo { get; set; }

    /// <summary>匹配到的 NEST 图号。</summary>
    public string? NestPartNo { get; set; }

    /// <summary>匹配状态。</summary>
    public MatchStatus MatchStatus { get; set; }
}