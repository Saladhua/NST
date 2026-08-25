using System.Globalization;
using System.Text.RegularExpressions;
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
    /// <summary>渲染 DPI（150：页面 842pt → 约 1754px，明细文字高约 15px）。</summary>
    private const int RenderDpi = 150;

    private static readonly Regex OrderNoRegex = OcrOrderNoPattern();
    private static readonly Regex DateRegex = OcrDatePattern();
    private static readonly Regex CodePattern = OcrCodePattern();

    private readonly RapidOcr _ocr = new();
    private readonly SemaphoreSlim _ocrLock = new(1, 1);
    private readonly ILogger<OcrOrderParser> _logger;
    private bool _initialized;

    public OcrOrderParser(ILogger<OcrOrderParser> logger)
    {
        _logger = logger;
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
        EnsureInitialized();

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

            using var bitmap = Conversion.ToImage(pdfBytes, page, null, new RenderOptions(RenderDpi));
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

    /// <summary>初始化 OCR 模型（中文识别模型 + 内置检测/分类模型）。</summary>
    private void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        lock (_ocrLock)
        {
            if (_initialized)
            {
                return;
            }

            var baseDir = AppContext.BaseDirectory;
            var detPath = Path.Combine(baseDir, "models", "v5", "ch_PP-OCRv5_mobile_det.onnx");
            var clsPath = Path.Combine(baseDir, "models", "v5", "ch_PP-LCNet_x0_25_textline_ori_cls_mobile.onnx");
            var recPath = Path.Combine(baseDir, "models", "v5", "ch_PP-OCRv5_rec_mobile.onnx");
            var keysPath = Path.Combine(baseDir, "models", "v5", "ppocrv5_dict.txt");

            if (!File.Exists(recPath) || !File.Exists(keysPath))
            {
                throw new InvalidOperationException("OCR 中文模型缺失，请确认 models/v5 目录包含 ch_PP-OCRv5_rec_mobile.onnx 与 ppocrv5_dict.txt");
            }

            _ocr.InitModels(detPath, clsPath, recPath, keysPath);
            _initialized = true;
            _logger.LogInformation("RapidOcr 模型初始化完成");
        }
    }

    /// <summary>执行 OCR（串行化，避免并发使用同一 ONNX 会话）。</summary>
    private OcrResult RunOcr(SKBitmap bitmap, CancellationToken cancellationToken)
    {
        _ocrLock.Wait(cancellationToken);
        try
        {
            var options = RapidOcrOptions.Default with { ImgResize = (int)(bitmap.Width > bitmap.Height ? bitmap.Width : bitmap.Height), DoAngle = false };
            return _ocr.Detect(bitmap, options);
        }
        finally
        {
            _ocrLock.Release();
        }
    }

    /// <summary>按坐标把 OCR 文本块重建为表格行（以物料编码列为行锚点分行，避免折行/行距导致的聚类误差）。</summary>
    private static void ParseTable(List<OcrWord> words, PdfParseResult result)
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

        // 2. 行锚点：编码列（物料编码表头 x 范围）内的数据块，每行一个编码
        var codeHeader = headerWords.FirstOrDefault(w => w.Text.StartsWith("物料编码", StringComparison.Ordinal))
            ?? headerWords.First(w => w.Text.Contains("编码"));
        var codeColCenter = codeHeader.X + codeHeader.Width / 2;
        var codeColTolerance = Math.Max(codeHeader.Width, 60);

        var dataWords = words.Where(w => w.Y > headerAnchor.Y + headerAnchor.Height * 0.8).ToList();
        var anchors = dataWords
            .Where(w => Math.Abs(w.X + w.Width / 2 - codeColCenter) < codeColTolerance)
            .OrderBy(w => w.Y)
            .ToList();

        if (anchors.Count == 0)
        {
            return;
        }

        // 3. 每个数据块归入 Y 距离最近的锚点行（折行块自然归入本行）
        var rowBuckets = anchors.ToDictionary(a => a, _ => new List<OcrWord>());
        foreach (var word in dataWords)
        {
            var nearest = anchors.OrderBy(a => Math.Abs(a.Y - word.Y)).First();
            rowBuckets[nearest].Add(word);
        }

        // 4. 每行按列归位（x 中心最近的表头列）
        var lineNo = result.Rows.Count;
        foreach (var anchor in anchors)
        {
            var row = rowBuckets[anchor];
            var cells = new Dictionary<string, string>();
            foreach (var word in row.OrderBy(w => w.X))
            {
                var center = word.X + word.Width / 2;
                var nearest = headerWords
                    .Select(h => new { h.Text, Center = h.X + h.Width / 2 })
                    .OrderBy(h => Math.Abs(center - h.Center))
                    .First();

                cells[nearest.Text] = cells.TryGetValue(nearest.Text, out var existing)
                    ? existing + word.Text
                    : word.Text;
            }

            var item = BuildRow(cells, lineNo + 1);
            if (IsValidOrderLine(item))
            {
                result.Rows.Add(item);
                lineNo++;
            }
        }
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
