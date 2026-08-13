using OrderPlatform.Domain.Entities;

namespace OrderPlatform.Domain.Interfaces;

public interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Customer?> GetByNameAsync(string name, CancellationToken cancellationToken);

    Task<List<Customer>> ListAsync(CancellationToken cancellationToken);

    Task AddAsync(Customer customer, CancellationToken cancellationToken);

    void Update(Customer customer);

    Task SoftDeleteAsync(Guid id, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
