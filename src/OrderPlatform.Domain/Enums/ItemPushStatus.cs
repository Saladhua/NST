namespace OrderPlatform.Domain.Enums;

/// <summary>订单明细行的推送状态（行级标记：推送过的行不再重复推送）。</summary>
public enum ItemPushStatus
{
    /// <summary>未推送。</summary>
    NotPushed,

    /// <summary>已推送。</summary>
    Pushed,

    /// <summary>推送失败（可重试）。</summary>
    Failed
}
