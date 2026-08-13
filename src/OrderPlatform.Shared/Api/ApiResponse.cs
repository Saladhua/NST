namespace OrderPlatform.Shared.Api;

public interface IApiResponse
{
}

public class ApiResponse<T> : IApiResponse
{
    public int Code { get; set; }

    public bool Success { get; set; }

    public string Message { get; set; } = string.Empty;

    public T? Data { get; set; }

    public static ApiResponse<T> Ok(T? data, string message = "成功")
    {
        return new ApiResponse<T> { Code = 200, Success = true, Message = message, Data = data };
    }

    public static ApiResponse<T> Fail(int code, string message)
    {
        return new ApiResponse<T> { Code = code, Success = false, Message = message };
    }
}
