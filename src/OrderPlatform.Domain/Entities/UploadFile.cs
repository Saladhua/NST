namespace OrderPlatform.Domain.Entities;

public class UploadFile
{
    public Guid Id { get; set; }

    public Guid BatchId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;

    public long FileSize { get; set; }

    public string FileType { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public bool Parsed { get; set; }

    public DateTime CreatedAt { get; set; }
}
