using System.Text.RegularExpressions;
using OrderPlatform.Application.Parsers;
using OrderPlatform.Domain.Entities;
using OrderPlatform.Domain.Enums;

namespace OrderPlatform.Application.Upload;

/// <summary>
/// PDF 采购订单明细 与 Excel 客户资料 的自动关联器。
/// 规则：
/// 1. 主关联键：PDF 存货编码 与 客户图号/客户新图号 精确相等。
/// 2. 次关联键：PDF 规格型号的 外径×壁厚 前缀（如 16*1.4）匹配 Excel 规格列前缀，唯一命中即关联。
/// 3. 未命中/多命中：标记 Unmatched 或 Partial，等待人工确认。
/// </summary>
public static class MatchService
{
    private static readonly Regex SpecPrefixRegex = new(@"^(\d+(?:\.\d+)?)\s*\*\s*(\d+(?:\.\d+)?)");

    public static (string customerPartNo, string nestPartNo, string alloy, string spray, decimal? length, MatchStatus status)
        Match(PdfParseRow row, List<CustomerPart> parts)
    {
        if (string.IsNullOrWhiteSpace(row.MaterialCode))
        {
            return (string.Empty, string.Empty, string.Empty, string.Empty, null, MatchStatus.Unmatched);
        }

        // 规则1：精确匹配客户图号 / 客户新图号
        var materialCode = row.MaterialCode.Trim();
        var exact = parts.FirstOrDefault(p =>
            !string.IsNullOrWhiteSpace(p.CustomerPartNo) && p.CustomerPartNo.Trim() == materialCode);
        if (exact is not null)
        {
            return (exact.CustomerPartNo, exact.NestPartNo, exact.Alloy, exact.Spray, exact.Length, MatchStatus.Matched);
        }

        // 规则2：外径×壁厚 前缀匹配（唯一命中）
        // 优先用规格（华尔达编码乱序但规格干净），规格为空时回退到编码。
        var prefixSource = string.IsNullOrWhiteSpace(row.Spec) ? row.MaterialCode : row.Spec;
        var rowPrefix = SpecPrefixRegex.Match(prefixSource);
        if (rowPrefix.Success)
        {
            var prefix = $"{rowPrefix.Groups[1].Value}*{rowPrefix.Groups[2].Value}";
            var candidates = parts
                .Where(p => SpecPrefixRegex.IsMatch(p.Spec))
                .Where(p =>
                {
                    var m = SpecPrefixRegex.Match(p.Spec);
                    return $"{m.Groups[1].Value}*{m.Groups[2].Value}" == prefix;
                })
                .ToList();

            if (candidates.Count == 1)
            {
                var c = candidates[0];
                return (c.CustomerPartNo, c.NestPartNo, c.Alloy, c.Spray, c.Length, MatchStatus.Matched);
            }
        }

        return (string.Empty, string.Empty, string.Empty, string.Empty, null, MatchStatus.Unmatched);
    }
}
