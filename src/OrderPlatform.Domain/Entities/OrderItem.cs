using OrderPlatform.Domain.Enums;

namespace OrderPlatform.Domain.Entities;

public class OrderItem
{
    public Guid Id { get; set; }

    public Guid OrderId { get; set; }

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

    public DateTime CreatedAt { get; set; }
}
