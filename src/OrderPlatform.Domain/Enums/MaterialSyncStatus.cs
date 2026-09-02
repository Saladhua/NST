namespace OrderPlatform.Domain.Enums;

/// <summary>订单明细的物料同步状态（同步到第三方 ERP 的货品查询结果）。</summary>
public enum MaterialSyncStatus
{
    /// <summary>未同步。</summary>
    NotSynced,

    /// <summary>已同步：ERP 中查询到货品代号。</summary>
    Synced,

    /// <summary>未找到：ERP 中不存在该货品（同步时会调 PRD_TB 自动创建，正常不再出现该状态）。</summary>
    NotFound,

    /// <summary>同步失败：接口调用异常。</summary>
    Failed
}
