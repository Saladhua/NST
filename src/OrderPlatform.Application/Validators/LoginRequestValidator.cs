using FluentValidation;
using OrderPlatform.Application.Auth.Dtos;

namespace OrderPlatform.Application.Validators;

/// <summary>登录请求校验器。</summary>
public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.UserName)
            .NotEmpty().WithMessage("用户名不能为空")
            .MaximumLength(50).WithMessage("用户名长度不能超过50个字符");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("密码不能为空")
            .MaximumLength(100).WithMessage("密码长度不能超过100个字符");
    }
}