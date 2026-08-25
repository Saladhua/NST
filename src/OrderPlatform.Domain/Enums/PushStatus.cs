namespace OrderPlatform.Domain.Enums;

/// <summary>订单推送状态。</summary>
public enum PushStatus
{
    /// <summary>未推送。</summary>
    NotPushed,

    /// <summary>部分推送：部分明细行已推送、部分行尚未推送。</summary>
    PartialPushed,

    /// <summary>已推送：全部明细行均已推送完成。</summary>
    Pushed,

    /// <summary>推送失败。</summary>
    Failed
}