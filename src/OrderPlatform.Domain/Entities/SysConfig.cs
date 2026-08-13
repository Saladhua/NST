namespace OrderPlatform.Domain.Entities;

public class SysConfig
{
    public Guid Id { get; set; }

    public string ConfigKey { get; set; } = string.Empty;

    public string ConfigValue { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
