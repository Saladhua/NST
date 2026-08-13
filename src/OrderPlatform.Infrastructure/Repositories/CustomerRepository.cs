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
        return _dbContext.Customers.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
    }

    public Task<Customer?> GetByNameAsync(string name, CancellationToken cancellationToken)
    {
        return _dbContext.Customers.FirstOrDefaultAsync(x => x.Name == name && !x.IsDeleted, cancellationToken);
    }

    public Task<List<Customer>> ListAsync(CancellationToken cancellationToken)
    {
        return _dbContext.Customers.Where(x => !x.IsDeleted).OrderBy(x => x.Name).ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Customer customer, CancellationToken cancellationToken)
    {
        await _dbContext.Customers.AddAsync(customer, cancellationToken);
    }

    public void Update(Customer customer)
    {
        _dbContext.Customers.Update(customer);
    }

    public async Task SoftDeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var customer = await _dbContext.Customers.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
        if (customer is not null)
        {
            customer.IsDeleted = true;
            customer.UpdatedAt = DateTime.Now;
        }
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
