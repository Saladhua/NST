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

    /// <summary>推送订单（行级：只推未推送且已匹配的行）。无论成败均返回详细结果与 ERP 报文，由前端按 Status 展示。</summary>
    [HttpPost("push")]
    public async Task<OrderPlatform.Shared.Api.ApiResponse<PushResultDto>> Push(PushOrderRequest request, CancellationToken cancellationToken)
    {
        var result = await _orderService.PushAsync(request.OrderId, cancellationToken);
        var message = result.Status switch
        {
            "Success" => $"推送成功，共 {result.PushedCount} 行",
            "Partial" => $"部分推送成功（成功 {result.PushedCount} 行 / 失败 {result.FailedCount} 行）",
            _ => "推送失败"
        };
        return OrderPlatform.Shared.Api.ApiResponse<PushResultDto>.Ok(result, message);
    }

    /// <summary>批量推送订单（逐个复用推送逻辑，单笔异常记为失败且不影响其余订单）。</summary>
    [HttpPost("batch-push")]
    public async Task<OrderPlatform.Shared.Api.ApiResponse<BatchPushResultDto>> BatchPush(BatchPushOrderRequest request, CancellationToken cancellationToken)
    {
        var result = await _orderService.BatchPushAsync(request.OrderIds, cancellationToken);
        var message = $"批量推送完成：成功 {result.SuccessCount} 单 / 失败 {result.FailedCount} 单";
        return OrderPlatform.Shared.Api.ApiResponse<BatchPushResultDto>.Ok(result, message);
    }

    /// <summary>物料同步：按 图号+长度 查询 ERP 货品代号（itemId 为空时同步整单已匹配行）。</summary>
    [HttpPost("sync-material")]
    public async Task<OrderPlatform.Shared.Api.ApiResponse<MaterialSyncResultDto>> SyncMaterial(SyncMaterialRequest request, CancellationToken cancellationToken)
    {
        var result = await _orderService.SyncMaterialAsync(request.OrderId, request.ItemId, cancellationToken);
        return OrderPlatform.Shared.Api.ApiResponse<MaterialSyncResultDto>.Ok(result, "物料同步完成");
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

/// <summary>批量推送订单请求。</summary>
public class BatchPushOrderRequest
{
    /// <summary>订单 ID 列表。</summary>
    public List<Guid> OrderIds { get; set; } = new();
}