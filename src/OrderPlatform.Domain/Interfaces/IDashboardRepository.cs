using OrderPlatform.Domain.Entities;

namespace OrderPlatform.Domain.Interfaces;

/// <summary>数据看板仓储接口，提供看板统计所需的原始数据。</summary>
public interface IDashboardRepository
{
    /// <summary>查询全部订单。</summary>
    Task<List<OrderMain>> GetAllOrdersAsync(CancellationToken cancellationToken);

    /// <summary>查询全部未删除客户。</summary>
    Task<List<Customer>> GetAllCustomersAsync(CancellationToken cancellationToken);
}