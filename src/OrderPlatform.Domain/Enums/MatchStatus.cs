namespace OrderPlatform.Domain.Enums;

/// <summary>订单/明细的图号关联状态。</summary>
public enum MatchStatus
{
    /// <summary>已关联：全部明细均匹配到图号。</summary>
    Matched,

    /// <summary>部分关联：部分明细匹配、部分未匹配，需人工确认。</summary>
    Partial,

    /// <summary>未关联：无任何明细匹配到图号。</summary>
    Unmatched
}