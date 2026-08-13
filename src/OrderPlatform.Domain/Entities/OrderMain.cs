using OrderPlatform.Domain.Enums;

namespace OrderPlatform.Domain.Entities;

public class OrderMain
{
    public Guid Id { get; set; }

    public string OrderNo { get; set; } = string.Empty;

    public Guid CustomerId { get; set; }

    public DateTime? OrderDate { get; set; }

    public decimal TotalQuantity { get; set; }

    public decimal TotalAmount { get; set; }

    public Guid? SourceFileId { get; set; }

    public MatchStatus ParseStatus { get; set; }

    public PushStatus PushStatus { get; set; }

    public string? PdfRawJson { get; set; }

    public DateTime CreatedAt { get; set; }

    public List<OrderItem> Items { get; set; } = new();
}
