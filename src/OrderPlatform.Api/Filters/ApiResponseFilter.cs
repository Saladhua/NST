using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using OrderPlatform.Shared.Api;

namespace OrderPlatform.Api.Filters;

public class ApiResponseFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!context.ModelState.IsValid)
        {
            var modelErrors = string.Join("；", context.ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage));
            context.Result = new ObjectResult(ApiResponse<object>.Fail(400, modelErrors));
            return;
        }

        var executed = await next();

        if (executed.Exception is not null)
        {
            return;
        }

        if (executed.Result is ObjectResult objectResult)
        {
            if (objectResult.Value is IApiResponse)
            {
                return;
            }

            if (objectResult.Value is ValidationProblemDetails problemDetails)
            {
                var message = string.Join("；", problemDetails.Errors.Values.SelectMany(v => v));
                executed.Result = new ObjectResult(ApiResponse<object>.Fail(400, message));
                return;
            }

            if (objectResult.StatusCode is >= 400)
            {
                executed.Result = new ObjectResult(ApiResponse<object>.Fail(
                    objectResult.StatusCode.Value,
                    objectResult.Value?.ToString() ?? "请求失败"));
                return;
            }

            executed.Result = new ObjectResult(ApiResponse<object>.Ok(objectResult.Value));
            return;
        }

        if (executed.Result is EmptyResult or null)
        {
            executed.Result = new ObjectResult(ApiResponse<object>.Ok(null));
        }
    }
}
