namespace OrderPlatform.Domain.Entities;

public class CustomerPart
{
    public Guid Id { get; set; }

    public Guid CustomerId { get; set; }

    /// <summary>NEST 图号。</summary>
    public string NestPartNo { get; set; } = string.Empty;

    /// <summary>客户图号 / 客户新图号。</summary>
    public string CustomerPartNo { get; set; } = string.Empty;

    public string Spray { get; set; } = string.Empty;

    public string Alloy { get; set; } = string.Empty;

    public string Spec { get; set; } = string.Empty;

    public decimal? Length { get; set; }

    /// <summary>原始整行文本（便于人工核对）。</summary>
    public string Raw { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}