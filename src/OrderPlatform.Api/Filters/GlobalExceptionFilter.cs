using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using OrderPlatform.Shared.Api;

namespace OrderPlatform.Api.Filters;

/// <summary>
/// 全局异常过滤器：统一捕获异常并转换为 ApiResponse 失败格式。
/// 业务异常/校验异常返回对应提示，其余按 500 处理。
/// </summary>
public class GlobalExceptionFilter : IExceptionFilter
{
    private readonly ILogger<GlobalExceptionFilter> _logger;

    public GlobalExceptionFilter(ILogger<GlobalExceptionFilter> logger)
    {
        _logger = logger;
    }

    public void OnException(ExceptionContext context)
    {
        var exception = context.Exception;

        // 业务异常：按业务 code 返回
        if (exception is BusinessException businessException)
        {
            context.Result = new ObjectResult(ApiResponse<object>.Fail(businessException.Code, businessException.Message));
            context.ExceptionHandled = true;
            return;
        }

        // FluentValidation 校验异常：拼接错误信息返回 400
        if (exception is ValidationException validationException)
        {
            var message = string.Join("；", validationException.Errors.Select(e => e.ErrorMessage));
            context.Result = new ObjectResult(ApiResponse<object>.Fail(400, message));
            context.ExceptionHandled = true;
            return;
        }

        // 未预期异常：记录日志并返回 500，避免暴露内部细节
        _logger.LogError(exception, "未处理异常: {Message}", exception.Message);
        context.Result = new ObjectResult(ApiResponse<object>.Fail(500, "服务器内部错误"));
        context.ExceptionHandled = true;
    }
}