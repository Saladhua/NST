using OrderPlatform.Domain.Enums;
using OrderPlatform.Domain.Interfaces;

namespace OrderPlatform.Application.Dashboard;

/// <summary>数据看板服务接口。</summary>
public interface IDashboardService
{
    /// <summary>获取看板汇总数据。</summary>
    Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken);
}

/// <summary>数据看板服务实现：基于全部订单/客户在内存中聚合统计。</summary>
public class DashboardService : IDashboardService
{
    private readonly IDashboardRepository _dashboardRepository;

    public DashboardService(IDashboardRepository dashboardRepository)
    {
        _dashboardRepository = dashboardRepository;
    }

    /// <summary>汇总订单总数、今日订单、总金额、待推送数、客户数及各维度排行。</summary>
    public async Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken)
    {
        var today = DateTime.Today;
        var orders = await _dashboardRepository.GetAllOrdersAsync(cancellationToken);
        var customers = await _dashboardRepository.GetAllCustomersAsync(cancellationToken);

        var customerMap = customers.ToDictionary(c => c.Id, c => c.Name);

        // 按客户统计订单数与金额
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

        // 最近 10 笔订单
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

/// <summary>看板汇总数据。</summary>
public class DashboardSummaryDto
{
    /// <summary>订单总数。</summary>
    public int TotalOrders { get; set; }

    /// <summary>今日新增订单数。</summary>
    public int TodayOrders { get; set; }

    /// <summary>订单总金额。</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>待推送订单数。</summary>
    public int PendingPush { get; set; }

    /// <summary>合作客户数。</summary>
    public int TotalCustomers { get; set; }

    /// <summary>客户订单统计。</summary>
    public List<CustomerOrderStatDto> CustomerOrderStats { get; set; } = new();

    /// <summary>最近订单。</summary>
    public List<RecentOrderDto> RecentOrders { get; set; } = new();
}

/// <summary>客户订单统计项。</summary>
public class CustomerOrderStatDto
{
    /// <summary>客户 ID。</summary>
    public Guid CustomerId { get; set; }

    /// <summary>客户名称。</summary>
    public string CustomerName { get; set; } = string.Empty;

    /// <summary>订单数。</summary>
    public int OrderCount { get; set; }

    /// <summary>订单总金额。</summary>
    public decimal TotalAmount { get; set; }
}

/// <summary>最近订单项。</summary>
public class RecentOrderDto
{
    /// <summary>订单 ID。</summary>
    public Guid Id { get; set; }

    /// <summary>订单号。</summary>
    public string OrderNo { get; set; } = string.Empty;

    /// <summary>客户名称。</summary>
    public string CustomerName { get; set; } = string.Empty;

    /// <summary>订单金额。</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>创建时间。</summary>
    public DateTime CreatedAt { get; set; }
}