namespace OrderPlatform.Domain.Entities;

/// <summary>订单推送日志实体，记录每次推送请求与响应（技术验证版仅模拟推送）。</summary>
public class OrderPushLog
{
    /// <summary>日志唯一标识。</summary>
    public Guid Id { get; set; }

    /// <summary>被推送的订单。</summary>
    public Guid OrderId { get; set; }

    /// <summary>推送目标系统。</summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>推送请求体 JSON。</summary>
    public string? RequestJson { get; set; }

    /// <summary>推送响应 JSON。</summary>
    public string? ResponseJson { get; set; }

    /// <summary>推送状态（Success / Failed）。</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>推送时间。</summary>
    public DateTime PushTime { get; set; }

    /// <summary>失败时的错误信息。</summary>
    public string? ErrorMessage { get; set; }
}