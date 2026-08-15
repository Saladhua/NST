using OrderPlatform.Domain.Entities;

namespace OrderPlatform.Domain.Interfaces;

/// <summary>客户图号资料仓储接口。</summary>
public interface ICustomerPartRepository
{
    /// <summary>查询某客户的全部图号资料。</summary>
    Task<List<CustomerPart>> ListByCustomerAsync(Guid customerId, CancellationToken cancellationToken);

    /// <summary>删除某客户的全部图号资料（Excel 重传时重建）。</summary>
    Task DeleteByCustomerAsync(Guid customerId, CancellationToken cancellationToken);

    /// <summary>批量新增图号资料。</summary>
    Task AddRangeAsync(List<CustomerPart> parts, CancellationToken cancellationToken);

    /// <summary>按 ID 查询图号资料。</summary>
    Task<CustomerPart?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>更新图号资料。</summary>
    void Update(CustomerPart part);

    /// <summary>保存变更。</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}