using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderPlatform.Application.Orders;
using OrderPlatform.Domain.Enums;

namespace OrderPlatform.Api.Controllers;

/// <summary>订单接口：列表、详情、推送、删除。</summary>
[ApiController]
[Route("api/order")]
[Authorize]
public class OrderController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrderController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    /// <summary>订单分页列表（支持关键词/客户/关联状态/推送状态筛选）。</summary>
    [HttpGet("list")]
    public async Task<OrderPlatform.Shared.Api.ApiResponse<PagedResult<OrderListDto>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null,
        [FromQuery] Guid? customerId = null,
        [FromQuery] PushStatus? pushStatus = null,
        [FromQuery] MatchStatus? parseStatus = null,
        CancellationToken cancellationToken = default)
    {
        if (page < 1)
        {
            page = 1;
        }

        if (pageSize is < 1 or > 100)
        {
            pageSize = 20;
        }

        var result = await _orderService.ListAsync(page, pageSize, keyword, customerId, pushStatus, parseStatus, cancellationToken);
        return OrderPlatform.Shared.Api.ApiResponse<PagedResult<OrderListDto>>.Ok(result);
    }

    /// <summary>订单详情（含明细）。</summary>
    [HttpGet("detail/{id:guid}")]
    public async Task<OrderPlatform.Shared.Api.ApiResponse<OrderDetailDto>> Detail(Guid id, CancellationToken cancellationToken)
    {
        var result = await _orderService.GetDetailAsync(id, cancellationToken);
        return OrderPlatform.Shared.Api.ApiResponse<OrderDetailDto>.Ok(result);
    }

    /// <summary>推送订单。</summary>
    [HttpPost("push")]
    public async Task<OrderPlatform.Shared.Api.ApiResponse<PushResultDto>> Push(PushOrderRequest request, CancellationToken cancellationToken)
    {
        var result = await _orderService.PushAsync(request.OrderId, cancellationToken);
        return OrderPlatform.Shared.Api.ApiResponse<PushResultDto>.Ok(result, "推送成功");
    }

    /// <summary>删除订单（仅管理员，已推送订单不可删）。</summary>
    [HttpDelete("{id:guid}")]
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Admin")]
    public async Task<OrderPlatform.Shared.Api.ApiResponse<bool>> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _orderService.DeleteAsync(id, cancellationToken);
        return OrderPlatform.Shared.Api.ApiResponse<bool>.Ok(true, "删除成功");
    }
}

/// <summary>推送订单请求。</summary>
public class PushOrderRequest
{
    /// <summary>订单 ID。</summary>
    public Guid OrderId { get; set; }
}