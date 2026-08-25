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

        var clean = spec.Trim().Replace(" ", string.Empty);
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

            // 「-」分段：壁厚-模数（如 2.0-12、2-13）
            if (main.Contains('-'))
            {
                var dashParts = main.Split('-');
                if (decimal.TryParse(dashParts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out var wall))
                {
                    result.WallThickness = wall;
                }

                if (dashParts.Length > 1
                    && decimal.TryParse(dashParts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var mod))
                {
                    result.Module = mod;
                }

                continue;
            }

            if (decimal.TryParse(main, NumberStyles.Any, CultureInfo.InvariantCulture, out var num))
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
}
