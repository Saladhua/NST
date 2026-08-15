using OrderPlatform.Domain.Entities;
using OrderPlatform.Domain.Interfaces;
using OrderPlatform.Shared.Api;

namespace OrderPlatform.Application.Config;

/// <summary>系统配置服务接口。</summary>
public interface IConfigService
{
    /// <summary>查询全部配置。</summary>
    Task<List<ConfigItemDto>> ListAsync(CancellationToken cancellationToken);

    /// <summary>更新配置值。</summary>
    Task UpdateAsync(Guid id, UpdateConfigRequest request, CancellationToken cancellationToken);
}

/// <summary>系统配置服务实现。</summary>
public class ConfigService : IConfigService
{
    private readonly IConfigRepository _configRepository;

    public ConfigService(IConfigRepository configRepository)
    {
        _configRepository = configRepository;
    }

    /// <summary>查询全部配置。</summary>
    public async Task<List<ConfigItemDto>> ListAsync(CancellationToken cancellationToken)
    {
        var configs = await _configRepository.ListAsync(cancellationToken);
        return configs.Select(c => new ConfigItemDto
        {
            Id = c.Id,
            ConfigKey = c.ConfigKey,
            ConfigValue = c.ConfigValue,
            Description = c.Description,
            UpdatedAt = c.UpdatedAt
        }).ToList();
    }

    /// <summary>更新配置值并记录更新时间。</summary>
    public async Task UpdateAsync(Guid id, UpdateConfigRequest request, CancellationToken cancellationToken)
    {
        var config = await _configRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new BusinessException("配置不存在");

        config.ConfigValue = request.ConfigValue.Trim();
        config.UpdatedAt = DateTime.Now;
        _configRepository.Update(config);
        await _configRepository.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>配置项。</summary>
public class ConfigItemDto
{
    /// <summary>配置 ID。</summary>
    public Guid Id { get; set; }

    /// <summary>配置键。</summary>
    public string ConfigKey { get; set; } = string.Empty;

    /// <summary>配置值。</summary>
    public string ConfigValue { get; set; } = string.Empty;

    /// <summary>说明。</summary>
    public string? Description { get; set; }

    /// <summary>更新时间。</summary>
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>更新配置请求。</summary>
public class UpdateConfigRequest
{
    /// <summary>新的配置值。</summary>
    public string ConfigValue { get; set; } = string.Empty;
}