using OrderPlatform.Application.Orders;
using OrderPlatform.Application.Auth;
using OrderPlatform.Domain.Entities;
using OrderPlatform.Domain.Enums;
using OrderPlatform.Domain.Interfaces;
using OrderPlatform.Shared.Api;

namespace OrderPlatform.Application.Users;

public interface IUserService
{
    Task<PagedResult<UserListDto>> ListAsync(int page, int pageSize, CancellationToken cancellationToken);

    Task CreateAsync(CreateUserRequest request, CancellationToken cancellationToken);

    Task UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken);

    Task ResetPasswordAsync(Guid id, ResetPasswordRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;

    public UserService(IUserRepository userRepository, IPasswordHasher passwordHasher)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
    }

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

    public async Task ResetPasswordAsync(Guid id, ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new BusinessException("用户不存在");

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        user.UpdatedAt = DateTime.Now;
        _userRepository.Update(user);
        await _userRepository.SaveChangesAsync(cancellationToken);
    }

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
