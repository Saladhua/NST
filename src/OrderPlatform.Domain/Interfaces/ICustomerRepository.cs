using OrderPlatform.Domain.Entities;

namespace OrderPlatform.Domain.Interfaces;

/// <summary>客户仓储接口。查询均过滤已软删除（IsDeleted）的客户。</summary>
public interface ICustomerRepository
{
    /// <summary>按 ID 查询客户（未删除）。</summary>
    Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>按名称精确查询客户（未删除）。</summary>
    Task<Customer?> GetByNameAsync(string name, CancellationToken cancellationToken);

    /// <summary>查询全部未删除客户（按名称排序）。</summary>
    Task<List<Customer>> ListAsync(CancellationToken cancellationToken);

    /// <summary>新增客户。</summary>
    Task AddAsync(Customer customer, CancellationToken cancellationToken);

    /// <summary>更新客户。</summary>
    void Update(Customer customer);

    /// <summary>软删除客户（保留历史订单关联与图号数据）。</summary>
    Task SoftDeleteAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>保存变更。</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}