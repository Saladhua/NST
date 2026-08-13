using OrderPlatform.Domain.Enums;

namespace OrderPlatform.Application.Orders;

public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();

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

public class OrderListDto
{
    public Guid Id { get; set; }

    public string OrderNo { get; set; } = string.Empty;

    public Guid CustomerId { get; set; }

    public string CustomerName { get; set; } = string.Empty;

    public DateTime? OrderDate { get; set; }

    public decimal TotalQuantity { get; set; }

    public decimal TotalAmount { get; set; }

    public MatchStatus ParseStatus { get; set; }

    public PushStatus PushStatus { get; set; }

    public DateTime CreatedAt { get; set; }
}

public class OrderDetailDto : OrderListDto
{
    public Guid? SourceFileId { get; set; }

    public List<OrderItemDto> Items { get; set; } = new();
}

public class OrderItemDto
{
    public Guid Id { get; set; }

    public int LineNo { get; set; }

    public string MaterialCode { get; set; } = string.Empty;

    public string MaterialName { get; set; } = string.Empty;

    public string Spec { get; set; } = string.Empty;

    public string CustomerPartNo { get; set; } = string.Empty;

    public string NestPartNo { get; set; } = string.Empty;

    public string Alloy { get; set; } = string.Empty;

    public string Spray { get; set; } = string.Empty;

    public decimal? Length { get; set; }

    public decimal Quantity { get; set; }

    public string Unit { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public decimal Amount { get; set; }

    public DateTime? ReceiveDate { get; set; }

    public MatchStatus MatchStatus { get; set; }
}

public class PushResultDto
{
    public Guid LogId { get; set; }

    public Guid OrderId { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTime PushTime { get; set; }
}
