using Microsoft.EntityFrameworkCore;
using OrderPlatform.Domain.Entities;
using OrderPlatform.Domain.Interfaces;
using OrderPlatform.Infrastructure.Persistence;

namespace OrderPlatform.Infrastructure.Repositories;

/// <summary>客户图号资料仓储实现。</summary>
public class CustomerPartRepository : ICustomerPartRepository
{
    private readonly OrderDbContext _dbContext;

    public CustomerPartRepository(OrderDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>查询某客户的全部图号资料。</summary>
    public Task<List<CustomerPart>> ListByCustomerAsync(Guid customerId, CancellationToken cancellationToken)
    {
        return _dbContext.CustomerParts
            .Where(x => x.CustomerId == customerId)
            .ToListAsync(cancellationToken);
    }

    /// <summary>删除某客户的全部图号资料（Excel 重传时重建）。</summary>
    public async Task DeleteByCustomerAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var parts = await _dbContext.CustomerParts
            .Where(x => x.CustomerId == customerId)
            .ToListAsync(cancellationToken);
        _dbContext.CustomerParts.RemoveRange(parts);
    }

    /// <summary>批量新增图号资料。</summary>
    public async Task AddRangeAsync(List<CustomerPart> parts, CancellationToken cancellationToken)
    {
        await _dbContext.CustomerParts.AddRangeAsync(parts, cancellationToken);
    }

    /// <summary>按 ID 查询图号资料。</summary>
    public Task<CustomerPart?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.CustomerParts.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    /// <summary>更新图号资料。</summary>
    public void Update(CustomerPart part)
    {
        _dbContext.CustomerParts.Update(part);
    }

    /// <summary>保存变更。</summary>
    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}