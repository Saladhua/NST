namespace OrderPlatform.Application.Upload.Dtos;

public class UploadBatchDto
{
    public Guid BatchId { get; set; }

    public string BatchNo { get; set; } = string.Empty;

    public string FileType { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public Guid? CustomerId { get; set; }

    public string? CustomerName { get; set; }

    public string Status { get; set; } = string.Empty;

    public int Progress { get; set; }

    public string? ErrorMessage { get; set; }

    public bool Parsed { get; set; }

    public int OrderCount { get; set; }

    public object? RawData { get; set; }

    public DateTime CreatedAt { get; set; }
}
