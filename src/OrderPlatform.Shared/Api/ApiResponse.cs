namespace OrderPlatform.Shared.Api;

/// <summary>统一返回格式标记接口，用于过滤器识别已包装的响应。</summary>
public interface IApiResponse
{
}

/// <summary>
/// 全局统一返回格式：{ code, success, message, data }。
/// code=200 表示成功；code=400/401/500 表示业务/认证/服务器错误。
/// </summary>
public class ApiResponse<T> : IApiResponse
{
    /// <summary>状态码：200 成功，其余为错误码。</summary>
    public int Code { get; set; }

    /// <summary>是否成功。</summary>
    public bool Success { get; set; }

    /// <summary>提示信息。</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>业务数据。</summary>
    public T? Data { get; set; }

    /// <summary>构造成功响应。</summary>
    public static ApiResponse<T> Ok(T? data, string message = "成功")
    {
        return new ApiResponse<T> { Code = 200, Success = true, Message = message, Data = data };
    }

    /// <summary>构造失败响应。</summary>
    public static ApiResponse<T> Fail(int code, string message)
    {
        return new ApiResponse<T> { Code = code, Success = false, Message = message };
    }
}