namespace OrderPlatform.Domain.Entities;

public class OrderPushLog
{
    public Guid Id { get; set; }

    public Guid OrderId { get; set; }

    public string Target { get; set; } = string.Empty;

    public string? RequestJson { get; set; }

    public string? ResponseJson { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTime PushTime { get; set; }

    public string? ErrorMessage { get; set; }
}
