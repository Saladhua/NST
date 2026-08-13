namespace OrderPlatform.Application.Parsers;

/// <summary>
/// Excel 客户资料解析结果。每个 sheet 对应一个客户，sheet 名即客户名。
/// </summary>
public class ExcelParseResult
{
    public List<ExcelSheetData> Sheets { get; set; } = new();
}

public class ExcelSheetData
{
    public string SheetName { get; set; } = string.Empty;

    public List<string> Headers { get; set; } = new();

    /// <summary>每行为一个字典，键为表头（列名），值为单元格文本。</summary>
    public List<Dictionary<string, string>> Rows { get; set; } = new();
}
