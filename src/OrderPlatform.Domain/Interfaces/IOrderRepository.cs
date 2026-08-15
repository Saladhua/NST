using OrderPlatform.Domain.Entities;
using OrderPlatform.Domain.Enums;

namespace OrderPlatform.Domain.Interfaces;

/// <summary>订单仓储接口，定义订单及其明细、推送日志的数据操作。</summary>
public interface IOrderRepository
{
    /// <summary>按 ID 查询订单（含明细）。</summary>
    Task<OrderMain?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>按订单号查询订单（含明细），用于重复导入去重。</summary>
    Task<OrderMain?> GetByOrderNoAsync(string orderNo, CancellationToken cancellationToken);

    /// <summary>按来源批次查询订单（含明细）。</summary>
    Task<List<OrderMain>> ListBySourceFileIdAsync(Guid sourceFileId, CancellationToken cancellationToken);

    /// <summary>按来源批次批量统计各批次生成的订单数。</summary>
    Task<Dictionary<Guid, int>> CountBySourceFileIdsAsync(
        IEnumerable<Guid> sourceFileIds,
        CancellationToken cancellationToken);

    /// <summary>查询未完全关联的订单（用于 Excel 导入后补匹配）。</summary>
    Task<List<OrderMain>> ListPendingMatchAsync(CancellationToken cancellationToken);

    /// <summary>分页查询订单（支持关键词/客户/推送状态/关联状态筛选）。</summary>
    Task<List<OrderMain>> QueryAsync(
        int page,
        int pageSize,
        string? keyword,
        Guid? customerId,
        PushStatus? pushStatus,
        MatchStatus? parseStatus,
        CancellationToken cancellationToken);

    /// <summary>统计筛选条件下的订单总数。</summary>
    Task<int> CountAsync(
        string? keyword,
        Guid? customerId,
        PushStatus? pushStatus,
        MatchStatus? parseStatus,
        CancellationToken cancellationToken);

    /// <summary>新增订单（含明细）。</summary>
    Task AddAsync(OrderMain order, CancellationToken cancellationToken);

    /// <summary>更新订单。</summary>
    void Update(OrderMain order);

    /// <summary>删除订单，并联动清理其明细与推送日志。</summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>按客户统计订单明细中已匹配（非空客户图号）的去重图号数量。</summary>
    Task<Dictionary<Guid, int>> CountMatchedPartNosByCustomersAsync(
        IEnumerable<Guid> customerIds,
        CancellationToken cancellationToken);

    /// <summary>保存变更。</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}