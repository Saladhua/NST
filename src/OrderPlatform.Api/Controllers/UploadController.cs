using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using OrderPlatform.Application.Orders;
using OrderPlatform.Application.Upload;
using OrderPlatform.Application.Upload.Dtos;
using OrderPlatform.Shared.Api;

namespace OrderPlatform.Api.Controllers;

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

    [HttpGet("batch/{batchId:guid}")]
    public async Task<ApiResponse<UploadBatchDto>> GetBatch(Guid batchId, CancellationToken cancellationToken)
    {
        var result = await _uploadService.GetBatchAsync(batchId, cancellationToken);
        return ApiResponse<UploadBatchDto>.Ok(result);
    }

    [HttpGet("batches")]
    public async Task<ApiResponse<PagedResult<UploadBatchDto>>> Batches(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var result = await _uploadService.ListBatchesAsync(page, pageSize, cancellationToken);
        return ApiResponse<PagedResult<UploadBatchDto>>.Ok(result);
    }

    [HttpGet("batch/{batchId:guid}/excel")]
    public async Task<ApiResponse<ExcelBatchDetailDto>> BatchExcel(Guid batchId, CancellationToken cancellationToken)
    {
        var result = await _uploadService.GetBatchExcelAsync(batchId, cancellationToken);
        return ApiResponse<ExcelBatchDetailDto>.Ok(result);
    }

    [HttpGet("customers")]
    public async Task<ApiResponse<List<CustomerImportDto>>> Customers(CancellationToken cancellationToken)
    {
        var result = await _uploadService.GetCustomersAsync(cancellationToken);
        return ApiResponse<List<CustomerImportDto>>.Ok(result);
    }

    [HttpGet("batch/{batchId:guid}/orders")]
    public async Task<ApiResponse<List<OrderGeneratedDto>>> BatchOrders(Guid batchId, CancellationToken cancellationToken)
    {
        var result = await _uploadService.GetBatchOrdersAsync(batchId, cancellationToken);
        return ApiResponse<List<OrderGeneratedDto>>.Ok(result);
    }

    [HttpDelete("customers/{customerId:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ApiResponse<bool>> DeleteCustomer(Guid customerId, CancellationToken cancellationToken)
    {
        await _uploadService.DeleteCustomerAsync(customerId, cancellationToken);
        return ApiResponse<bool>.Ok(true, "删除成功");
    }

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