namespace OrderPlatform.Domain.Entities;

/// <summary>客户实体。客户来源于 Excel 客户资料的 sheet 名，sheet 名即客户名。</summary>
public class Customer
{
    /// <summary>客户唯一标识。</summary>
    public Guid Id { get; set; }

    /// <summary>客户名称（唯一，来自 Excel sheet 名或 PDF 采购方名）。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>客户编码。</summary>
    public string? Code { get; set; }

    /// <summary>备注。</summary>
    public string? Remark { get; set; }

    /// <summary>是否已删除（软删除）。</summary>
    public bool IsDeleted { get; set; }

    /// <summary>创建时间。</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>更新时间。</summary>
    public DateTime? UpdatedAt { get; set; }
}