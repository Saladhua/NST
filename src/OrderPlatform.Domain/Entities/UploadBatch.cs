using OrderPlatform.Domain.Enums;

namespace OrderPlatform.Domain.Entities;

public class UploadBatch
{
    public Guid Id { get; set; }

    public string BatchNo { get; set; } = string.Empty;

    public string FileType { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public Guid? CustomerId { get; set; }

    public Guid UploadUserId { get; set; }

    public UploadStatus Status { get; set; }

    public int Progress { get; set; }

    public string? ErrorMessage { get; set; }

    public string? RawDataJson { get; set; }

    public string OriginalPath { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}
