namespace OrderPlatform.Application.Upload.Dtos;

/// <summary>Excel 批次解析明细。每个 sheet 对应一个客户，sheet 名即客户名。</summary>
public class ExcelBatchDetailDto
{
    public Guid BatchId { get; set; }

    public string BatchNo { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public List<ExcelSheetDetailDto> Sheets { get; set; } = new();
}

public class ExcelSheetDetailDto
{
    public string SheetName { get; set; } = string.Empty;

    public List<string> Headers { get; set; } = new();

    /// <summary>每行为一个字典，键为表头（列名），值为单元格文本。</summary>
    public List<Dictionary<string, string>> Rows { get; set; } = new();
}
