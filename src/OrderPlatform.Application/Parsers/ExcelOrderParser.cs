using System.Globalization;

namespace OrderPlatform.Application.Parsers;

/// <summary>Excel 订单解析器接口：识别并解析订单类 Excel（三可 / 法拉达）。</summary>
public interface IExcelOrderParser
{
    /// <summary>判断工作簿是否为订单文件（表头区含「采购单号 / 订单编号」特征）。</summary>
    bool IsOrderWorkbook(List<ExcelGrid> grids);

    /// <summary>解析订单 Excel（表头信息 + 明细行）。</summary>
    PdfParseResult Parse(List<ExcelGrid> grids);
}

/// <summary>
/// Excel 订单解析器实现。
/// 三可布局：表头区（订单日期 / 订单编号 / 供货单位）+ 明细列（序号|存货编码|存货名称|规格型号|图号|材质|计量|净重|数量|到货日期|备注）。
/// 法拉达布局：表头区（采购单号 / 采购日期 / 供应商编码）+ 明细列（序号|料件编号|供应商产品编码|品名|规格|旧料号|采购量|单位|单价|税前金额|交货日期|含税金额|备注|物料描述）。
/// </summary>
public class ExcelOrderParser : IExcelOrderParser
{
    private static readonly string[] OrderHeaderLabels = { "采购单号", "订单编号" };

    /// <inheritdoc />
    public bool IsOrderWorkbook(List<ExcelGrid> grids)
    {
        return grids.Any(g => OrderHeaderLabels.Any(l => FindLabelRow(g, l) >= 0));
    }

    /// <inheritdoc />
    public PdfParseResult Parse(List<ExcelGrid> grids)
    {
        var result = new PdfParseResult();
        foreach (var grid in grids)
        {
            if (OrderHeaderLabels.All(l => FindLabelRow(grid, l) < 0))
            {
                continue;
            }

            // 保存原始明细表（表头 + 明细行），供上传记录「查看数据」展示
            result.Sheets.Add(ToSheetData(grid));

            ParseSheet(grid, result);
        }

        return result;
    }

    /// <summary>从原始网格提取「明细表头行 + 其后数据行」为通用表结构。</summary>
    private static ExcelSheetData ToSheetData(ExcelGrid grid)
    {
        var data = new ExcelSheetData { SheetName = grid.SheetName };
        var headerRow = ExcelReader.FindHeaderRow(grid, "存货编码");
        if (headerRow < 0)
        {
            headerRow = ExcelReader.FindHeaderRow(grid, "料件编号");
        }

        if (headerRow < 0 || headerRow >= grid.Cells.Count)
        {
            return data;
        }

        var headers = grid.Cells[headerRow];
        data.Headers = headers.Select(h => h.Trim()).ToList();
        for (var r = headerRow + 1; r < grid.Cells.Count; r++)
        {
            var cells = grid.Cells[r];
            var row = new Dictionary<string, string>();
            var anyValue = false;
            for (var c = 0; c < headers.Count && c < cells.Count; c++)
            {
                var value = cells[c].Trim();
                row[headers[c].Trim()] = value;
                if (value.Length > 0)
                {
                    anyValue = true;
                }
            }

            if (anyValue)
            {
                data.Rows.Add(row);
            }
        }

        return data;
    }

    /// <summary>解析单个订单 sheet。</summary>
    private static void ParseSheet(ExcelGrid grid, PdfParseResult result)
    {
        // 表头区：订单号 / 日期 / 客户
        var orderNo = FindLabelValue(grid, "采购单号");
        if (string.IsNullOrEmpty(orderNo))
        {
            orderNo = FindLabelValue(grid, "订单编号");
        }

        if (result.OrderNo.Length == 0 && orderNo.Length > 0)
        {
            result.OrderNo = orderNo;
        }

        var dateText = FindLabelValue(grid, "采购日期");
        if (string.IsNullOrEmpty(dateText))
        {
            dateText = FindLabelValue(grid, "订单日期");
        }

        result.OrderDate ??= ParseDate(dateText);

        // 客户识别：表头区含公司名的单元格（采购方）
        // 注：法拉达客户现已更名为「发润达」（广东发润达汽车零部件有限公司），两者等价识别
        foreach (var keyword in new[] { "三可", "发润达", "法拉达" })
        {
            var company = ExcelReader.FindCellContaining(grid, keyword);
            if (company.Length > 0)
            {
                result.BuyerName = keyword;
                break;
            }
        }

        // 明细表头行：三可「存货编码」/ 法拉达「料件编号」
        var headerRow = ExcelReader.FindHeaderRow(grid, "存货编码");
        if (headerRow < 0)
        {
            headerRow = ExcelReader.FindHeaderRow(grid, "料件编号");
        }

        if (headerRow < 0)
        {
            return;
        }

        var headers = grid.Cells[headerRow];
        var lineNo = result.Rows.Count;
        for (var r = headerRow + 1; r < grid.Cells.Count; r++)
        {
            var cells = grid.Cells[r];
            string Cell(string name)
            {
                for (var c = 0; c < headers.Count && c < cells.Count; c++)
                {
                    if (headers[c].Trim() == name)
                    {
                        return cells[c].Trim();
                    }
                }

                return string.Empty;
            }

            var code = Cell("存货编码");
            if (code.Length == 0)
            {
                code = Cell("料件编号");
            }

            var specText = Cell("规格型号") is var s && s.Length > 0 ? s : Cell("规格");
            var nameText = Cell("存货名称") is var n && n.Length > 0 ? n : Cell("品名");

            // 合计行 / 空行跳过（合计行特征：编码缺失，或编码/规格/品名不成结构）
            if (code.Length == 0 || code.Contains("合计") || (specText.Length == 0 && nameText.Length == 0))
            {
                continue;
            }

            var quantity = ParseDecimal(Cell("数量")) + ParseDecimal(Cell("采购量"));
            var remark = string.Join("; ", new[] { Cell("图号"), Cell("物料描述"), Cell("备注") }.Where(v => v.Length > 0));

            lineNo++;
            result.Rows.Add(new PdfParseRow
            {
                LineNo = lineNo,
                MaterialCode = code,
                MaterialName = nameText,
                Spec = specText,
                Material = Cell("材质"),
                Unit = Cell("计量") is var u && u.Length > 0 ? u : Cell("单位"),
                Quantity = quantity,
                Price = ParseDecimal(Cell("单价")),
                Amount = ParseDecimal(Cell("含税金额")) + ParseDecimal(Cell("税前金额")),
                ReceiveDate = ParseDate(Cell("到货日期")) ?? ParseDate(Cell("交货日期")),
                Remark = remark,
                Raw = string.Join(" | ", cells.Where(v => v.Trim().Length > 0))
            });
        }
    }

    /// <summary>查找标签所在行号，未命中返回 -1。</summary>
    private static int FindLabelRow(ExcelGrid grid, string label)
    {
        for (var r = 0; r < Math.Min(10, grid.Cells.Count); r++)
        {
            foreach (var cell in grid.Cells[r])
            {
                var t = cell.Replace("：", ":").Trim();
                if (t == label || t.StartsWith(label, StringComparison.Ordinal))
                {
                    return r;
                }
            }
        }

        return -1;
    }

    /// <summary>查找标签取值。</summary>
    private static string FindLabelValue(ExcelGrid grid, string label)
    {
        return ExcelReader.FindLabelValue(grid, label);
    }

    /// <summary>解析小数（去除千分位逗号）。</summary>
    private static decimal ParseDecimal(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        return decimal.TryParse(text.Replace(",", string.Empty), NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0;
    }

    /// <summary>解析日期（yyyy-MM-dd / yyyy/M/d 等）。</summary>
    private static DateTime? ParseDate(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var t = text.Trim();
        if (DateTime.TryParse(t, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            || DateTime.TryParseExact(t, new[] { "yyyy-MM-dd", "yyyy/M/d", "yyyy-M-d", "yyyy.MM.dd" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out d))
        {
            if (d.Year > 2000 && d.Year < 2100)
            {
                return d;
            }
        }

        return null;
    }
}
