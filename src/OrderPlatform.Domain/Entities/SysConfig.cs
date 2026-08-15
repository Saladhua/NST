namespace OrderPlatform.Domain.Entities;

/// <summary>系统配置实体（键值对，管理员维护）。</summary>
public class SysConfig
{
    /// <summary>配置唯一标识。</summary>
    public Guid Id { get; set; }

    /// <summary>配置键（唯一）。</summary>
    public string ConfigKey { get; set; } = string.Empty;

    /// <summary>配置值。</summary>
    public string ConfigValue { get; set; } = string.Empty;

    /// <summary>配置说明。</summary>
    public string? Description { get; set; }

    /// <summary>更新时间。</summary>
    public DateTime? UpdatedAt { get; set; }
}