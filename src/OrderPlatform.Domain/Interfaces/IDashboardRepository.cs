using OrderPlatform.Domain.Entities;

namespace OrderPlatform.Domain.Interfaces;

public interface IDashboardRepository
{
    Task<List<OrderMain>> GetAllOrdersAsync(CancellationToken cancellationToken);

    Task<List<Customer>> GetAllCustomersAsync(CancellationToken cancellationToken);
}
