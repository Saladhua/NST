namespace OrderPlatform.Application.Upload.Dtos;

/// <summary>上传批次 DTO（返回给前端的状态/进度/结果信息）。</summary>
public class UploadBatchDto
{
    /// <summary>批次 ID。</summary>
    public Guid BatchId { get; set; }

    /// <summary>批次号。</summary>
    public string BatchNo { get; set; } = string.Empty;

    /// <summary>文件类型（PDF / Excel）。</summary>
    public string FileType { get; set; } = string.Empty;

    /// <summary>文件名。</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>识别出的客户 ID。</summary>
    public Guid? CustomerId { get; set; }

    /// <summary>客户名称。</summary>
    public string? CustomerName { get; set; }

    /// <summary>解析状态字符串。</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>解析进度（0-100）。</summary>
    public int Progress { get; set; }

    /// <summary>错误信息或重复导入提示。</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>是否已解析。</summary>
    public bool Parsed { get; set; }

    /// <summary>该批次生成的订单数。</summary>
    public int OrderCount { get; set; }

    /// <summary>是否订单已被删除（已完成且无订单且非重复导入）。</summary>
    public bool OrderDeleted { get; set; }

    /// <summary>原始解析数据（前端一般不使用）。</summary>
    public object? RawData { get; set; }

    /// <summary>创建时间。</summary>
    public DateTime CreatedAt { get; set; }
}