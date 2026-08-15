using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderPlatform.Application.Config;

namespace OrderPlatform.Api.Controllers;

/// <summary>系统配置接口（仅管理员）。</summary>
[ApiController]
[Route("api/config")]
[Authorize]
public class ConfigController : ControllerBase
{
    private readonly IConfigService _configService;

    public ConfigController(IConfigService configService)
    {
        _configService = configService;
    }

    /// <summary>配置列表。</summary>
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<OrderPlatform.Shared.Api.ApiResponse<List<ConfigItemDto>>> List(CancellationToken cancellationToken)
    {
        var result = await _configService.ListAsync(cancellationToken);
        return OrderPlatform.Shared.Api.ApiResponse<List<ConfigItemDto>>.Ok(result);
    }

    /// <summary>更新配置值。</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<OrderPlatform.Shared.Api.ApiResponse<object>> Update(Guid id, UpdateConfigRequest request, CancellationToken cancellationToken)
    {
        await _configService.UpdateAsync(id, request, cancellationToken);
        return OrderPlatform.Shared.Api.ApiResponse<object>.Ok(null, "配置更新成功");
    }
}