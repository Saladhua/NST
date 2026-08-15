namespace OrderPlatform.Domain.Enums;

/// <summary>订单推送状态。</summary>
public enum PushStatus
{
    /// <summary>未推送。</summary>
    NotPushed,

    /// <summary>已推送。</summary>
    Pushed,

    /// <summary>推送失败。</summary>
    Failed
}