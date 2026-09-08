using System.Globalization;
using System.Text.RegularExpressions;

namespace OrderPlatform.Application.Parsers;

/// <summary>规格串拆解结果（订单规格与客户资料规格通用）。</summary>
public class SpecParts
{
    /// <summary>外径（如 16）。</summary>
    public decimal? OuterDiameter { get; set; }

    /// <summary>壁厚（如 1.4）。</summary>
    public decimal? WallThickness { get; set; }

    /// <summary>模数/孔数（资料规格第三段，或订单规格「壁厚-模数」中的模数）。</summary>
    public decimal? Module { get; set; }

    /// <summary>长度 mm（订单规格第三段）。</summary>
    public decimal? Length { get; set; }

    /// <summary>收口（三可订单规格第四段，如 4 / 10）。</summary>
    public string ShouKou { get; set; } = string.Empty;

    /// <summary>材质（订单规格 / 附加段或资料括号内，如 1100、3F03、1060）。</summary>
    public string Material { get; set; } = string.Empty;

    /// <summary>孔型（方孔 / 圆孔 / 缩口等）。</summary>
    public string HoleType { get; set; } = string.Empty;

    /// <summary>模具号（如 D97）。</summary>
    public string MoldNo { get; set; } = string.Empty;
}

/// <summary>
/// 规格串解析器。约定：
/// 订单规格 = 外径*壁厚*长度（华尔达可带 /模具/材质 后缀）、外径*壁厚-模数(孔型)*长度（创达）、
/// 外径*壁厚-模数*长度*收口（三可）、壁厚*外径*长度（法拉达，反序自动反转）。
/// 资料规格 = 外径*壁厚*模数（第三段为模数，非长度）。
/// </summary>
public static partial class SpecParser
{
    [GeneratedRegex(@"[（(]([^（）()]*)[）)]")]
    private static partial Regex BracketPattern();

    [GeneratedRegex(@"^D\d+$", RegexOptions.IgnoreCase)]
    private static partial Regex MoldPattern();

    [GeneratedRegex(@"^[\dA-Za-z.\-+/（）()，,\*\u4e00-\u9fff]+$")]
    private static partial Regex ValidSpecPattern();

    /// <summary>解析订单明细的规格串（第三段为长度）。</summary>
    public static SpecParts ParseOrderSpec(string? spec)
    {
        return Parse(spec, isMaterialSpec: false);
    }

    /// <summary>解析客户资料的规格串（第三段为模数）。</summary>
    public static SpecParts ParseMaterialSpec(string? spec)
    {
        return Parse(spec, isMaterialSpec: true);
    }

    private static SpecParts Parse(string? spec, bool isMaterialSpec)
    {
        var result = new SpecParts();
        if (string.IsNullOrWhiteSpace(spec))
        {
            return result;
        }

        var clean = FixMissingSlash(spec.Trim().Replace(" ", string.Empty));
        if (!ValidSpecPattern().IsMatch(clean))
        {
            return result;
        }

        // 1. 提取括号内容（孔型 / 材质，如「1060，圆孔」「方孔缩口」）
        foreach (Match b in BracketPattern().Matches(clean))
        {
            var inner = b.Groups[1].Value;
            foreach (var piece in inner.Split('，', ',', '/'))
            {
                var p = piece.Trim();
                if (string.IsNullOrEmpty(p))
                {
                    continue;
                }

                if (IsMaterialText(p))
                {
                    result.Material = p;
                }
                else
                {
                    result.HoleType = result.HoleType.Length == 0 ? p : result.HoleType + p;
                }
            }
        }

        clean = BracketPattern().Replace(clean, string.Empty);

        // 2. 按 * 分段，逐段解析
        var segments = clean.Split('*');
        var numbers = new List<decimal>();
        foreach (var segment in segments)
        {
            if (segment.Length == 0)
            {
                continue;
            }

            // 「/」附加段：603/D97/1100 → 主值 603，附加 D97(模具)、1100(材质)
            var slashParts = segment.Split('/');
            var main = slashParts[0];
            for (var i = 1; i < slashParts.Length; i++)
            {
                var extra = slashParts[i].Trim();
                if (extra.Length == 0)
                {
                    continue;
                }

                if (MoldPattern().IsMatch(extra))
                {
                    result.MoldNo = extra;
                }
                else if (IsMaterialText(extra))
                {
                    result.Material = extra;
                }
            }

            // 「数字+D模具号」粘连段（缺斜杠，如 176D97/1100）：剥离模具号并把数字计入数值段
            var joined = Regex.Match(main, @"^(\d+(?:\.\d+)?)(D\d+)$");
            if (joined.Success)
            {
                if (TryParseNumber(joined.Groups[1].Value, out var joinedNum))
                {
                    numbers.Add(joinedNum);
                }

                result.MoldNo = joined.Groups[2].Value;
                continue;
            }

            // 「-」分段：壁厚-模数（如 2.0-12、2-13）
            if (main.Contains('-'))
            {
                var dashParts = main.Split('-');
                if (TryParseNumber(dashParts[0], out var wall))
                {
                    result.WallThickness = wall;
                }

                if (dashParts.Length > 1 && TryParseNumber(dashParts[1], out var mod))
                {
                    result.Module = mod;
                }

                continue;
            }

            if (TryParseNumber(main, out var num))
            {
                numbers.Add(num);
            }
            else if (IsMaterialText(main) && numbers.Count > 0)
            {
                // 尾段非数值（如材质文本）时记为材质
                result.Material = main;
            }
        }

        // 3. 按结构判定：
        //    - 含「壁厚-模数」段（创达/三可）：numbers 剩余 = [外径, 长度, (收口)]
        //    - 无该段：numbers = [外径, 壁厚, 长度|模数, (收口)]
        if (result.WallThickness is not null && result.Module is not null && numbers.Count >= 2)
        {
            // 创达/三可：外径*壁厚-模数*长度(*收口)
            result.OuterDiameter = numbers[0];
            result.Length = numbers[1];
            if (numbers.Count >= 3)
            {
                result.ShouKou = TrimNum(numbers[2]);
            }
        }
        else if (numbers.Count >= 4)
        {
            // 四段：外径*壁厚*长度*收口
            result.OuterDiameter = numbers[0];
            result.WallThickness = numbers[1];
            result.Length = numbers[2];
            result.ShouKou = TrimNum(numbers[3]);
        }
        else if (numbers.Count == 3)
        {
            var n1 = numbers[0];
            var n2 = numbers[1];
            var n3 = numbers[2];

            if (n1 < n2 && n1 < 5m)
            {
                // 反序：壁厚*外径*长度（法拉达）
                result.WallThickness = n1;
                result.OuterDiameter = n2;
                if (isMaterialSpec)
                {
                    result.Module = n3;
                }
                else
                {
                    result.Length = n3;
                }
            }
            else
            {
                // 标准：外径*壁厚*第三段
                result.OuterDiameter = n1;
                result.WallThickness = n2;
                if (isMaterialSpec)
                {
                    result.Module = n3;
                }
                else
                {
                    result.Length = n3;
                }
            }
        }
        else if (numbers.Count == 2)
        {
            result.OuterDiameter = numbers[0];
            result.WallThickness = numbers[1];
        }

        return result;
    }

    /// <summary>判断文本是否像材质牌号（1100、3102、3F03、1060 等）。</summary>
    public static bool IsMaterialText(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || MoldPattern().IsMatch(text))
        {
            return false;
        }

        return Regex.IsMatch(text, @"^\d{4}$|^\d[A-Z]\d{2}$|^\d{3}[A-Z]$");
    }

    /// <summary>
    /// 修复规格中「数字+D模具号」粘连缺失斜杠的问题（PDF 单元格内换行断字导致），
    /// 如 16*1.6*176D97/1100 → 16*1.6*176/D97/1100；已带斜杠的规格不受影响。
    /// </summary>
    private static string FixMissingSlash(string spec)
    {
        if (string.IsNullOrWhiteSpace(spec))
        {
            return string.Empty;
        }

        // 「176D97/1100」：把数字结束紧接 D 模具号且后跟数码/材质段的位置补上斜杠
        return Regex.Replace(spec, @"(\d+)D(?=\d+/)", "$1/D", RegexOptions.None, TimeSpan.FromSeconds(1));
    }

    /// <summary>
    /// 解析数值：兼容逗号小数（如 607,5 → 607.5）与千分位逗号（如 1,280 → 1280）。
    /// 逗号后不足 3 位（或段数不整）视为小数分隔符，否则视为千分位。
    /// </summary>
    private static bool TryParseNumber(string? text, out decimal value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var s = text.Trim();
        if (s.Contains(','))
        {
            var parts = s.Split(',');
            var isThousands = parts.Length > 1
                && parts.Skip(1).All(p => p.Trim().Length == 3);
            s = isThousands ? s.Replace(",", string.Empty) : s.Replace(',', '.');
        }

        return decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>数值转紧凑文本（去掉无意义的小数位，如 4.0 → 4、2.50 → 2.5）。</summary>
    public static string TrimNum(decimal value)
    {
        return value.ToString("0.####", CultureInfo.InvariantCulture);
    }

    /// <summary>规格比对键：外径*壁厚（数值归一化，如 16*1.4）。</summary>
    public static string SpecKey(decimal? outer, decimal? wall)
    {
        if (outer is null || wall is null)
        {
            return string.Empty;
        }

        return TrimNum(outer.Value) + "*" + TrimNum(wall.Value);
    }

    /// <summary>
    /// 规格展示/推送规范化：
    /// 1. 括号内仅保留孔型等非材质内容（去掉 1060/3102/1100/3F03 等材质牌号），括号变空则整段删除；
    /// 2. 「外径*壁厚-模数*长度」结构的减号转乘号并丢弃长度段（长度单独成列，不并入规格）。
    /// 例：32*2*12（1060，圆孔）→ 32*2*12（圆孔）；18*1.8-14*210 → 18*1.8*14；
    ///    32*1.3-32*862(方孔) → 32*1.3*32(方孔)；25.4*2-13*1686*4 → 25.4*2*13。
    /// </summary>
    public static string NormalizeSpec(string? spec)
    {
        if (string.IsNullOrWhiteSpace(spec))
        {
            return string.Empty;
        }

        var clean = FixMissingSlash(spec.Trim().Replace(" ", string.Empty));

        // 1. 去掉括号内的材质牌号，仅保留孔型等非材质词；括号空则整段删除（保留原括号类型）
        clean = BracketPattern().Replace(clean, match =>
        {
            var open = match.Value[0];   // （ 或 (
            var close = open == '（' ? '）' : ')';
            var pieces = match.Groups[1].Value
                .Split('，', ',', '/')
                .Select(p => p.Trim())
                .Where(p => p.Length > 0 && !IsMaterialText(p))
                .ToList();
            return pieces.Count > 0 ? $"{open}{string.Join("，", pieces)}{close}" : string.Empty;
        });

        // 2. 「外径*壁厚-模数*长度(*收口)(孔型)」→「外径*壁厚*模数(孔型)」：减号转乘号，丢弃长度段，保留尾部括号
        var m = System.Text.RegularExpressions.Regex.Match(clean, @"^(\d+(?:\.\d+)?)\*(\d+(?:\.\d+)?)-(\d+(?:\.\d+)?)(?:\*[\d.]+)*([（(][^（）()]*[）)])?$");
        if (m.Success)
        {
            clean = $"{m.Groups[1].Value}*{m.Groups[2].Value}*{m.Groups[3].Value}{m.Groups[4].Value}";
        }

        // 3. 剥离尾部模具/材质段（/D97/1100 或粘连的 D97/1100），规格列仅保留 外径*壁厚*长度 三段
        //    如 16*1.6*176/D97/1100 → 16*1.6*176；16*1.6*176D97/1100 → 16*1.6*176
        clean = Regex.Replace(clean, @"/D\d+/\d{3,4}$", string.Empty);
        clean = Regex.Replace(clean, @"D\d+/\d{3,4}$", string.Empty);

        return clean;
    }
}
