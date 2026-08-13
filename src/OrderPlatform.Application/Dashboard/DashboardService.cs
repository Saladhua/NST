using OrderPlatform.Domain.Enums;
using OrderPlatform.Domain.Interfaces;

namespace OrderPlatform.Application.Dashboard;

public interface IDashboardService
{
    Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken);
}

public class DashboardService : IDashboardService
{
    private readonly IDashboardRepository _dashboardRepository;

    public DashboardService(IDashboardRepository dashboardRepository)
    {
        _dashboardRepository = dashboardRepository;
    }

    public async Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken)
    {
        var today = DateTime.Today;
        var orders = await _dashboardRepository.GetAllOrdersAsync(cancellationToken);
        var customers = await _dashboardRepository.GetAllCustomersAsync(cancellationToken);

        var customerMap = customers.ToDictionary(c => c.Id, c => c.Name);

        var customerOrderStats = orders
            .GroupBy(o => o.CustomerId)
            .Select(g => new CustomerOrderStatDto
            {
                CustomerId = g.Key,
                CustomerName = customerMap.TryGetValue(g.Key, out var name) ? name : string.Empty,
                OrderCount = g.Count(),
                TotalAmount = g.Sum(o => o.TotalAmount)
            })
            .OrderByDescending(x => x.OrderCount)
            .ToList();

        var recentOrders = orders
            .OrderByDescending(o => o.CreatedAt)
            .Take(10)
            .Select(o => new RecentOrderDto
            {
                Id = o.Id,
                OrderNo = o.OrderNo,
                CustomerName = customerMap.TryGetValue(o.CustomerId, out var name) ? name : string.Empty,
                TotalAmount = o.TotalAmount,
                CreatedAt = o.CreatedAt
            })
            .ToList();

        return new DashboardSummaryDto
        {
            TotalOrders = orders.Count,
            TodayOrders = orders.Count(o => o.CreatedAt >= today),
            TotalAmount = orders.Sum(o => o.TotalAmount),
            PendingPush = orders.Count(o => o.PushStatus == PushStatus.NotPushed),
            TotalCustomers = customers.Count,
            CustomerOrderStats = customerOrderStats,
            RecentOrders = recentOrders
        };
    }
}

public class DashboardSummaryDto
{
    public int TotalOrders { get; set; }

    public int TodayOrders { get; set; }

    public decimal TotalAmount { get; set; }

    public int PendingPush { get; set; }

    public int TotalCustomers { get; set; }

    public List<CustomerOrderStatDto> CustomerOrderStats { get; set; } = new();

    public List<RecentOrderDto> RecentOrders { get; set; } = new();
}

public class CustomerOrderStatDto
{
    public Guid CustomerId { get; set; }

    public string CustomerName { get; set; } = string.Empty;

    public int OrderCount { get; set; }

    public decimal TotalAmount { get; set; }
}

public class RecentOrderDto
{
    public Guid Id { get; set; }

    public string OrderNo { get; set; } = string.Empty;

    public string CustomerName { get; set; } = string.Empty;

    public decimal TotalAmount { get; set; }

    public DateTime CreatedAt { get; set; }
}
