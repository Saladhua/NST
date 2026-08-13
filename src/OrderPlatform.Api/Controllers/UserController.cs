using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderPlatform.Application.Orders;
using OrderPlatform.Application.Users;

namespace OrderPlatform.Api.Controllers;

[ApiController]
[Route("api/user")]
[Authorize]
public class UserController : ControllerBase
{
    private readonly IUserService _userService;

    public UserController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet("list")]
    [Authorize(Roles = "Admin")]
    public async Task<OrderPlatform.Shared.Api.ApiResponse<PagedResult<UserListDto>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _userService.ListAsync(page, pageSize, cancellationToken);
        return OrderPlatform.Shared.Api.ApiResponse<PagedResult<UserListDto>>.Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<OrderPlatform.Shared.Api.ApiResponse<object>> Create(CreateUserRequest request, CancellationToken cancellationToken)
    {
        await _userService.CreateAsync(request, cancellationToken);
        return OrderPlatform.Shared.Api.ApiResponse<object>.Ok(null, "新增用户成功");
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<OrderPlatform.Shared.Api.ApiResponse<object>> Update(Guid id, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        await _userService.UpdateAsync(id, request, cancellationToken);
        return OrderPlatform.Shared.Api.ApiResponse<object>.Ok(null, "更新用户成功");
    }

    [HttpPut("{id:guid}/reset-password")]
    [Authorize(Roles = "Admin")]
    public async Task<OrderPlatform.Shared.Api.ApiResponse<object>> ResetPassword(Guid id, ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        await _userService.ResetPasswordAsync(id, request, cancellationToken);
        return OrderPlatform.Shared.Api.ApiResponse<object>.Ok(null, "重置密码成功");
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<OrderPlatform.Shared.Api.ApiResponse<object>> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _userService.DeleteAsync(id, cancellationToken);
        return OrderPlatform.Shared.Api.ApiResponse<object>.Ok(null, "删除用户成功");
    }
}