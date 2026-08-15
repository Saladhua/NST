using OrderPlatform.Domain.Enums;

namespace OrderPlatform.Application.Orders;

/// <summary>分页结果通用包装。</summary>
public class PagedResult<T>
{
    /// <summary>当前页数据。</summary>
    public List<T> Items { get; set; } = new();

    /// <summary>总条数。</summary>
    public int Total { get; set; }

    public PagedResult()
    {
    }

    public PagedResult(List<T> items, int total)
    {
        Items = items;
        Total = total;
    }
}

/// <summary>订单列表项。</summary>
public class OrderListDto
{
    /// <summary>订单 ID。</summary>
    public Guid Id { get; set; }

    /// <summary>订单号。</summary>
    public string OrderNo { get; set; } = string.Empty;

    /// <summary>关联客户 ID。</summary>
    public Guid CustomerId { get; set; }

    /// <summary>关联客户名称。</summary>
    public string CustomerName { get; set; } = string.Empty;

    /// <summary>订单日期。</summary>
    public DateTime? OrderDate { get; set; }

    /// <summary>总数量。</summary>
    public decimal TotalQuantity { get; set; }

    /// <summary>总金额。</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>关联状态。</summary>
    public MatchStatus ParseStatus { get; set; }

    /// <summary>推送状态。</summary>
    public PushStatus PushStatus { get; set; }

    /// <summary>创建时间。</summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>订单详情（列表项 + 来源批次 + 明细行）。</summary>
public class OrderDetailDto : OrderListDto
{
    /// <summary>来源上传批次。</summary>
    public Guid? SourceFileId { get; set; }

    /// <summary>订单明细行。</summary>
    public List<OrderItemDto> Items { get; set; } = new();
}

/// <summary>订单明细行。</summary>
public class OrderItemDto
{
    /// <summary>明细 ID。</summary>
    public Guid Id { get; set; }

    /// <summary>行号。</summary>
    public int LineNo { get; set; }

    /// <summary>物料编码。</summary>
    public string MaterialCode { get; set; } = string.Empty;

    /// <summary>物料名称。</summary>
    public string MaterialName { get; set; } = string.Empty;

    /// <summary>规格。</summary>
    public string Spec { get; set; } = string.Empty;

    /// <summary>匹配到的客户图号。</summary>
    public string CustomerPartNo { get; set; } = string.Empty;

    /// <summary>匹配到的 NEST 图号。</summary>
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

    /// <summary>匹配状态。</summary>
    public MatchStatus MatchStatus { get; set; }
}

/// <summary>订单推送结果。</summary>
public class PushResultDto
{
    /// <summary>推送日志 ID。</summary>
    public Guid LogId { get; set; }

    /// <summary>订单 ID。</summary>
    public Guid OrderId { get; set; }

    /// <summary>推送状态。</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>推送时间。</summary>
    public DateTime PushTime { get; set; }
}