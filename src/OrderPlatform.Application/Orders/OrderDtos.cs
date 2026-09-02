using OrderPlatform.Domain.Enums;

namespace OrderPlatform.Application.Orders;

/// <summary>分页结果通用包装。</summary>
public class PagedResult<T>
{
    /// <summary>当前页数据。</summary>
    public List<T> Items { get; set; } = new();

    /// <summary>总条数。</summary>
    public int Total { get; set; }

    public PagedResult()
    {
    }

    public PagedResult(List<T> items, int total)
    {
        Items = items;
        Total = total;
    }
}

/// <summary>订单列表项。</summary>
public class OrderListDto
{
    /// <summary>订单 ID。</summary>
    public Guid Id { get; set; }

    /// <summary>订单号。</summary>
    public string OrderNo { get; set; } = string.Empty;

    /// <summary>关联客户 ID。</summary>
    public Guid CustomerId { get; set; }

    /// <summary>关联客户名称。</summary>
    public string CustomerName { get; set; } = string.Empty;

    /// <summary>订单日期。</summary>
    public DateTime? OrderDate { get; set; }

    /// <summary>总数量。</summary>
    public decimal TotalQuantity { get; set; }

    /// <summary>总金额。</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>关联状态。</summary>
    public MatchStatus ParseStatus { get; set; }

    /// <summary>推送状态。</summary>
    public PushStatus PushStatus { get; set; }

    /// <summary>ERP 受订单号（推送成功后回填，如 SO69020008）。</summary>
    public string? ErpOsNo { get; set; }

    /// <summary>创建时间。</summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>订单详情（列表项 + 来源批次 + 明细行）。</summary>
public class OrderDetailDto : OrderListDto
{
    /// <summary>来源上传批次。</summary>
    public Guid? SourceFileId { get; set; }

    /// <summary>订单明细行。</summary>
    public List<OrderItemDto> Items { get; set; } = new();
}

/// <summary>订单明细行。</summary>
public class OrderItemDto
{
    /// <summary>明细 ID。</summary>
    public Guid Id { get; set; }

    /// <summary>行号。</summary>
    public int LineNo { get; set; }

    /// <summary>物料编码。</summary>
    public string MaterialCode { get; set; } = string.Empty;

    /// <summary>物料名称。</summary>
    public string MaterialName { get; set; } = string.Empty;

    /// <summary>规格。</summary>
    public string Spec { get; set; } = string.Empty;

    /// <summary>外径（规格拆解）。</summary>
    public decimal? OuterDiameter { get; set; }

    /// <summary>壁厚（规格拆解）。</summary>
    public decimal? WallThickness { get; set; }

    /// <summary>模数/孔数（规格拆解）。</summary>
    public decimal? Module { get; set; }

    /// <summary>收口（规格拆解）。</summary>
    public string ShouKou { get; set; } = string.Empty;

    /// <summary>材质。</summary>
    public string Material { get; set; } = string.Empty;

    /// <summary>匹配到的客户图号。</summary>
    public string CustomerPartNo { get; set; } = string.Empty;

    /// <summary>匹配到的 NEST 图号。</summary>
    public string NestPartNo { get; set; } = string.Empty;

    /// <summary>合金。</summary>
    public string Alloy { get; set; } = string.Empty;

    /// <summary>喷锌。</summary>
    public string Spray { get; set; } = string.Empty;

    /// <summary>长度（mm）。</summary>
    public decimal? Length { get; set; }

    /// <summary>数量。</summary>
    public decimal Quantity { get; set; }

    /// <summary>单位。</summary>
    public string Unit { get; set; } = string.Empty;

    /// <summary>单价。</summary>
    public decimal Price { get; set; }

    /// <summary>金额。</summary>
    public decimal Amount { get; set; }

    /// <summary>交货日期。</summary>
    public DateTime? ReceiveDate { get; set; }

    /// <summary>行推送时间（无交货日期时作为要货日期展示）。</summary>
    public DateTime? PushedAt { get; set; }

    /// <summary>行备注（创达订单号所在列）。</summary>
    public string Remark { get; set; } = string.Empty;

    /// <summary>匹配状态。</summary>
    public MatchStatus MatchStatus { get; set; }

    /// <summary>ERP 货品代号（物料同步后回填）。</summary>
    public string ErpPrdNo { get; set; } = string.Empty;

    /// <summary>物料同步状态。</summary>
    public MaterialSyncStatus MaterialSyncStatus { get; set; }

    /// <summary>行级推送状态。</summary>
    public ItemPushStatus ItemPushStatus { get; set; }

    /// <summary>ERP 受订单号（行推送成功后回填所在受订单号）。</summary>
    public string ErpOsNo { get; set; } = string.Empty;
}

/// <summary>物料同步请求。</summary>
public class SyncMaterialRequest
{
    /// <summary>订单 ID。</summary>
    public Guid OrderId { get; set; }

    /// <summary>明细 ID（为空时同步整单）。</summary>
    public Guid? ItemId { get; set; }
}

/// <summary>物料同步结果。</summary>
public class MaterialSyncResultDto
{
    /// <summary>订单 ID。</summary>
    public Guid OrderId { get; set; }

    /// <summary>同步行数。</summary>
    public int Total { get; set; }

    /// <summary>同步成功（ERP 查到货品）行数。</summary>
    public int Synced { get; set; }

    /// <summary>其中通过 PRD_TB 新建货品后同步成功的行数（含在 Synced 内）。</summary>
    public int Created { get; set; }

    /// <summary>ERP 未找到行数。</summary>
    public int NotFound { get; set; }

    /// <summary>同步失败行数。</summary>
    public int Failed { get; set; }

    /// <summary>错误信息。</summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>订单推送结果。</summary>
public class PushResultDto
{
    /// <summary>推送日志 ID。</summary>
    public Guid LogId { get; set; }

    /// <summary>订单 ID。</summary>
    public Guid OrderId { get; set; }

    /// <summary>推送状态：Success / Partial / Failed。</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>推送时间。</summary>
    public DateTime PushTime { get; set; }

    /// <summary>ERP 受订单号（表头生成成功后返回）。</summary>
    public string? OsNo { get; set; }

    /// <summary>本次待推行数。</summary>
    public int TotalCount { get; set; }

    /// <summary>推送成功行数。</summary>
    public int PushedCount { get; set; }

    /// <summary>推送失败行数（含表头级失败波及的行）。</summary>
    public int FailedCount { get; set; }

    /// <summary>失败原因汇总。</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>ERP 接口调用轨迹（含请求参数与原始响应报文）。</summary>
    public List<ErpApiCallDto> ErpCalls { get; set; } = new();
}

/// <summary>批量推送结果（前端按 OrderId 关联订单号展示）。</summary>
public class BatchPushResultDto
{
    /// <summary>本次处理订单数。</summary>
    public int Total { get; set; }

    /// <summary>推送成功单数（Success + Partial）。</summary>
    public int SuccessCount { get; set; }

    /// <summary>推送失败单数（Failed 或异常）。</summary>
    public int FailedCount { get; set; }

    /// <summary>逐单结果。</summary>
    public List<BatchPushItemResultDto> Results { get; set; } = new();
}

/// <summary>批量推送中单个订单的结果。</summary>
public class BatchPushItemResultDto
{
    /// <summary>订单 ID。</summary>
    public Guid OrderId { get; set; }

    /// <summary>推送状态：Success / Partial / Failed。</summary>
    public string Status { get; set; } = "Failed";

    /// <summary>推送成功行数。</summary>
    public int PushedCount { get; set; }

    /// <summary>推送失败行数。</summary>
    public int FailedCount { get; set; }

    /// <summary>失败原因（异常或单笔推送的失败汇总）。</summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>ERP 单次接口调用的报文记录（返回给前端展示用）。</summary>
public class ErpApiCallDto
{
    /// <summary>接口名称。</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>实际发送的 JSON 请求报文（原文）。</summary>
    public string RequestJson { get; set; } = string.Empty;

    /// <summary>HTTP 状态码。</summary>
    public int? HttpStatus { get; set; }

    /// <summary>ERP 原始响应报文（原文）。</summary>
    public string Response { get; set; } = string.Empty;

    /// <summary>耗时（毫秒）。</summary>
    public long DurationMs { get; set; }

    /// <summary>是否成功。</summary>
    public bool Success { get; set; }

    /// <summary>异常信息（失败时）。</summary>
    public string? Error { get; set; }
}