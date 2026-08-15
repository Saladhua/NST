using FluentValidation;
using OrderPlatform.Application.Auth.Dtos;

namespace OrderPlatform.Application.Validators;

/// <summary>修改密码请求校验器。</summary>
public class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.OldPassword)
            .NotEmpty().WithMessage("原密码不能为空");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("新密码不能为空")
            .MinimumLength(6).WithMessage("新密码长度不能少于6个字符")
            .MaximumLength(50).WithMessage("新密码长度不能超过50个字符")
            .NotEqual(x => x.OldPassword).WithMessage("新密码不能与原密码相同");
    }
}