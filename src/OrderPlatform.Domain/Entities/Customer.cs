namespace OrderPlatform.Domain.Entities;

public class Customer
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Code { get; set; }

    public string? Remark { get; set; }

    /// <summary>是否已删除（软删除）。</summary>
    public bool IsDeleted { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
