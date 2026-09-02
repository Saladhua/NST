using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PDFtoImage;
using RapidOcrNet;
using SkiaSharp;
using UglyToad.PdfPig;

namespace OrderPlatform.Application.Parsers;

/// <summary>OCR 订单解析器接口：解析无文字层的 PDF（如创达的曲线化订单）。</summary>
public interface IOcrOrderParser
{
    /// <summary>判断 PDF 是否无文字层（需要 OCR）。</summary>
    bool NeedsOcr(string filePath);

    /// <summary>渲染 PDF 并 OCR 识别，按坐标重建表格解析订单。</summary>
    PdfParseResult Parse(string filePath, CancellationToken cancellationToken = default);
}

/// <summary>
/// 基于 PDFtoImage 渲染 + RapidOcrNet（PP-OCR 中文模型）的订单解析器。
/// 流程：渲染页面位图 → OCR 得到带坐标文本块 → 表头锚点定列 → 行聚类 → 按列归位。
/// 当前适配创达布局：物料编码|物料名称|客商物料编码|规格|主单位|数量|含税单价|价税合计|到货日期|使用部门|行备注。
/// </summary>
public partial class OcrOrderParser : IOcrOrderParser
{
    /// <summary>渲染 DPI（200：页面 842pt → 约 2339px，中文小字识别率更高）。可用 Ocr:RenderDpi 配置覆盖。</summary>
    private readonly int _renderDpi;

    private static readonly Regex OrderNoRegex = OcrOrderNoPattern();
    private static readonly Regex DateRegex = OcrDatePattern();
    private static readonly Regex CodePattern = OcrCodePattern();

    private readonly Lazy<RapidOcr> _ocr;
    private readonly SemaphoreSlim _ocrLock = new(1, 1);
    private readonly ILogger<OcrOrderParser> _logger;

    public OcrOrderParser(ILogger<OcrOrderParser> logger, IConfiguration? configuration = null)
    {
        _logger = logger;
        _renderDpi = configuration?.GetValue<int>("Ocr:RenderDpi") ?? 200;

        // 模型目录可用 Ocr:ModelDir 配置覆盖，默认取应用目录下的 models/v5
        var modelDir = configuration?["Ocr:ModelDir"] ?? AppContext.BaseDirectory;
        _ocr = new Lazy<RapidOcr>(() =>
        {
            var detPath = Path.Combine(modelDir, "models", "v5", "ch_PP-OCRv5_mobile_det.onnx");
            var clsPath = Path.Combine(modelDir, "models", "v5", "ch_PP-LCNet_x0_25_textline_ori_cls_mobile.onnx");
            var recPath = Path.Combine(modelDir, "models", "v5", "ch_PP-OCRv5_rec_mobile.onnx");
            var keysPath = Path.Combine(modelDir, "models", "v5", "ppocrv5_dict.txt");

            if (!File.Exists(recPath) || !File.Exists(keysPath))
            {
                throw new InvalidOperationException("OCR 中文模型缺失，请确认 models/v5 目录包含 ch_PP-OCRv5_rec_mobile.onnx 与 ppocrv5_dict.txt");
            }

            var ocr = new RapidOcr();
            ocr.InitModels(detPath, clsPath, recPath, keysPath);
            _logger.LogInformation("RapidOcr 模型初始化完成");
            return ocr;
        }, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <inheritdoc />
    public bool NeedsOcr(string filePath)
    {
        try
        {
            using var document = PdfDocument.Open(filePath);
            var page = document.GetPages().FirstOrDefault();
            if (page is null)
            {
                return false;
            }

            return !page.GetWords().Any();
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc />
    public PdfParseResult Parse(string filePath, CancellationToken cancellationToken = default)
    {
        var result = new PdfParseResult();
        _ = _ocr.Value;

        var pdfBytes = File.ReadAllBytes(filePath);
        var pageCount = 1;
        try
        {
            using var doc = UglyToad.PdfPig.PdfDocument.Open(filePath);
            pageCount = doc.NumberOfPages;
        }
        catch
        {
            // 页数获取失败时按单页处理
        }

        for (var page = 0; page < pageCount; page++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var bitmap = RenderPage(pdfBytes, page, cancellationToken);
            var ocrResult = RunOcr(bitmap, cancellationToken);

            var words = ocrResult.TextBlocks
                .Where(b => !string.IsNullOrWhiteSpace(b.Text))
                .Select(b => new OcrWord
                {
                    Text = b.Text.Trim(),
                    X = b.BoxPoints.Min(p => p.X),
                    Y = b.BoxPoints.Min(p => p.Y),
                    Width = b.BoxPoints.Max(p => p.X) - b.BoxPoints.Min(p => p.X),
                    Height = b.BoxPoints.Max(p => p.Y) - b.BoxPoints.Min(p => p.Y)
                })
                .ToList();

            result.RawText += string.Join("\n", words.Select(w => w.Text)) + "\n";

            // 页面级信息
            var fullText = result.RawText;
            result.OrderNo = string.IsNullOrEmpty(result.OrderNo) ? ExtractOrderNo(fullText) : result.OrderNo;
            result.OrderDate ??= ExtractDate(fullText);
            result.BuyerName = string.IsNullOrEmpty(result.BuyerName) ? ExtractBuyerName(fullText) : result.BuyerName;

            ParseTable(words, result);
        }

        // 创达：订单号优先取行备注中的编号
        var remarkOrderNo = PdfParser.ExtractOrderNoFromRemarks(result.Rows);
        if (remarkOrderNo.Length > 0)
        {
            result.OrderNo = remarkOrderNo;
        }

        return result;
    }

    /// <summary>渲染 PDF 页为位图：高 DPI 打开失败时自动降级重试（兼容部分曲线化 PDF 在高 DPI 渲染崩溃的情况）。</summary>
    private SKBitmap RenderPage(byte[] pdfBytes, int page, CancellationToken cancellationToken)
    {
        var dpis = new[] { _renderDpi, 150, 100, 72 };
        Exception? lastException = null;
        foreach (var dpi in dpis.Distinct())
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return Conversion.ToImage(pdfBytes, page, null, new RenderOptions(dpi));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                lastException = ex;
                _logger.LogWarning("PDF 第 {Page} 页按 {Dpi} DPI 渲染失败：{Message}，尝试降级", page, dpi, ex.Message);
            }
        }

        throw lastException ?? new PDFtoImage.Exceptions.PdfUnknownException();
    }

    /// <summary>执行 OCR（串行化，避免并发使用同一 ONNX 会话）。</summary>
    private OcrResult RunOcr(SKBitmap bitmap, CancellationToken cancellationToken)
    {
        _ocrLock.Wait(cancellationToken);
        try
        {
            var options = RapidOcrOptions.Default with { ImgResize = (int)(bitmap.Width > bitmap.Height ? bitmap.Width : bitmap.Height), DoAngle = false };
            return _ocr.Value.Detect(bitmap, options);
        }
        finally
        {
            _ocrLock.Release();
        }
    }

    /// <summary>按坐标把 OCR 文本块重建为表格行。行锚点 = 编码列内的纯数字编码块；行归属按锚点 y 条带切分；列归属按表头词合并后的列区间归位。</summary>
    private void ParseTable(List<OcrWord> words, PdfParseResult result)
    {
        // 1. 表头锚点：包含「物料编码」的块
        var headerAnchor = words.FirstOrDefault(w => w.Text.StartsWith("物料编码", StringComparison.Ordinal))
            ?? words.FirstOrDefault(w => w.Text.Contains("物料编码") || w.Text.Contains("存货编码"));
        if (headerAnchor is null)
        {
            return;
        }

        var headerWords = words
            .Where(w => Math.Abs(w.Y - headerAnchor.Y) < Math.Max(headerAnchor.Height, w.Height) * 0.8)
            .OrderBy(w => w.X)
            .ToList();

        if (headerWords.Count < 3)
        {
            return;
        }

        var columns = BuildColumns(headerWords);
        var codeCol = columns.FirstOrDefault(c => c.Name.StartsWith("物料编码", StringComparison.Ordinal));
        if (codeCol is null)
        {
            return;
        }

        // 排除表尾合计行：以「合计」标签词为界，其 y 之后的块（合计/总数量/总金额）不再参与行归属
        var totalAnchor = words.FirstOrDefault(w => w.Y > headerAnchor.Y && w.Text.Trim() == "合计");
        var tableBottom = totalAnchor is null ? double.MaxValue : totalAnchor.Y;
        var dataWords = words
            .Where(w => w.Y > headerAnchor.Y + headerAnchor.Height * 0.8)
            .Where(w => w.Y < tableBottom)
            .ToList();

        // 2. 行锚点：编码列区间内的纯数字编码块（排除备注/合计/文本块被误当行锚点）
        var anchors = dataWords
            .Where(w => CenterX(w) >= codeCol.Left && CenterX(w) <= codeCol.Right)
            .Where(w => OcrCodePattern().IsMatch(CleanCode(w.Text)))
            .OrderBy(w => w.Y)
            .ToList();

        if (anchors.Count == 0)
        {
            return;
        }

        // 3. 行归属：相邻锚点 y 的中点切分条带，数据块按 y 落带归行（同一逻辑行的折行/备注碎片不再错配到相邻行）
        var rowBuckets = anchors.ToDictionary(a => a, _ => new List<OcrWord>());
        foreach (var word in dataWords)
        {
            for (var i = 0; i < anchors.Count; i++)
            {
                var lower = i == 0 ? double.MinValue : anchors[i - 1].Y + (anchors[i].Y - anchors[i - 1].Y) / 2;
                var upper = i == anchors.Count - 1 ? double.MaxValue : anchors[i].Y + (anchors[i + 1].Y - anchors[i].Y) / 2;
                if (word.Y >= lower && word.Y < upper)
                {
                    rowBuckets[anchors[i]].Add(word);
                    break;
                }
            }
        }

        // 4. 每行按列区间归位（列边界以表头词为准，右缘含表格边框外的尾部碎片）
        var lineNo = result.Rows.Count;
        foreach (var anchor in anchors)
        {
            var row = rowBuckets[anchor];
            var cells = AssignCellsByRegion(row, columns);
            var item = BuildRow(cells, lineNo + 1);
            if (IsValidOrderLine(item))
            {
                result.Rows.Add(item);
                lineNo++;
            }
        }
    }

    /// <summary>文本块 x 中心。</summary>
    private static double CenterX(OcrWord word) => word.X + word.Width / 2;

    /// <summary>表头词转列区间：把横向紧邻的拆词合并为一列（如「行」「备注」→「行备注」），相邻列以中点划分边界。</summary>
    private static List<ColumnRegion> BuildColumns(List<OcrWord> headerWords)
    {
        // 拆词间隙：OCR 把列标题拆开时横向相距通常为 0~字高，而列与列之间有空档，取字高的 0.25 作为合并阈值
        var mergeGap = headerWords.Max(w => w.Height) * 0.25;
        var columns = new List<ColumnRegion>();
        foreach (var w in headerWords.OrderBy(x => x.X))
        {
            var right = w.X + w.Width;
            var last = columns.Count > 0 ? columns[^1] : null;
            if (last is not null && w.X <= last.Right + mergeGap)
            {
                last.Name += w.Text;
                last.Left = Math.Min(last.Left, w.X);
                last.Right = Math.Max(last.Right, right);
            }
            else
            {
                columns.Add(new ColumnRegion { Name = w.Text, Left = w.X, Right = right });
            }
        }

        if (columns.Count == 0)
        {
            return columns;
        }

        // 首列左边界、末列右边界向外扩展，避免边框外数据块丢失；相邻列以中点划分
        columns[0].Left = double.MinValue;
        for (var i = 0; i < columns.Count - 1; i++)
        {
            var mid = (columns[i].Right + columns[i + 1].Left) / 2;
            columns[i].Right = mid;
            columns[i + 1].Left = mid;
        }

        columns[^1].Right = double.MaxValue;
        return columns;
    }

    /// <summary>按列区间归位：数据块中心落在哪个列区间即归该列；区间外则回到最近列中心。</summary>
    private static Dictionary<string, string> AssignCellsByRegion(List<OcrWord> row, List<ColumnRegion> columns)
    {
        var cells = new Dictionary<string, string>();
        foreach (var word in row.OrderBy(w => w.X))
        {
            var center = CenterX(word);
            var col = columns.FirstOrDefault(c => center >= c.Left && center <= c.Right)
                ?? columns.OrderBy(c => Math.Abs(center - (c.Left + c.Right) / 2)).FirstOrDefault();
            if (col is null)
            {
                continue;
            }

            cells[col.Name] = cells.TryGetValue(col.Name, out var existing)
                ? existing + word.Text
                : word.Text;
        }

        return cells;
    }

    /// <summary>表格列区间（合并后的列名 + 左右 x 边界）。</summary>
    private sealed class ColumnRegion
    {
        public string Name { get; set; } = string.Empty;

        public double Left { get; set; }

        public double Right { get; set; }
    }

    /// <summary>根据单元格字典构建明细行（创达列映射；表头按前缀匹配避免「客商物料编码」误命中）。</summary>
    private static PdfParseRow BuildRow(Dictionary<string, string> cells, int lineNo)
    {
        string Cell(params string[] names)
        {
            foreach (var name in names)
            {
                var key = cells.Keys.FirstOrDefault(k => k.StartsWith(name, StringComparison.Ordinal));
                if (key is not null)
                {
                    return cells[key];
                }
            }

            return string.Empty;
        }

        var spec = Cell("规格");
        return new PdfParseRow
        {
            LineNo = lineNo,
            MaterialCode = CleanCode(Cell("物料编码")),
            MaterialName = Cell("物料名称"),
            Spec = spec,
            Unit = Cell("主单位"),
            Quantity = ParseDecimal(Cell("数量")),
            Price = ParseDecimal(Cell("含税单价")),
            Amount = ParseDecimal(Cell("价税合计")),
            ReceiveDate = ParseDate(Cell("到货日期")),
            Remark = Cell("行备注") is var r && r.Length > 0 ? r : Cell("备注"),
            Raw = string.Join(" | ", cells.Select(kv => kv.Key + ":" + kv.Value))
        };
    }

    /// <summary>是否有效明细行（物料编码为 8 位以上数字/编码）。</summary>
    private static bool IsValidOrderLine(PdfParseRow item)
    {
        var code = item.MaterialCode ?? string.Empty;
        if (code.Length == 0)
        {
            return false;
        }

        if (code.Contains("合计"))
        {
            return false;
        }

        return code.Length >= 8 || item.Quantity > 0;
    }

    /// <summary>清洗 OCR 编码文本（去空格与常见误识字符）。</summary>
    private static string CleanCode(string text)
    {
        return text.Replace(" ", string.Empty).Replace("口", "0").Replace("O", "0").Replace("l", "1").Replace("|", string.Empty);
    }

    /// <summary>解析小数（去除千分位与 OCR 误识分隔符）。</summary>
    private static decimal ParseDecimal(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        var cleaned = text.Replace(",", string.Empty).Replace("，", string.Empty).Replace(" ", string.Empty);
        return decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0;
    }

    /// <summary>解析日期。</summary>
    private static DateTime? ParseDate(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var t = text.Trim();
        if (DateTime.TryParseExact(t, new[] { "yyyy-MM-dd", "yyyy/M/d", "yyyy-M-d", "yyyy.MM.dd" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            || DateTime.TryParse(t, CultureInfo.InvariantCulture, DateTimeStyles.None, out d))
        {
            if (d.Year > 2000 && d.Year < 2100)
            {
                return d;
            }
        }

        return null;
    }

    /// <summary>提取订单号（CD 开头 / PO- / CGDD）。</summary>
    private static string ExtractOrderNo(string text)
    {
        var match = OrderNoRegex.Match(text);
        return match.Success ? match.Value : string.Empty;
    }

    /// <summary>提取订单日期。</summary>
    private static DateTime? ExtractDate(string text)
    {
        var m = DateRegex.Match(text);
        if (m.Success && DateTime.TryParseExact(m.Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
        {
            return d;
        }

        return null;
    }

    /// <summary>识别客户（创达）。</summary>
    private static string ExtractBuyerName(string text)
    {
        if (text.Contains("创达"))
        {
            return "创达";
        }

        if (text.Contains("华尔达"))
        {
            return "华尔达";
        }

        if (text.Contains("三可"))
        {
            return "三可";
        }

        // 法拉达客户已更名「发润达」，两名称并存于客户库，按文件实际用名识别
        if (text.Contains("发润达"))
        {
            return "发润达";
        }

        if (text.Contains("法拉达"))
        {
            return "法拉达";
        }

        if (text.Contains("仪达") || text.Contains("Yida"))
        {
            return "马鞍山仪达";
        }

        return string.Empty;
    }

    /// <summary>订单号模式：CD 长编号 / PO- / CGDD。</summary>
    [GeneratedRegex(@"(CD\d{10,}|PO-?\d[\d\-]*|CGDD\d+)")]
    private static partial Regex OcrOrderNoPattern();

    /// <summary>日期模式。</summary>
    [GeneratedRegex(@"\d{4}-\d{2}-\d{2}")]
    private static partial Regex OcrDatePattern();

    /// <summary>物料编码模式：8 位以上数字。</summary>
    [GeneratedRegex(@"\d{8,}")]
    private static partial Regex OcrCodePattern();

    /// <summary>OCR 文本块（含坐标）。</summary>
    private sealed class OcrWord
    {
        public string Text { get; set; } = string.Empty;

        public double X { get; set; }

        public double Y { get; set; }

        public double Width { get; set; }

        public double Height { get; set; }
    }
}
