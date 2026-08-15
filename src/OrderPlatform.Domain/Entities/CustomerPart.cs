namespace OrderPlatform.Domain.Entities;

/// <summary>客户图号资料（NEST 套图图号与客户图号的对应关系），由 Excel 客户资料导入生成。</summary>
public class CustomerPart
{
    /// <summary>图号资料唯一标识。</summary>
    public Guid Id { get; set; }

    /// <summary>所属客户。</summary>
    public Guid CustomerId { get; set; }

    /// <summary>NEST 图号。</summary>
    public string NestPartNo { get; set; } = string.Empty;

    /// <summary>客户图号 / 客户新图号。</summary>
    public string CustomerPartNo { get; set; } = string.Empty;

    /// <summary>喷锌。</summary>
    public string Spray { get; set; } = string.Empty;

    /// <summary>合金。</summary>
    public string Alloy { get; set; } = string.Empty;

    /// <summary>规格（如 16*1.4），用于按「外径×壁厚」前缀匹配。</summary>
    public string Spec { get; set; } = string.Empty;

    /// <summary>长度（mm）。</summary>
    public decimal? Length { get; set; }

    /// <summary>原始整行文本（便于人工核对）。</summary>
    public string Raw { get; set; } = string.Empty;

    /// <summary>创建时间。</summary>
    public DateTime CreatedAt { get; set; }
}