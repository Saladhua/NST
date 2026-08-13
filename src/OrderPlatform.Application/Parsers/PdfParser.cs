using System.Globalization;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;

namespace OrderPlatform.Application.Parsers;

public interface IPdfParser
{
    Task<PdfParseResult> ParseAsync(string filePath, CancellationToken cancellationToken);
}

public partial class PdfParser : IPdfParser
{
    private const double RowClusterThreshold = 10;
    private static readonly Regex OrderNoRegex = OrderNoPattern();
    private static readonly Regex DateCnRegex = DateCnPattern();
    private static readonly Regex DateEnRegex = DateEnPattern();
    private static readonly Regex CodeRegex = CodePattern();
    private static readonly Regex OldCodeRegex = OldCodePattern();
    private static readonly Regex PriceAmountRegex = PriceAmountPattern();

    public Task<PdfParseResult> ParseAsync(string filePath, CancellationToken cancellationToken)
    {
        return Task.FromResult(Parse(filePath));
    }

    public static PdfParseResult Parse(string filePath)
    {
        var result = new PdfParseResult();
        using var document = PdfDocument.Open(filePath);

        foreach (var page in document.GetPages())
        {
            var rawText = page.Text;
            result.RawText += rawText + "\n";

            result.OrderNo = string.IsNullOrEmpty(result.OrderNo) ? ExtractOrderNo(rawText) : result.OrderNo;
            result.OrderDate ??= ExtractOrderDate(rawText);
            result.BuyerName = string.IsNullOrEmpty(result.BuyerName) ? ExtractBuyerName(rawText) : result.BuyerName;

            ParseTable(page, result);
        }

        return result;
    }

    /// <summary>
    /// 按坐标把页面文字解析为表格行。
    /// 布局A（华尔达）：表头 存货编码|存货名称|规格型号|单位|数量|单价|金额|表体备注|订单号，编码列折行。
    /// 布局B（马鞍山仪达）：表头 行号|存货名称|存货编码|老编码|数量|单位|件数|交货日期|备注，中文乱码但可读编码/数量。
    /// </summary>
    private static void ParseTable(UglyToad.PdfPig.Content.Page page, PdfParseResult result)
    {
        var words = page.GetWords()
            .Where(w => !string.IsNullOrWhiteSpace(w.Text))
            .Select(w => new WordItem
            {
                Text = Normalize(w.Text),
                X = w.BoundingBox.Left,
                Y = w.BoundingBox.Top
            })
            .ToList();

        if (words.Count == 0)
        {
            return;
        }

        // 1. 找表头单词的 y：包含任一表头特征词
        var headerAnchor = words.FirstOrDefault(w =>
            w.Text.Contains("存货编码") || w.Text.Contains("存货名称") ||
            w.Text.Contains("规格型号") || w.Text == "行号");
        if (headerAnchor is null)
        {
            return;
        }

        var headerY = headerAnchor.Y;
        // 表头聚类：y 与 headerY 差 < 6 的词
        var headerWords = words
            .Where(w => Math.Abs(w.Y - headerY) < 6)
            .OrderBy(w => w.X)
            .ToList();

        var isYidaLayout = headerWords.Any(h => h.Text == "行号");
        var headerXs = headerWords.Select(h => h.X).ToList();

        // 2. 数据行聚类
        var dataWords = words
            .Where(w => w.Y < headerY - 6)
            .OrderBy(w => w.Y)
            .ToList();

        var rows = ClusterRows(dataWords);

        // 3. 每行按列归位：每个词归入最近的表头列
        foreach (var row in rows)
        {
            var cells = AssignCells(row, headerWords, isYidaLayout);
            var item = BuildRow(cells, isYidaLayout, result.Rows.Count + 1);
            if (IsValidOrderLine(item))
            {
                result.Rows.Add(item);
            }
        }
    }

    /// <summary>
    /// 判断是否为有效订单行：
    /// 过滤页眉/页脚/条款文本行（数量为 0 且编码不满足物料编码格式）。
    /// 物料编码特征：含 "*"（如 03.02.18*1.8*222/...）或以 H/P 开头（如 H110081008 / P100079109）。
    /// </summary>
    private static bool IsValidOrderLine(PdfParseRow item)
    {
        var code = item.MaterialCode ?? string.Empty;
        var spec = item.Spec ?? string.Empty;

        // 编码与规格均为空：非物料行（页眉/备注/合计文本），过滤
        if (string.IsNullOrWhiteSpace(code) && string.IsNullOrWhiteSpace(spec))
        {
            return false;
        }

        if (item.Quantity > 0)
        {
            return true;
        }

        return code.Contains('*') || code.StartsWith("H", StringComparison.OrdinalIgnoreCase) || code.StartsWith("P", StringComparison.OrdinalIgnoreCase);
    }

    private static List<List<WordItem>> ClusterRows(List<WordItem> words)
    {
        var rows = new List<List<WordItem>>();
        var current = new List<WordItem>();
        double? lastY = null;

        foreach (var word in words)
        {
            if (lastY is not null && Math.Abs(word.Y - lastY.Value) > RowClusterThreshold)
            {
                if (current.Count > 0)
                {
                    rows.Add(current);
                }

                current = new List<WordItem>();
            }

            current.Add(word);
            lastY = word.Y;
        }

        if (current.Count > 0)
        {
            rows.Add(current);
        }

        return rows;
    }

    /// <summary>把一行词按最近表头 x 归列，返回 [表头词文本 -> 单元格文本]（同列多词拼接）。</summary>
    private static Dictionary<string, string> AssignCells(List<WordItem> row, List<WordItem> headers, bool isYidaLayout)
    {
        var cell = new Dictionary<string, string>();
        var headerList = headers
            .Select((h, idx) => new { h, idx })
            .ToList();

        foreach (var word in row)
        {
            var nearest = headerList
                .OrderBy(item => Math.Abs(word.X - item.h.X))
                .FirstOrDefault();
            if (nearest is null)
            {
                continue;
            }

            var key = nearest.h.Text + "#" + nearest.idx;
            cell[key] = cell.TryGetValue(key, out var existing) ? existing + word.Text : word.Text;
        }

        return cell;
    }

    private static PdfParseRow BuildRow(Dictionary<string, string> cells, bool isYidaLayout, int lineNo)
    {
        var item = new PdfParseRow { LineNo = lineNo };

        if (!isYidaLayout)
        {
            // 布局A：按表头文本匹配
            item.MaterialCode = GetHeaderCell(cells, "存货编码") ?? string.Empty;
            item.MaterialName = GetHeaderCell(cells, "存货名称") ?? string.Empty;
            item.Spec = (GetHeaderCell(cells, "规格型号") ?? GetHeaderCell(cells, "规格")) ?? string.Empty;
            item.Unit = GetHeaderCell(cells, "单位") ?? string.Empty;
            item.Quantity = ParseDecimal(GetHeaderCell(cells, "数量"));
            item.Remark = (GetHeaderCell(cells, "表体备注") ?? GetHeaderCell(cells, "备注")) ?? string.Empty;

            // 单价/金额（可能粘连在一个词里）
            var priceCell = GetHeaderCell(cells, "单价") ?? string.Empty;
            var amountCell = GetHeaderCell(cells, "金额") ?? string.Empty;
            TryParsePriceAmount(priceCell, amountCell, out var price, out var amount);
            item.Price = price;
            item.Amount = amount;
        }
        else
        {
            // 布局B：位置固定 行号|名称|编码|老编码|数量|单位|件数|交期|备注
            // 表头词按 x 排序：
            // 0 行号, 1 存货名称, 2 存货编码, 3 老编码, 4 数量, 5 单位, 6 件数, 7 交货日期, 8 备注
            var ordered = cells.OrderBy(kv =>
            {
                var idxPart = kv.Key.Substring(kv.Key.LastIndexOf('#') + 1);
                return int.Parse(idxPart);
            }).ToList();

            if (ordered.Count >= 3)
            {
                item.MaterialCode = ordered[2].Value.Trim();
            }

            if (ordered.Count >= 4)
            {
                var oldCode = ordered[3].Value.Trim();
                if (oldCode.Length > 0)
                {
                    if (string.IsNullOrEmpty(item.MaterialName))
                    {
                        item.MaterialName = oldCode;
                    }
                }
            }

            if (ordered.Count >= 5)
            {
                item.Quantity = ParseDecimal(ordered[4].Value);
            }

            if (ordered.Count >= 6)
            {
                item.Unit = ordered[5].Value.Trim();
            }

            if (ordered.Count >= 8)
            {
                item.ReceiveDate = ParseDate(ordered[7].Value);
            }

            if (ordered.Count >= 9)
            {
                item.Remark = ordered[8].Value.Trim();
            }

            // 兜底：通过正则从整行原始文本提取编码/老编码
            if (string.IsNullOrEmpty(item.MaterialCode))
            {
                var raw = string.Concat(ordered.Select(kv => kv.Value));
                item.MaterialCode = CodeRegex.Matches(raw).Select(m => m.Value).FirstOrDefault() ?? string.Empty;
                var old = OldCodeRegex.Match(raw);
                if (old.Success && string.IsNullOrEmpty(item.MaterialName))
                {
                    item.MaterialName = old.Value;
                }
            }
        }

        item.Raw = string.Join(" | ", cells.Values.Select(v => v.Trim()));
        return item;
    }

    private static string? GetHeaderCell(Dictionary<string, string> cells, string headerText)
    {
        // 匹配表头词为 headerText 的单元格（键 = headerText#idx）
        var key = cells.Keys.FirstOrDefault(k => k.StartsWith(headerText + "#", StringComparison.Ordinal));
        return key is null ? null : cells[key];
    }

    private static void TryParsePriceAmount(string priceCell, string amountCell, out decimal price, out decimal amount)
    {
        price = ParseDecimal(priceCell);
        amount = ParseDecimal(amountCell);

        if (amount == 0 && !string.IsNullOrEmpty(priceCell) && priceCell.Contains(".00"))
        {
            // 粘连形式：单价+金额，如 11.800130980.00 => 11.80 / 130980.00
            var m = PriceAmountRegex.Match(priceCell);
            if (m.Success)
            {
                price = ParseDecimal(m.Groups["p"].Value);
                amount = ParseDecimal(m.Groups["a"].Value);
                return;
            }

            // 可能单价本身含金额：例如 "11.800130980.00" 拆开
            var dotCount = priceCell.Count(c => c == '.');
            if (dotCount == 2)
            {
                var idx1 = priceCell.IndexOf('.');
                var idx2 = priceCell.LastIndexOf('.');
                var p = priceCell[..(idx2 - 2)];
                var a = priceCell[(idx2 - 2)..];
                if (decimal.TryParse(p, NumberStyles.Any, CultureInfo.InvariantCulture, out var pv)
                    && decimal.TryParse(a, NumberStyles.Any, CultureInfo.InvariantCulture, out var av))
                {
                    price = pv;
                    amount = av;
                }
            }
        }
    }

    private static decimal ParseDecimal(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        var cleaned = text.Replace(",", string.Empty);
        if (decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        return 0;
    }

    private static DateTime? ParseDate(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (DateTime.TryParseExact(text.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d1))
        {
            return d1;
        }

        return null;
    }

    private static string ExtractOrderNo(string text)
    {
        var match = OrderNoRegex.Match(text);
        return match.Success ? match.Value : string.Empty;
    }

    private static DateTime? ExtractOrderDate(string text)
    {
        var m1 = DateCnRegex.Match(text);
        if (m1.Success
            && int.TryParse(m1.Groups["y"].Value, out var y)
            && int.TryParse(m1.Groups["m"].Value, out var mo)
            && int.TryParse(m1.Groups["d"].Value, out var d))
        {
            return new DateTime(y, mo, d);
        }

        var m2 = DateEnRegex.Match(text);
        if (m2.Success)
        {
            return ParseDate(m2.Value);
        }

        return null;
    }

    private static string ExtractBuyerName(string text)
    {
        if (text.Contains("华尔达"))
        {
            return "华尔达";
        }

        if (text.Contains("Yida") || text.Contains("Maanshan") || text.Contains("仪达"))
        {
            return "马鞍山仪达";
        }

        if (text.Contains("创达"))
        {
            return "创达";
        }

        if (text.Contains("法拉达"))
        {
            return "法拉达";
        }

        if (text.Contains("三可"))
        {
            return "三可";
        }

        return string.Empty;
    }

    private static string Normalize(string text) => text.Trim();

    [GeneratedRegex(@"(PO-?\d[\d\-]*|CGDD\d+)")]
    private static partial Regex OrderNoPattern();

    [GeneratedRegex(@"(\d{4})年(\d{1,2})月(\d{1,2})日")]
    private static partial Regex DateCnPattern();

    [GeneratedRegex(@"\d{4}-\d{2}-\d{2}")]
    private static partial Regex DateEnPattern();

    [GeneratedRegex(@"[HP]\d{5,}")]
    private static partial Regex CodePattern();

    [GeneratedRegex(@"DS[\w\-]+")]
    private static partial Regex OldCodePattern();

    [GeneratedRegex(@"(?<p>\d+\.\d{1,2})(?<a>\d+\.\d{2})")]
    private static partial Regex PriceAmountPattern();

    private sealed class WordItem
    {
        public string Text { get; set; } = string.Empty;

        public double X { get; set; }

        public double Y { get; set; }
    }
}