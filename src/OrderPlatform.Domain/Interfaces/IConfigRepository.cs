using OrderPlatform.Domain.Entities;

namespace OrderPlatform.Domain.Interfaces;

/// <summary>系统配置仓储接口。</summary>
public interface IConfigRepository
{
    /// <summary>查询全部配置（按键排序）。</summary>
    Task<List<SysConfig>> ListAsync(CancellationToken cancellationToken);

    /// <summary>按 ID 查询配置。</summary>
    Task<SysConfig?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>按键查询配置。</summary>
    Task<SysConfig?> GetByKeyAsync(string key, CancellationToken cancellationToken);

    /// <summary>更新配置。</summary>
    void Update(SysConfig config);

    /// <summary>保存变更。</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}