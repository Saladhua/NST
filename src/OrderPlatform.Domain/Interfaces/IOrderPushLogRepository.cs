using OrderPlatform.Domain.Entities;

namespace OrderPlatform.Domain.Interfaces;

/// <summary>订单推送日志仓储接口。</summary>
public interface IOrderPushLogRepository
{
    /// <summary>新增推送日志。</summary>
    Task AddAsync(OrderPushLog log, CancellationToken cancellationToken);

    /// <summary>保存变更。</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}