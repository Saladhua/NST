using ClosedXML.Excel;

namespace OrderPlatform.Application.Parsers;

public interface IExcelParser
{
    Task<ExcelParseResult> ParseAsync(string filePath, CancellationToken cancellationToken);
}

public class ExcelParser : IExcelParser
{
    public Task<ExcelParseResult> ParseAsync(string filePath, CancellationToken cancellationToken)
    {
        return Task.FromResult(Parse(filePath));
    }

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

            // 数据行：从第二行开始
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

    private static string TrimCell(IXLCell cell)
    {
        var value = cell.GetString();
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
