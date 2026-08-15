using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using OrderPlatform.Shared.Api;

namespace OrderPlatform.Api.Filters;

/// <summary>
/// 统一返回过滤器：把所有控制器返回包装为 ApiResponse&lt;T&gt; 格式。
/// 若返回已是 ApiResponse 则不重复包装；ModelState 不合法直接返回 400。
/// </summary>
public class ApiResponseFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // 模型校验失败：直接以统一格式返回 400
        if (!context.ModelState.IsValid)
        {
            var modelErrors = string.Join("；", context.ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage));
            context.Result = new ObjectResult(ApiResponse<object>.Fail(400, modelErrors));
            return;
        }

        var executed = await next();

        // 已有异常时交给全局异常过滤器处理
        if (executed.Exception is not null)
        {
            return;
        }

        if (executed.Result is ObjectResult objectResult)
        {
            // 已包装过的不再处理
            if (objectResult.Value is IApiResponse)
            {
                return;
            }

            // 验证问题详情（ValidationProblemDetails）转为统一 400
            if (objectResult.Value is ValidationProblemDetails problemDetails)
            {
                var message = string.Join("；", problemDetails.Errors.Values.SelectMany(v => v));
                executed.Result = new ObjectResult(ApiResponse<object>.Fail(400, message));
                return;
            }

            // 4xx/5xx 状态码转为统一失败格式
            if (objectResult.StatusCode is >= 400)
            {
                executed.Result = new ObjectResult(ApiResponse<object>.Fail(
                    objectResult.StatusCode.Value,
                    objectResult.Value?.ToString() ?? "请求失败"));
                return;
            }

            // 正常结果包装为成功响应
            executed.Result = new ObjectResult(ApiResponse<object>.Ok(objectResult.Value));
            return;
        }

        // 无返回内容的方法视为成功且数据为空
        if (executed.Result is EmptyResult or null)
        {
            executed.Result = new ObjectResult(ApiResponse<object>.Ok(null));
        }
    }
}