using Microsoft.EntityFrameworkCore;
using OrderPlatform.Domain.Entities;
using OrderPlatform.Domain.Interfaces;
using OrderPlatform.Infrastructure.Persistence;

namespace OrderPlatform.Infrastructure.Repositories;

/// <summary>客户仓储实现（查询均过滤已软删除的客户）。</summary>
public class CustomerRepository : ICustomerRepository
{
    private readonly OrderDbContext _dbContext;

    public CustomerRepository(OrderDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>按 ID 查询客户（未删除）。</summary>
    public Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.Customers.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
    }

    /// <summary>按名称精确查询客户（未删除）。</summary>
    public Task<Customer?> GetByNameAsync(string name, CancellationToken cancellationToken)
    {
        return _dbContext.Customers.FirstOrDefaultAsync(x => x.Name == name && !x.IsDeleted, cancellationToken);
    }

    /// <summary>查询全部未删除客户（按名称排序）。</summary>
    public Task<List<Customer>> ListAsync(CancellationToken cancellationToken)
    {
        return _dbContext.Customers.Where(x => !x.IsDeleted).OrderBy(x => x.Name).ToListAsync(cancellationToken);
    }

    /// <summary>新增客户。</summary>
    public async Task AddAsync(Customer customer, CancellationToken cancellationToken)
    {
        await _dbContext.Customers.AddAsync(customer, cancellationToken);
    }

    /// <summary>更新客户。</summary>
    public void Update(Customer customer)
    {
        _dbContext.Customers.Update(customer);
    }

    /// <summary>软删除客户（仅标记 IsDeleted，保留历史数据）。</summary>
    public async Task SoftDeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var customer = await _dbContext.Customers.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
        if (customer is not null)
        {
            customer.IsDeleted = true;
            customer.UpdatedAt = DateTime.Now;
        }
    }

    /// <summary>保存变更。</summary>
    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}