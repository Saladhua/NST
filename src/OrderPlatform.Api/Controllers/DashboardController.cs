using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderPlatform.Application.Dashboard;

namespace OrderPlatform.Api.Controllers;

/// <summary>数据看板接口。</summary>
[ApiController]
[Route("api/dashboard")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    /// <summary>看板汇总数据（订单/客户统计）。</summary>
    [HttpGet("summary")]
    public async Task<OrderPlatform.Shared.Api.ApiResponse<DashboardSummaryDto>> Summary(CancellationToken cancellationToken)
    {
        var result = await _dashboardService.GetSummaryAsync(cancellationToken);
        return OrderPlatform.Shared.Api.ApiResponse<DashboardSummaryDto>.Ok(result);
    }
}