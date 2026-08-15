namespace OrderPlatform.Shared.Api;

/// <summary>
/// 业务异常：服务层抛出后由全局异常过滤器捕获，
/// 以对应 code 返回前端提示，而不视为服务器内部错误。
/// </summary>
public class BusinessException : Exception
{
    /// <summary>业务错误码（默认 400）。</summary>
    public int Code { get; }

    public BusinessException(string message, int code = 400)
        : base(message)
    {
        Code = code;
    }
}