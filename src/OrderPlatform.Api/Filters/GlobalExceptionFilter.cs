using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using OrderPlatform.Shared.Api;

namespace OrderPlatform.Api.Filters;

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

        if (exception is BusinessException businessException)
        {
            context.Result = new ObjectResult(ApiResponse<object>.Fail(businessException.Code, businessException.Message));
            context.ExceptionHandled = true;
            return;
        }

        if (exception is ValidationException validationException)
        {
            var message = string.Join("；", validationException.Errors.Select(e => e.ErrorMessage));
            context.Result = new ObjectResult(ApiResponse<object>.Fail(400, message));
            context.ExceptionHandled = true;
            return;
        }

        _logger.LogError(exception, "未处理异常: {Message}", exception.Message);
        context.Result = new ObjectResult(ApiResponse<object>.Fail(500, "服务器内部错误"));
        context.ExceptionHandled = true;
    }
}
