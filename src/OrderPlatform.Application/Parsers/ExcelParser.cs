using ClosedXML.Excel;

namespace OrderPlatform.Application.Parsers;

/// <summary>Excel 解析器接口。</summary>
public interface IExcelParser
{
    Task<ExcelParseResult> ParseAsync(string filePath, CancellationToken cancellationToken);
}

/// <summary>
/// 基于 ClosedXML 的 Excel 客户资料解析器。
/// 约定：每个 sheet 对应一个客户，sheet 名即客户名；第一行为表头，其后为数据行。
/// </summary>
public class ExcelParser : IExcelParser
{
    public Task<ExcelParseResult> ParseAsync(string filePath, CancellationToken cancellationToken)
    {
        return Task.FromResult(Parse(filePath));
    }

    /// <summary>解析整个工作簿：逐 sheet 读取表头与数据行。</summary>
    public static ExcelParseResult Parse(string filePath)
    {
        var result = new ExcelParseResult();
        using var workbook = new XLWorkbook(filePath);

        foreach (var sheet in workbook.Worksheets)
        {
            var data = new ExcelSheetData
            {
                SheetName = sheet.Name.Trim()
            };

            var usedRows = sheet.RangeUsed();
            if (usedRows is null)
            {
                result.Sheets.Add(data);
                continue;
            }

            var firstRow = usedRows.FirstRow().RowNumber();
            var headerCount = usedRows.ColumnCount();

            // 表头：第一行
            for (var col = 0; col < headerCount; col++)
            {
                var cell = sheet.Cell(firstRow, usedRows.FirstColumn().ColumnNumber() + col);
                data.Headers.Add(TrimCell(cell));
            }

            // 数据行：从第二行开始，忽略全空行
            for (var rowNum = firstRow + 1; rowNum <= usedRows.LastRow().RowNumber(); rowNum++)
            {
                var dict = new Dictionary<string, string>();
                var anyValue = false;
                for (var col = 0; col < headerCount; col++)
                {
                    var colNum = usedRows.FirstColumn().ColumnNumber() + col;
                    var value = TrimCell(sheet.Cell(rowNum, colNum));
                    var header = col < data.Headers.Count ? data.Headers[col] : $"列{col + 1}";
                    dict[header] = value;
                    if (!string.IsNullOrEmpty(value))
                    {
                        anyValue = true;
                    }
                }

                if (anyValue)
                {
                    data.Rows.Add(dict);
                }
            }

            result.Sheets.Add(data);
        }

        return result;
    }

    /// <summary>读取单元格文本并去除首尾空白。</summary>
    private static string TrimCell(IXLCell cell)
    {
        var value = cell.GetString();
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}