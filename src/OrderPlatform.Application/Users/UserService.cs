using OrderPlatform.Application.Orders;
using OrderPlatform.Application.Auth;
using OrderPlatform.Domain.Entities;
using OrderPlatform.Domain.Enums;
using OrderPlatform.Domain.Interfaces;
using OrderPlatform.Shared.Api;

namespace OrderPlatform.Application.Users;

/// <summary>用户管理服务接口（管理员功能）。</summary>
public interface IUserService
{
    /// <summary>分页查询用户列表。</summary>
    Task<PagedResult<UserListDto>> ListAsync(int page, int pageSize, CancellationToken cancellationToken);

    /// <summary>新增用户。</summary>
    Task CreateAsync(CreateUserRequest request, CancellationToken cancellationToken);

    /// <summary>更新用户（姓名/联系方式/角色/状态）。</summary>
    Task UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken);

    /// <summary>重置用户密码。</summary>
    Task ResetPasswordAsync(Guid id, ResetPasswordRequest request, CancellationToken cancellationToken);

    /// <summary>删除用户（管理员账号不可删除）。</summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}

/// <summary>用户管理服务实现。</summary>
public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;

    public UserService(IUserRepository userRepository, IPasswordHasher passwordHasher)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
    }

    /// <summary>分页查询用户列表。</summary>
    public async Task<PagedResult<UserListDto>> ListAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var users = await _userRepository.ListAsync(page, pageSize, cancellationToken);
        var total = await _userRepository.CountAsync(cancellationToken);
        var items = users.Select(u => new UserListDto
        {
            Id = u.Id,
            UserName = u.UserName,
            DisplayName = u.DisplayName,
            Phone = u.Phone,
            Email = u.Email,
            Role = u.Role,
            Status = u.Status,
            CreatedAt = u.CreatedAt
        }).ToList();

        return new PagedResult<UserListDto>(items, total);
    }

    /// <summary>新增用户（校验用户名唯一，密码 BCrypt 加密存储）。</summary>
    public async Task CreateAsync(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var userName = request.UserName.Trim();
        if (await _userRepository.ExistsByUserNameAsync(userName, cancellationToken))
        {
            throw new BusinessException("用户名已存在");
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = userName,
            PasswordHash = _passwordHasher.Hash(request.Password),
            DisplayName = request.DisplayName.Trim(),
            Phone = request.Phone?.Trim(),
            Email = request.Email?.Trim(),
            Role = request.Role,
            Status = UserStatus.Active,
            CreatedAt = DateTime.Now
        };

        await _userRepository.AddAsync(user, cancellationToken);
        await _userRepository.SaveChangesAsync(cancellationToken);
    }

    /// <summary>更新用户基本信息、角色与状态。</summary>
    public async Task UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new BusinessException("用户不存在");

        user.DisplayName = request.DisplayName.Trim();
        user.Phone = request.Phone?.Trim();
        user.Email = request.Email?.Trim();
        user.Role = request.Role;
        user.Status = request.Status;
        user.UpdatedAt = DateTime.Now;
        _userRepository.Update(user);
        await _userRepository.SaveChangesAsync(cancellationToken);
    }

    /// <summary>重置用户密码（管理员操作，无需原密码）。</summary>
    public async Task ResetPasswordAsync(Guid id, ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new BusinessException("用户不存在");

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        user.UpdatedAt = DateTime.Now;
        _userRepository.Update(user);
        await _userRepository.SaveChangesAsync(cancellationToken);
    }

    /// <summary>删除用户（禁止删除管理员账号）。</summary>
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new BusinessException("用户不存在");

        if (user.Role == UserRole.Admin)
        {
            throw new BusinessException("不能删除管理员账号");
        }

        _userRepository.Delete(user);
        await _userRepository.SaveChangesAsync(cancellationToken);
    }
}