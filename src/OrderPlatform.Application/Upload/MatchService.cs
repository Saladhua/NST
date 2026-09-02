using System.Text.RegularExpressions;
using OrderPlatform.Application.Parsers;
using OrderPlatform.Domain.Entities;
using OrderPlatform.Domain.Enums;

namespace OrderPlatform.Application.Upload;

/// <summary>单行明细的匹配结果。</summary>
public class MatchResult
{
    /// <summary>匹配到的客户图号。</summary>
    public string CustomerPartNo { get; init; } = string.Empty;

    /// <summary>匹配到的 NEST 套图图号。</summary>
    public string NestPartNo { get; init; } = string.Empty;

    /// <summary>合金。</summary>
    public string Alloy { get; init; } = string.Empty;

    /// <summary>喷锌。</summary>
    public string Spray { get; init; } = string.Empty;

    /// <summary>长度（mm）。</summary>
    public decimal? Length { get; init; }

    /// <summary>匹配状态。</summary>
    public MatchStatus Status { get; init; }

    public static MatchResult Unmatched()
    {
        return new MatchResult { Status = MatchStatus.Unmatched };
    }
}

/// <summary>
/// 订单明细 与 客户资料 的自动关联器。
/// 规则：
/// 1. 主关联键（全部客户通用）：订单存货编码 与 资料客户图号/客户新图号 精确相等。
/// 2. 规格关联键（按客户差异化）：
///    - 三可：规格（外径+壁厚+模数）+ 收口 + 材质（资料有值才比对）；
///    - 华尔达：规格（外径+壁厚）+ 长度 + 材质（资料有值才比对）；
///    - 法拉达：规格反取值（订单规格为 壁厚*外径*长度 反序，解析后反转）+ 长度；
///    - 创达 / 马鞍山仪达：不需要规格匹配（编码可命中）。
/// 3. 其他/未识别客户：外径×壁厚 前缀唯一命中兜底。
/// 4. 未命中/多命中：标记 Unmatched，等待人工确认。
/// </summary>
public static partial class MatchService
{
    /// <summary>「外径×壁厚」规格前缀的正则，如 16*1.4。</summary>
    private static readonly Regex SpecPrefixRegex = new(@"^(\d+(?:\.\d+)?)\s*\*\s*(\d+(?:\.\d+)?)");

    /// <summary>材质牌号提取：3F03+Zn-H112 → 3F03。</summary>
    [GeneratedRegex(@"\d[A-Z]\d{2}|\d{4}")]
    private static partial Regex MaterialTokenPattern();

    /// <summary>对单行明细执行匹配。</summary>
    /// <param name="customerName">客户名称（决定规格匹配策略，可为空走兜底）。</param>
    /// <param name="row">订单明细行。</param>
    /// <param name="parts">该客户的资料列表。</param>
    public static MatchResult Match(string? customerName, PdfParseRow row, List<CustomerPart> parts)
    {
        if (string.IsNullOrWhiteSpace(row.MaterialCode) && string.IsNullOrWhiteSpace(row.Spec))
        {
            return MatchResult.Unmatched();
        }

        // 规则1：精确匹配客户图号 / 客户新图号（全部客户通用）
        var materialCode = (row.MaterialCode ?? string.Empty).Trim();
        if (materialCode.Length > 0)
        {
            var exact = parts.FirstOrDefault(p =>
                !string.IsNullOrWhiteSpace(p.CustomerPartNo) && p.CustomerPartNo.Trim() == materialCode);
            if (exact is not null)
            {
                return FromPart(exact);
            }
        }

        if (parts.Count == 0)
        {
            return MatchResult.Unmatched();
        }

        // 规则2：按客户策略执行规格匹配
        var strategy = ResolveStrategy(customerName);
        return strategy switch
        {
            CustomerStrategy.Sanke => MatchBySpec(row, parts, compareModule: true, compareShouKou: true, compareMaterial: true, compareLength: false),
            CustomerStrategy.Huaruda => MatchBySpec(row, parts, compareModule: false, compareShouKou: false, compareMaterial: true, compareLength: true),
            CustomerStrategy.Falada => MatchBySpec(row, parts, compareModule: false, compareShouKou: false, compareMaterial: false, compareLength: true),
            _ => MatchBySpecPrefix(row, parts)
        };
    }

    /// <summary>客户匹配策略枚举。</summary>
    private enum CustomerStrategy
    {
        Sanke,
        Huaruda,
        Falada,
        Default
    }

    /// <summary>按客户名解析匹配策略（兼容全称/简称）。</summary>
    private static CustomerStrategy ResolveStrategy(string? customerName)
    {
        if (string.IsNullOrWhiteSpace(customerName))
        {
            return CustomerStrategy.Default;
        }

        if (customerName.Contains("三可"))
        {
            return CustomerStrategy.Sanke;
        }

        if (customerName.Contains("华尔达"))
        {
            return CustomerStrategy.Huaruda;
        }

        if (customerName.Contains("发润达") || customerName.Contains("法拉达"))
        {
            return CustomerStrategy.Falada;
        }

        return CustomerStrategy.Default;
    }

    /// <summary>
    /// 规格匹配通用实现：解析订单规格，逐条件过滤资料，唯一命中才算匹配。
    /// 条件字段在资料侧有值才参与比对（「有的就用，没有的就不用」）。
    /// 全条件无命中时材质条件降级重试一次（订单与资料的合金牌号体系可能不一致，如华尔达订单 /1100 与资料 3102），降级后仍要求唯一命中。
    /// </summary>
    private static MatchResult MatchBySpec(
        PdfParseRow row,
        List<CustomerPart> parts,
        bool compareModule,
        bool compareShouKou,
        bool compareMaterial,
        bool compareLength)
    {
        var spec = SpecParser.ParseOrderSpec(row.Spec);
        if (spec.OuterDiameter is null || spec.WallThickness is null)
        {
            return MatchResult.Unmatched();
        }

        var orderMaterial = ExtractMaterial(row.Material);
        if (orderMaterial.Length == 0)
        {
            orderMaterial = spec.Material;
        }

        var candidates = FilterBySpec(parts, spec, orderMaterial, compareModule, compareShouKou, compareMaterial, compareLength);
        if (candidates.Count == 0 && compareMaterial && orderMaterial.Length > 0)
        {
            // 材质降级：忽略材质后重新过滤（唯一命中才采纳）
            candidates = FilterBySpec(parts, spec, orderMaterial, compareModule, compareShouKou, compareMaterial: false, compareLength);
        }

        return candidates.Count == 1 ? FromPart(candidates[0]) : MatchResult.Unmatched();
    }

    /// <summary>按规格条件过滤资料。</summary>
    private static List<CustomerPart> FilterBySpec(
        List<CustomerPart> parts,
        SpecParts spec,
        string orderMaterial,
        bool compareModule,
        bool compareShouKou,
        bool compareMaterial,
        bool compareLength)
    {
        IEnumerable<CustomerPart> query = parts;

        // 条件1：外径 + 壁厚
        query = query.Where(p =>
        {
            var ps = SpecParser.ParseMaterialSpec(p.Spec);
            return ps.OuterDiameter == spec.OuterDiameter && ps.WallThickness == spec.WallThickness;
        });

        // 条件2：模数（三可）
        if (compareModule && spec.Module is not null)
        {
            query = query.Where(p =>
            {
                var ps = SpecParser.ParseMaterialSpec(p.Spec);
                return ps.Module is null || ps.Module == spec.Module;
            });
        }

        // 条件3：收口（三可，资料有值才比对）
        if (compareShouKou && !string.IsNullOrWhiteSpace(spec.ShouKou))
        {
            query = query.Where(p => string.IsNullOrWhiteSpace(p.ShouKou) || SameText(p.ShouKou, spec.ShouKou));
        }

        // 条件4：材质（资料有值才比对）
        if (compareMaterial && orderMaterial.Length > 0)
        {
            query = query.Where(p => string.IsNullOrWhiteSpace(p.Alloy) || SameText(p.Alloy, orderMaterial));
        }

        // 条件5：长度（资料有值才比对）
        if (compareLength && spec.Length is not null)
        {
            query = query.Where(p => p.Length is null || p.Length == spec.Length);
        }

        return query.ToList();
    }

    /// <summary>兜底：外径×壁厚 前缀唯一命中（历史规则，用于未识别客户）。</summary>
    private static MatchResult MatchBySpecPrefix(PdfParseRow row, List<CustomerPart> parts)
    {
        // 优先用规格（华尔达编码乱序但规格干净），规格为空时回退到编码。
        var prefixSource = string.IsNullOrWhiteSpace(row.Spec) ? row.MaterialCode : row.Spec;
        var rowPrefix = SpecPrefixRegex.Match(prefixSource ?? string.Empty);
        if (!rowPrefix.Success)
        {
            return MatchResult.Unmatched();
        }

        var prefix = $"{rowPrefix.Groups[1].Value}*{rowPrefix.Groups[2].Value}";
        var candidates = parts
            .Where(p => SpecPrefixRegex.IsMatch(p.Spec))
            .Where(p =>
            {
                var m = SpecPrefixRegex.Match(p.Spec);
                return $"{m.Groups[1].Value}*{m.Groups[2].Value}" == prefix;
            })
            .ToList();

        return candidates.Count == 1 ? FromPart(candidates[0]) : MatchResult.Unmatched();
    }

    /// <summary>资料命中转匹配结果。</summary>
    private static MatchResult FromPart(CustomerPart part)
    {
        return new MatchResult
        {
            CustomerPartNo = part.CustomerPartNo,
            NestPartNo = part.NestPartNo,
            Alloy = part.Alloy,
            Spray = part.Spray,
            Length = part.Length,
            Status = MatchStatus.Matched
        };
    }

    /// <summary>从订单文本中提取材质牌号（如 3F03+Zn-H112 → 3F03）。</summary>
    public static string ExtractMaterial(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var m = MaterialTokenPattern().Match(text);
        return m.Success ? m.Value : string.Empty;
    }

    /// <summary>文本比对（忽略大小写与首尾空白；数值文本归一化后比对，如 10 与 10.0 视为相等）。</summary>
    private static bool SameText(string a, string b)
    {
        if (decimal.TryParse(a.Trim(), out var da) && decimal.TryParse(b.Trim(), out var db))
        {
            return da == db;
        }

        return string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
