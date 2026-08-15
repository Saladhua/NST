using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using OrderPlatform.Application.Orders;
using OrderPlatform.Application.Upload;
using OrderPlatform.Application.Upload.Dtos;
using OrderPlatform.Shared.Api;

namespace OrderPlatform.Api.Controllers;

/// <summary>上传接口：接收文件、查询批次/解析明细/客户统计。</summary>
[ApiController]
[Route("api/upload")]
[Authorize]
public class UploadController : ControllerBase
{
    private readonly IUploadService _uploadService;

    public UploadController(IUploadService uploadService)
    {
        _uploadService = uploadService;
    }

    /// <summary>上传文件（PDF 订单或 Excel 客户资料），创建批次并异步解析。</summary>
    [HttpPost]
    [RequestSizeLimit(50 * 1024 * 1024)]
    public async Task<ApiResponse<List<UploadBatchDto>>> Upload(
        IFormFileCollection files,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var result = await _uploadService.CreateBatchesAsync(files, userId, cancellationToken);
        return ApiResponse<List<UploadBatchDto>>.Ok(result, "上传成功，正在后台解析");
    }

    /// <summary>查询单批次解析状态与进度。</summary>
    [HttpGet("batch/{batchId:guid}")]
    public async Task<ApiResponse<UploadBatchDto>> GetBatch(Guid batchId, CancellationToken cancellationToken)
    {
        var result = await _uploadService.GetBatchAsync(batchId, cancellationToken);
        return ApiResponse<UploadBatchDto>.Ok(result);
    }

    /// <summary>分页查询历史上传记录。</summary>
    [HttpGet("batches")]
    public async Task<ApiResponse<PagedResult<UploadBatchDto>>> Batches(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var result = await _uploadService.ListBatchesAsync(page, pageSize, cancellationToken);
        return ApiResponse<PagedResult<UploadBatchDto>>.Ok(result);
    }

    /// <summary>查看 Excel 批次解析明细。</summary>
    [HttpGet("batch/{batchId:guid}/excel")]
    public async Task<ApiResponse<ExcelBatchDetailDto>> BatchExcel(Guid batchId, CancellationToken cancellationToken)
    {
        var result = await _uploadService.GetBatchExcelAsync(batchId, cancellationToken);
        return ApiResponse<ExcelBatchDetailDto>.Ok(result);
    }

    /// <summary>客户图号统计列表。</summary>
    [HttpGet("customers")]
    public async Task<ApiResponse<List<CustomerImportDto>>> Customers(CancellationToken cancellationToken)
    {
        var result = await _uploadService.GetCustomersAsync(cancellationToken);
        return ApiResponse<List<CustomerImportDto>>.Ok(result);
    }

    /// <summary>查看 PDF 批次生成的订单。</summary>
    [HttpGet("batch/{batchId:guid}/orders")]
    public async Task<ApiResponse<List<OrderGeneratedDto>>> BatchOrders(Guid batchId, CancellationToken cancellationToken)
    {
        var result = await _uploadService.GetBatchOrdersAsync(batchId, cancellationToken);
        return ApiResponse<List<OrderGeneratedDto>>.Ok(result);
    }

    /// <summary>软删除客户资料（仅管理员）。</summary>
    [HttpDelete("customers/{customerId:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ApiResponse<bool>> DeleteCustomer(Guid customerId, CancellationToken cancellationToken)
    {
        await _uploadService.DeleteCustomerAsync(customerId, cancellationToken);
        return ApiResponse<bool>.Ok(true, "删除成功");
    }

    /// <summary>从 JWT 中解析当前用户 ID。</summary>
    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub);
        if (userIdClaim is null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            throw new BusinessException("登录状态无效，请重新登录", 401);
        }

        return userId;
    }
}