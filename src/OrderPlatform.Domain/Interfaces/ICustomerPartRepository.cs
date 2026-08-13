using OrderPlatform.Domain.Entities;

namespace OrderPlatform.Domain.Interfaces;

public interface ICustomerPartRepository
{
    Task<List<CustomerPart>> ListByCustomerAsync(Guid customerId, CancellationToken cancellationToken);

    Task<Dictionary<Guid, int>> CountGroupByCustomerAsync(IEnumerable<Guid> customerIds, CancellationToken cancellationToken);

    Task DeleteByCustomerAsync(Guid customerId, CancellationToken cancellationToken);

    Task AddRangeAsync(List<CustomerPart> parts, CancellationToken cancellationToken);

    Task<CustomerPart?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    void Update(CustomerPart part);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
