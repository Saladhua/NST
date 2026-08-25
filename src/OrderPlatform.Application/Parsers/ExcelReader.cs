using ClosedXML.Excel;
using NPOI.SS.UserModel;

namespace OrderPlatform.Application.Parsers;

/// <summary>Excel 原始网格：一个 sheet 的全部单元格文本矩阵。</summary>
public class ExcelGrid
{
    /// <summary>sheet 名。</summary>
    public string SheetName { get; set; } = string.Empty;

    /// <summary>单元格文本矩阵（Cells[行][列]，日期统一为 yyyy-MM-dd 文本）。</summary>
    public List<List<string>> Cells { get; set; } = new();
}

/// <summary>
/// 统一 Excel 读取器：.xlsx 用 ClosedXML，.xls 用 NPOI。
/// 输出原始网格，不假定表头位置（订单 Excel 的表头区与明细区位置不固定）。
/// </summary>
public static class ExcelReader
{
    /// <summary>读取工作簿全部 sheet 为原始网格。</summary>
    public static List<ExcelGrid> Read(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext == ".xls"
            ? ReadByNpoi(filePath)
            : ReadByClosedXml(filePath);
    }

    /// <summary>读取 .xlsx（ClosedXML）。</summary>
    private static List<ExcelGrid> ReadByClosedXml(string filePath)
    {
        var grids = new List<ExcelGrid>();
        using var workbook = new XLWorkbook(filePath);

        foreach (var sheet in workbook.Worksheets)
        {
            var grid = new ExcelGrid { SheetName = sheet.Name.Trim() };
            var used = sheet.RangeUsed();
            if (used is null)
            {
                grids.Add(grid);
                continue;
            }

            var firstCol = used.FirstColumn().ColumnNumber();
            for (var row = used.FirstRow().RowNumber(); row <= used.LastRow().RowNumber(); row++)
            {
                var cells = new List<string>();
                for (var col = 0; col < used.ColumnCount(); col++)
                {
                    var cell = sheet.Cell(row, firstCol + col);
                    var text = cell.DataType == XLDataType.DateTime
                        ? cell.GetDateTime().ToString("yyyy-MM-dd")
                        : cell.GetString().Trim();
                    cells.Add(text);
                }

                grid.Cells.Add(cells);
            }

            grids.Add(grid);
        }

        return grids;
    }

    /// <summary>读取 .xls / .xlsx（NPOI，兼容老格式）。</summary>
    private static List<ExcelGrid> ReadByNpoi(string filePath)
    {
        var grids = new List<ExcelGrid>();
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var workbook = WorkbookFactory.Create(stream);
        var formatter = new DataFormatter();

        for (var i = 0; i < workbook.NumberOfSheets; i++)
        {
            var sheet = workbook.GetSheetAt(i);
            var grid = new ExcelGrid { SheetName = sheet.SheetName.Trim() };

            for (var r = 0; r <= sheet.LastRowNum; r++)
            {
                var row = sheet.GetRow(r);
                var cells = new List<string>();
                var colCount = row?.LastCellNum ?? 0;
                for (var c = 0; c < colCount; c++)
                {
                    var cell = row?.GetCell(c);
                    string text;
                    if (cell is null)
                    {
                        text = string.Empty;
                    }
                    else if (cell.CellType == CellType.Numeric && DateUtil.IsCellDateFormatted(cell))
                    {
                        text = cell.DateCellValue.ToString("yyyy-MM-dd");
                    }
                    else
                    {
                        text = formatter.FormatCellValue(cell).Trim();
                    }

                    cells.Add(text);
                }

                grid.Cells.Add(cells);
            }

            grids.Add(grid);
        }

        return grids;
    }

    /// <summary>在网格前 maxRows 行中查找「标签」单元格同行右侧最近的非空文本（订单表头区取值）。</summary>
    public static string FindLabelValue(ExcelGrid grid, string label, int maxRows = 10)
    {
        for (var r = 0; r < Math.Min(maxRows, grid.Cells.Count); r++)
        {
            var row = grid.Cells[r];
            for (var c = 0; c < row.Count; c++)
            {
                if (!row[c].Replace("：", ":").Trim().Equals(label, StringComparison.Ordinal)
                    && !row[c].Replace("：", ":").Trim().StartsWith(label, StringComparison.Ordinal))
                {
                    continue;
                }

                // 标签形如「订单编号 0000033877」（同格粘连）时先拆
                var own = row[c].Replace(label, string.Empty).Replace("：", ":").Replace(":", string.Empty).Trim();
                if (own.Length > 0)
                {
                    return own;
                }

                // 同行右侧找第一个非空
                for (var k = c + 1; k < row.Count; k++)
                {
                    if (row[k].Trim().Length > 0)
                    {
                        return row[k].Trim();
                    }
                }
            }
        }

        return string.Empty;
    }

    /// <summary>在网格前 maxRows 行中查找包含关键字的单元格文本（客户名识别）。</summary>
    public static string FindCellContaining(ExcelGrid grid, string keyword, int maxRows = 6)
    {
        for (var r = 0; r < Math.Min(maxRows, grid.Cells.Count); r++)
        {
            foreach (var cell in grid.Cells[r])
            {
                if (cell.Contains(keyword, StringComparison.Ordinal))
                {
                    return cell.Trim();
                }
            }
        }

        return string.Empty;
    }

    /// <summary>定位包含指定表头关键字的行号（明细表头行），未命中返回 -1。</summary>
    public static int FindHeaderRow(ExcelGrid grid, string headerKeyword)
    {
        for (var r = 0; r < grid.Cells.Count; r++)
        {
            if (grid.Cells[r].Any(c => c.Trim() == headerKeyword))
            {
                return r;
            }
        }

        return -1;
    }
}
