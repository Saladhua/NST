using FluentValidation;
using OrderPlatform.Application.Auth.Dtos;

namespace OrderPlatform.Application.Validators;

/// <summary>注册请求校验器。</summary>
public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.UserName)
            .NotEmpty().WithMessage("用户名不能为空")
            .MinimumLength(3).WithMessage("用户名长度不能少于3个字符")
            .MaximumLength(50).WithMessage("用户名长度不能超过50个字符");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("密码不能为空")
            .MinimumLength(6).WithMessage("密码长度不能少于6个字符")
            .MaximumLength(50).WithMessage("密码长度不能超过50个字符");

        RuleFor(x => x.DisplayName)
            .NotEmpty().WithMessage("姓名不能为空")
            .MaximumLength(50).WithMessage("姓名长度不能超过50个字符");

        RuleFor(x => x.Phone)
            .Matches(@"^1\d{10}$").WithMessage("手机号格式不正确")
            .When(x => !string.IsNullOrWhiteSpace(x.Phone));

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("邮箱格式不正确")
            .When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}