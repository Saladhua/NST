using OrderPlatform.Domain.Enums;

namespace OrderPlatform.Application.Upload.Dtos;

/// <summary>上传 Excel 后注册的客户资料。</summary>
public class CustomerImportDto
{
    public Guid CustomerId { get; set; }

    public string CustomerName { get; set; } = string.Empty;

    public int PartCount { get; set; }
}

/// <summary>上传 PDF 后自动关联生成的订单。</summary>
public class OrderGeneratedDto
{
    public Guid OrderId { get; set; }

    public string OrderNo { get; set; } = string.Empty;

    public Guid? CustomerId { get; set; }

    public string? CustomerName { get; set; }

    public int ItemCount { get; set; }

    public MatchStatus ParseStatus { get; set; }

    public List<MatchResultItem> Items { get; set; } = new();
}

public class MatchResultItem
{
    public int LineNo { get; set; }

    public string MaterialCode { get; set; } = string.Empty;

    public string Spec { get; set; } = string.Empty;

    public decimal Quantity { get; set; }

    public string Unit { get; set; } = string.Empty;

    public string? CustomerPartNo { get; set; }

    public string? NestPartNo { get; set; }

    public MatchStatus MatchStatus { get; set; }
}
