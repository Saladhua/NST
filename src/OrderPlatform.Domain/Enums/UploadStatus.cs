namespace OrderPlatform.Domain.Enums;

/// <summary>上传批次解析状态（异步流转：Pending → Parsing → Completed/Failed）。</summary>
public enum UploadStatus
{
    /// <summary>待处理（已入队）。</summary>
    Pending,

    /// <summary>解析中。</summary>
    Parsing,

    /// <summary>解析完成。</summary>
    Completed,

    /// <summary>解析失败。</summary>
    Failed
}