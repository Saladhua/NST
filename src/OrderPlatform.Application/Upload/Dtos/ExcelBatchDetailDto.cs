namespace OrderPlatform.Application.Upload.Dtos;

/// <summary>Excel 批次解析明细。每个 sheet 对应一个客户，sheet 名即客户名。</summary>
public class ExcelBatchDetailDto
{
    /// <summary>批次 ID。</summary>
    public Guid BatchId { get; set; }

    /// <summary>批次号。</summary>
    public string BatchNo { get; set; } = string.Empty;

    /// <summary>文件名。</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>各 sheet 的解析数据。</summary>
    public List<ExcelSheetDetailDto> Sheets { get; set; } = new();
}

/// <summary>Excel 单个 sheet 的解析数据。</summary>
public class ExcelSheetDetailDto
{
    /// <summary>sheet 名（即客户名）。</summary>
    public string SheetName { get; set; } = string.Empty;

    /// <summary>表头（列名）。</summary>
    public List<string> Headers { get; set; } = new();

    /// <summary>每行为一个字典，键为表头（列名），值为单元格文本。</summary>
    public List<Dictionary<string, string>> Rows { get; set; } = new();
}