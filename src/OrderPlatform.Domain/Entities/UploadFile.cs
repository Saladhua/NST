namespace OrderPlatform.Domain.Entities;

/// <summary>上传文件记录实体（历史审计，不提供删除入口）。</summary>
public class UploadFile
{
    /// <summary>文件记录唯一标识。</summary>
    public Guid Id { get; set; }

    /// <summary>所属上传批次。</summary>
    public Guid BatchId { get; set; }

    /// <summary>原始文件名。</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>保存路径。</summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>文件大小（字节）。</summary>
    public long FileSize { get; set; }

    /// <summary>文件类型（PDF / Excel）。</summary>
    public string FileType { get; set; } = string.Empty;

    /// <summary>状态。</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>是否已解析。</summary>
    public bool Parsed { get; set; }

    /// <summary>创建时间。</summary>
    public DateTime CreatedAt { get; set; }
}