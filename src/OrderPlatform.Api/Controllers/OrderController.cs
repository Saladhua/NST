using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderPlatform.Application.Orders;
using OrderPlatform.Domain.Enums;

namespace OrderPlatform.Api.Controllers;

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

    [HttpGet("detail/{id:guid}")]
    public async Task<OrderPlatform.Shared.Api.ApiResponse<OrderDetailDto>> Detail(Guid id, CancellationToken cancellationToken)
    {
        var result = await _orderService.GetDetailAsync(id, cancellationToken);
        return OrderPlatform.Shared.Api.ApiResponse<OrderDetailDto>.Ok(result);
    }

    [HttpPost("push")]
    public async Task<OrderPlatform.Shared.Api.ApiResponse<PushResultDto>> Push(PushOrderRequest request, CancellationToken cancellationToken)
    {
        var result = await _orderService.PushAsync(request.OrderId, cancellationToken);
        return OrderPlatform.Shared.Api.ApiResponse<PushResultDto>.Ok(result, "推送成功");
    }
}

public class PushOrderRequest
{
    public Guid OrderId { get; set; }
}