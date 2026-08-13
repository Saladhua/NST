using OrderPlatform.Domain.Entities;
using OrderPlatform.Domain.Interfaces;
using OrderPlatform.Shared.Api;

namespace OrderPlatform.Application.Config;

public interface IConfigService
{
    Task<List<ConfigItemDto>> ListAsync(CancellationToken cancellationToken);

    Task UpdateAsync(Guid id, UpdateConfigRequest request, CancellationToken cancellationToken);
}

public class ConfigService : IConfigService
{
    private readonly IConfigRepository _configRepository;

    public ConfigService(IConfigRepository configRepository)
    {
        _configRepository = configRepository;
    }

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

public class ConfigItemDto
{
    public Guid Id { get; set; }

    public string ConfigKey { get; set; } = string.Empty;

    public string ConfigValue { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateTime? UpdatedAt { get; set; }
}

public class UpdateConfigRequest
{
    public string ConfigValue { get; set; } = string.Empty;
}
