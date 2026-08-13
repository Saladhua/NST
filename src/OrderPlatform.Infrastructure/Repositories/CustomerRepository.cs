using Microsoft.EntityFrameworkCore;
using OrderPlatform.Domain.Entities;
using OrderPlatform.Domain.Interfaces;
using OrderPlatform.Infrastructure.Persistence;

namespace OrderPlatform.Infrastructure.Repositories;

public class CustomerRepository : ICustomerRepository
{
    private readonly OrderDbContext _dbContext;

    public CustomerRepository(OrderDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.Customers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task<Customer?> GetByNameAsync(string name, CancellationToken cancellationToken)
    {
        return _dbContext.Customers.FirstOrDefaultAsync(x => x.Name == name, cancellationToken);
    }

    public Task<List<Customer>> ListAsync(CancellationToken cancellationToken)
    {
        return _dbContext.Customers.OrderBy(x => x.Name).ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Customer customer, CancellationToken cancellationToken)
    {
        await _dbContext.Customers.AddAsync(customer, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
