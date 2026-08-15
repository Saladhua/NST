using AutoMapper;
using FluentValidation;
using Microsoft.IdentityModel.JsonWebTokens;
using OrderPlatform.Application.Auth.Dtos;
using OrderPlatform.Domain.Entities;
using OrderPlatform.Domain.Enums;
using OrderPlatform.Domain.Interfaces;
using OrderPlatform.Shared.Api;

namespace OrderPlatform.Application.Auth;

/// <summary>认证服务实现：登录、刷新令牌、注册、修改密码。</summary>
public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IMapper _mapper;
    private readonly IValidator<LoginRequest> _loginValidator;
    private readonly IValidator<RegisterRequest> _registerValidator;
    private readonly IValidator<ChangePasswordRequest> _changePasswordValidator;

    public AuthService(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IMapper mapper,
        IValidator<LoginRequest> loginValidator,
        IValidator<RegisterRequest> registerValidator,
        IValidator<ChangePasswordRequest> changePasswordValidator)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _mapper = mapper;
        _loginValidator = loginValidator;
        _registerValidator = registerValidator;
        _changePasswordValidator = changePasswordValidator;
    }

    /// <summary>登录：校验参数、用户名、密码与账号状态，成功后签发令牌。</summary>
    public async Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        await _loginValidator.ValidateAndThrowAsync(request, cancellationToken);

        var user = await _userRepository.GetByUserNameAsync(request.UserName.Trim(), cancellationToken);
        if (user is null)
        {
            throw new BusinessException("用户名不存在");
        }

        if (!_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            throw new BusinessException("密码错误");
        }

        if (user.Status != UserStatus.Active)
        {
            throw new BusinessException("账号已被禁用");
        }

        return BuildAuthResult(user);
    }

    /// <summary>刷新令牌：校验刷新令牌有效性、用户存在与账号状态，再签发新令牌。</summary>
    public async Task<AuthResult> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken)
    {
        var principal = _tokenService.ValidateRefreshToken(request.RefreshToken)
            ?? throw new BusinessException("刷新令牌无效或已过期", 401);

        var userIdClaim = principal.FindFirst(JwtRegisteredClaimNames.Sub);
        if (userIdClaim is null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            throw new BusinessException("刷新令牌无效或已过期", 401);
        }

        var user = await _userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new BusinessException("用户不存在", 401);

        if (user.Status != UserStatus.Active)
        {
            throw new BusinessException("账号已被禁用", 401);
        }

        return BuildAuthResult(user);
    }

    /// <summary>注册：校验参数与用户名唯一性，创建普通用户。</summary>
    public async Task RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        await _registerValidator.ValidateAndThrowAsync(request, cancellationToken);

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
            Role = UserRole.User,
            Status = UserStatus.Active,
            CreatedAt = DateTime.Now
        };

        await _userRepository.AddAsync(user, cancellationToken);
        await _userRepository.SaveChangesAsync(cancellationToken);
    }

    /// <summary>修改密码：校验原密码正确后更新为新密码。</summary>
    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        await _changePasswordValidator.ValidateAndThrowAsync(request, cancellationToken);

        var user = await _userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new BusinessException("用户不存在", 401);

        if (!_passwordHasher.Verify(request.OldPassword, user.PasswordHash))
        {
            throw new BusinessException("原密码不正确");
        }

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        user.UpdatedAt = DateTime.Now;
        _userRepository.Update(user);
        await _userRepository.SaveChangesAsync(cancellationToken);
    }

    /// <summary>组装登录/刷新成功后的返回结果（访问令牌 + 刷新令牌 + 用户信息）。</summary>
    private AuthResult BuildAuthResult(User user)
    {
        return new AuthResult
        {
            AccessToken = _tokenService.GenerateAccessToken(user),
            RefreshToken = _tokenService.GenerateRefreshToken(user),
            ExpiresAt = DateTime.Now.AddMinutes(_tokenService.AccessTokenExpireMinutes),
            UserInfo = _mapper.Map<UserInfoDto>(user)
        };
    }
}