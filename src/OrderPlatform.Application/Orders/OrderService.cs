using AutoMapper;
using Microsoft.Extensions.Logging;
using OrderPlatform.Application.Parsers;
using OrderPlatform.Application.Upload.Dtos;
using OrderPlatform.Domain.Entities;
using OrderPlatform.Domain.Enums;
using OrderPlatform.Domain.Interfaces;
using OrderPlatform.Shared.Api;
using System.Text.Json;

namespace OrderPlatform.Application.Orders;

/// <summary>订单服务接口：订单列表、详情、推送（行级）、物料同步、删除。</summary>
public interface IOrderService
{
    /// <summary>分页查询订单列表（支持关键词/客户/推送状态/关联状态筛选）。</summary>
    Task<PagedResult<OrderListDto>> ListAsync(int page, int pageSize, string? keyword, Guid? customerId, PushStatus? pushStatus, MatchStatus? parseStatus, CancellationToken cancellationToken);

    /// <summary>查询订单详情（含明细，按行号排序）。</summary>
    Task<OrderDetailDto> GetDetailAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>推送订单（行级：只推未推送且已匹配的行，已推送行自动跳过）。无论成败均返回详细结果与 ERP 报文。</summary>
    Task<PushResultDto> PushAsync(Guid orderId, CancellationToken cancellationToken);

    /// <summary>批量推送订单：逐单复用推送逻辑，单笔异常记为失败且不中断其余订单。</summary>
    Task<BatchPushResultDto> BatchPushAsync(IEnumerable<Guid> orderIds, CancellationToken cancellationToken);

    /// <summary>物料同步：按 图号+长度 调 ERP 查询货品代号并回填状态（单行或整单）；查不到时调 PRD_TB 自动创建后复查。</summary>
    Task<MaterialSyncResultDto> SyncMaterialAsync(Guid orderId, Guid? itemId, CancellationToken cancellationToken);

    /// <summary>删除订单（已推送的订单不可删除）。</summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}

/// <summary>订单服务实现。</summary>
public class OrderService : IOrderService
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new() { WriteIndented = false };

    private readonly IOrderRepository _orderRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IOrderPushLogRepository _pushLogRepository;
    private readonly IErpPushService _erpPushService;
    private readonly IMapper _mapper;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        IOrderRepository orderRepository,
        ICustomerRepository customerRepository,
        IOrderPushLogRepository pushLogRepository,
        IErpPushService erpPushService,
        IMapper mapper,
        ILogger<OrderService> logger)
    {
        _orderRepository = orderRepository;
        _customerRepository = customerRepository;
        _pushLogRepository = pushLogRepository;
        _erpPushService = erpPushService;
        _mapper = mapper;
        _logger = logger;
    }

    /// <summary>分页查询订单并补充客户名称。</summary>
    public async Task<PagedResult<OrderListDto>> ListAsync(
        int page,
        int pageSize,
        string? keyword,
        Guid? customerId,
        PushStatus? pushStatus,
        MatchStatus? parseStatus,
        CancellationToken cancellationToken)
    {
        var items = await _orderRepository.QueryAsync(page, pageSize, keyword, customerId, pushStatus, parseStatus, cancellationToken);
        var total = await _orderRepository.CountAsync(keyword, customerId, pushStatus, parseStatus, cancellationToken);

        var customers = await _customerRepository.ListAsync(cancellationToken);
        var customerMap = customers.ToDictionary(c => c.Id, c => c.Name);

        var list = items.Select(o => new OrderListDto
        {
            Id = o.Id,
            OrderNo = o.OrderNo,
            CustomerId = o.CustomerId,
            CustomerName = customerMap.TryGetValue(o.CustomerId, out var name) ? name : string.Empty,
            OrderDate = o.OrderDate,
            TotalQuantity = o.TotalQuantity,
            TotalAmount = o.TotalAmount,
            ParseStatus = o.ParseStatus,
            PushStatus = o.PushStatus,
            ErpOsNo = o.ErpOsNo,
            CreatedAt = o.CreatedAt
        }).ToList();

        return new PagedResult<OrderListDto>(list, total);
    }

    /// <summary>查询订单详情，包含明细行与客户名称。</summary>
    public async Task<OrderDetailDto> GetDetailAsync(Guid id, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new BusinessException("订单不存在");

        var customer = await _customerRepository.GetByIdAsync(order.CustomerId, cancellationToken);

        var dto = new OrderDetailDto
        {
            Id = order.Id,
            OrderNo = order.OrderNo,
            CustomerId = order.CustomerId,
            CustomerName = customer?.Name ?? string.Empty,
            OrderDate = order.OrderDate,
            TotalQuantity = order.TotalQuantity,
            TotalAmount = order.TotalAmount,
            ParseStatus = order.ParseStatus,
            PushStatus = order.PushStatus,
            ErpOsNo = order.ErpOsNo,
            SourceFileId = order.SourceFileId,
            CreatedAt = order.CreatedAt,
            Items = order.Items.OrderBy(i => i.LineNo).Select(i => new OrderItemDto
            {
                Id = i.Id,
                LineNo = i.LineNo,
                MaterialCode = i.MaterialCode,
                MaterialName = i.MaterialName,
                Spec = i.Spec,
                OuterDiameter = i.OuterDiameter,
                WallThickness = i.WallThickness,
                Module = i.Module,
                ShouKou = i.ShouKou,
                Material = i.Material,
                CustomerPartNo = i.CustomerPartNo,
                NestPartNo = i.NestPartNo,
                Alloy = i.Alloy,
                Spray = i.Spray,
                Length = i.Length,
                Quantity = i.Quantity,
                Unit = i.Unit,
                Price = i.Price,
                Amount = i.Amount,
                ReceiveDate = i.ReceiveDate,
                PushedAt = i.PushedAt,
                Remark = i.Remark,
                MatchStatus = i.MatchStatus,
                ErpPrdNo = i.ErpPrdNo,
                MaterialSyncStatus = i.MaterialSyncStatus,
                ItemPushStatus = i.ItemPushStatus
            }).ToList()
        };

        return dto;
    }

    /// <summary>
    /// 推送订单（行级）：只推送「未推送且已匹配图号」的明细行，已推送行自动跳过，
    /// 未匹配行跳过（人工匹配后可再推）。全部行推送完成后订单标记已推送，不再允许推送。
    /// </summary>
    public async Task<PushResultDto> PushAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdAsync(orderId, cancellationToken)
            ?? throw new BusinessException("订单不存在");

        if (order.PushStatus == PushStatus.Pushed)
        {
            throw new BusinessException("订单已全部推送，请勿重复推送");
        }

        // 待推送行：未推送（或推送失败可重试）且已匹配图号
        var pendingItems = order.Items
            .Where(i => i.ItemPushStatus != ItemPushStatus.Pushed && i.MatchStatus == MatchStatus.Matched)
            .OrderBy(i => i.LineNo)
            .ToList();

        if (pendingItems.Count == 0)
        {
            throw new BusinessException("没有可推送的明细行（未关联图号的行需先完成匹配）");
        }

        var customer = await _customerRepository.GetByIdAsync(order.CustomerId, cancellationToken);
        if (customer is null)
        {
            throw new BusinessException("订单客户不存在，无法推送");
        }

        var pushedItems = new List<object>();
        var cusNo = string.Empty;
        var osNo = string.Empty;
        var errors = new List<string>();
        var pushedCount = 0;

        try
        {
            // 1. 根据客户名称获取客户 ID
            var erpCustomer = await _erpPushService.GetCustomerAsync(customer.Name, cancellationToken);
            cusNo = erpCustomer.CusNo;
            if (string.IsNullOrWhiteSpace(cusNo))
            {
                throw new BusinessException($"ERP 接口 get_cus 未找到客户：{customer.Name}");
            }

            // 2. 生成受订单表头，取得受订单号
            var header = await _erpPushService.CreateOrderHeaderAsync(cusNo, cancellationToken);
            osNo = header.OsNo;
            if (string.IsNullOrWhiteSpace(osNo))
            {
                throw new BusinessException("ERP 接口 MF_POS 未返回受订单号，生成表头失败");
            }

            // 3. 逐行获取货品代号并生成受订单表身（单行失败不中断其余行）
            foreach (var item in pendingItems)
            {
                try
                {
                    // 查询图号统一用 NEST 图号（与 ERP 侧图号体系一致），无 NEST 图号时回退客户图号
                    var product = await _erpPushService.GetProductAsync(
                        item.NestPartNo.Length > 0 ? item.NestPartNo : item.CustomerPartNo,
                        item.Length?.ToString("0.##") ?? string.Empty,
                        cancellationToken);

                    if (string.IsNullOrWhiteSpace(product.PrdNo))
                    {
                        throw new InvalidOperationException($"ERP 接口 get_PRD 未匹配到货品代号（图号：{item.CustomerPartNo}），请先执行物料同步（ERP 中不存在的货品将自动创建）");
                    }

                    item.ErpPrdNo = product.PrdNo;
                    // 创达数量以行备注「XXX根」为准
                    var pushQty = IsChuangda(customer.Name)
                        ? ExtractQuantityFromRemark(item.Remark) ?? item.Quantity
                        : item.Quantity;
                    // 推送规格：统一规范化（去材质留孔型、减号结构转乘号丢长度）；三可客户图号拼接规则不变
                    var pushSpec = SpecParser.NormalizeSpec(item.Spec);
                    // 客户图号：三可为 物料编码-收口，其余用匹配到的客户图号；客户订单号即本平台订单号
                    var khth = IsSanke(customer.Name)
                        ? BuildSankeKhth(item.MaterialCode, item.ShouKou)
                        : item.CustomerPartNo;
                    await _erpPushService.CreateOrderItemAsync(new ErpOrderItemRequest
                    {
                        SoNo = osNo,
                        Itm = item.LineNo,
                        PrdNo = product.PrdNo,
                        Qty = pushQty,
                        Ydd = FormatYdd(item.ReceiveDate, DateTime.Now),
                        Khth = khth,
                        CusOs = order.OrderNo
                    }, cancellationToken);

                    item.ItemPushStatus = ItemPushStatus.Pushed;
                    item.PushedAt = DateTime.Now;
                    pushedCount++;
                    pushedItems.Add(new
                    {
                        LineNo = item.LineNo,
                        PrdNo = product.PrdNo,
                        Qty = pushQty,
                        Ydd = FormatYdd(item.ReceiveDate, DateTime.Now),
                        Spec = pushSpec,
                        Khth = khth,
                        CusOs = order.OrderNo
                    });
                }
                catch (Exception ex)
                {
                    item.ItemPushStatus = ItemPushStatus.Failed;
                    errors.Add($"第 {item.LineNo} 行：{ex.Message}");
                    _logger.LogError(ex, "订单 {OrderNo} 第 {LineNo} 行推送失败", order.OrderNo, item.LineNo);
                }
            }
        }
        catch (Exception ex)
        {
            // 表头级失败：全部待推行标记失败
            foreach (var item in pendingItems.Where(i => i.ItemPushStatus != ItemPushStatus.Pushed))
            {
                item.ItemPushStatus = ItemPushStatus.Failed;
            }

            errors.Insert(0, ex.Message);
        }

        // 4. 汇总订单级推送状态：全部行推送完成 → Pushed；部分完成 → PartialPushed；否则 Failed
        var allPushed = order.Items.Count > 0 && order.Items.All(i => i.ItemPushStatus == ItemPushStatus.Pushed);
        var anyPushed = order.Items.Any(i => i.ItemPushStatus == ItemPushStatus.Pushed);
        order.PushStatus = allPushed ? PushStatus.Pushed : anyPushed ? PushStatus.PartialPushed : PushStatus.Failed;
        // 回填 ERP 受订单号（表头生成成功即有值；未取得时保留原值）
        if (!string.IsNullOrWhiteSpace(osNo))
        {
            order.ErpOsNo = osNo;
        }

        _orderRepository.Update(order);
        await _orderRepository.SaveChangesAsync(cancellationToken);

        var errorMessage = errors.Count > 0 ? string.Join("；", errors) : null;
        var status = errorMessage is null ? "Success" : anyPushed ? "Partial" : "Failed";

        // 5. 记录推送日志：请求/响应均为本次 ERP 真实调用轨迹与原始报文
        var requestJson = JsonSerializer.Serialize(new
        {
            Customer = customer.Name,
            CusNo = cusNo,
            OsNo = osNo,
            Items = pushedItems
        }, JsonSerializerOptions);

        var log = new OrderPushLog
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            Target = "ERP",
            RequestJson = requestJson,
            Status = status,
            PushTime = DateTime.Now,
            ResponseJson = JsonSerializer.Serialize(_erpPushService.Calls, JsonSerializerOptions),
            ErrorMessage = errorMessage
        };

        await _pushLogRepository.AddAsync(log, cancellationToken);
        await _pushLogRepository.SaveChangesAsync(cancellationToken);

        return new PushResultDto
        {
            LogId = log.Id,
            OrderId = order.Id,
            Status = status,
            PushTime = log.PushTime,
            OsNo = string.IsNullOrWhiteSpace(osNo) ? null : osNo,
            TotalCount = pendingItems.Count,
            PushedCount = pushedCount,
            FailedCount = pendingItems.Count - pushedCount,
            ErrorMessage = errorMessage,
            ErpCalls = _erpPushService.Calls.Select(c => new ErpApiCallDto
            {
                Action = c.Action,
                RequestJson = c.RequestJson,
                HttpStatus = c.HttpStatus,
                Response = c.Response,
                DurationMs = c.DurationMs,
                Success = c.Success,
                Error = c.Error
            }).ToList()
        };
    }

    /// <summary>
    /// 批量推送订单：逐单复用 <see cref="PushAsync"/>，单笔异常（已推送/无待推行/订单不存在等）
    /// 记为失败并继续推送其余订单，最终汇总成功/失败单数。
    /// </summary>
    public async Task<BatchPushResultDto> BatchPushAsync(IEnumerable<Guid> orderIds, CancellationToken cancellationToken)
    {
        var ids = orderIds.Distinct().ToList();
        var results = new List<BatchPushItemResultDto>();

        foreach (var id in ids)
        {
            try
            {
                var r = await PushAsync(id, cancellationToken);
                results.Add(new BatchPushItemResultDto
                {
                    OrderId = id,
                    Status = r.Status,
                    PushedCount = r.PushedCount,
                    FailedCount = r.FailedCount,
                    ErrorMessage = r.ErrorMessage
                });
            }
            catch (Exception ex)
            {
                results.Add(new BatchPushItemResultDto
                {
                    OrderId = id,
                    Status = "Failed",
                    ErrorMessage = ex.Message
                });
            }
        }

        return new BatchPushResultDto
        {
            Total = results.Count,
            SuccessCount = results.Count(r => r.Status is "Success" or "Partial"),
            FailedCount = results.Count(r => r.Status == "Failed"),
            Results = results
        };
    }

    /// <summary>
    /// 物料同步：按 图号 + 长度 调 ERP 查询货品代号，回填 ErpPrdNo 与同步状态。
    /// itemId 为空时同步订单内全部已匹配行。ERP 查不到时调 PRD_TB 新建货品后复查，复查到即回填。
    /// </summary>
    public async Task<MaterialSyncResultDto> SyncMaterialAsync(Guid orderId, Guid? itemId, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdAsync(orderId, cancellationToken)
            ?? throw new BusinessException("订单不存在");

        var customer = await _customerRepository.GetByIdAsync(order.CustomerId, cancellationToken);
        var customerName = customer?.Name ?? string.Empty;

        var targets = order.Items
            .Where(i => itemId is null || i.Id == itemId.Value)
            .Where(i => i.MatchStatus == MatchStatus.Matched)
            .OrderBy(i => i.LineNo)
            .ToList();

        if (targets.Count == 0)
        {
            throw new BusinessException("没有可同步的明细行（需先完成图号匹配）");
        }

        var synced = 0;
        var createdCount = 0;
        var notFound = 0;
        var failed = 0;
        var errors = new List<string>();

        foreach (var item in targets)
        {
            try
            {
                // 查询图号统一用 NEST 图号（与 ERP 侧图号体系一致），无 NEST 图号时回退客户图号
                var th = item.NestPartNo.Length > 0 ? item.NestPartNo : item.CustomerPartNo;
                var pic = item.Length?.ToString("0.##") ?? string.Empty;

                var product = await _erpPushService.GetProductAsync(th, pic, cancellationToken);

                if (string.IsNullOrWhiteSpace(product.PrdNo))
                {
                    // ERP 中不存在该货品：调 PRD_TB 新建（米重系统内无数据，不传），再复查回填
                    await _erpPushService.CreateProductAsync(new ErpProductCreateRequest
                    {
                        Th = th,
                        Pic = pic,
                        Spc = SpecParser.NormalizeSpec(item.Spec),
                        Caiz = item.Material
                    }, cancellationToken);

                    var requery = await _erpPushService.GetProductAsync(th, pic, cancellationToken);
                    if (string.IsNullOrWhiteSpace(requery.PrdNo))
                    {
                        item.MaterialSyncStatus = MaterialSyncStatus.Failed;
                        item.ErpPrdNo = string.Empty;
                        failed++;
                        errors.Add($"第 {item.LineNo} 行：PRD_TB 已创建但 get_PRD 仍未查到货品代号（图号：{th}）");
                        _logger.LogWarning("订单 {OrderNo} 第 {LineNo} 行新建货品后复查仍无货品代号", order.OrderNo, item.LineNo);
                    }
                    else
                    {
                        // 命中后回填：规格取接口返回的 SPC（规范化：去材质留孔型、减号结构转乘号，不为空时覆盖解析值）
                        if (!string.IsNullOrWhiteSpace(requery.Spc))
                        {
                            item.Spec = SpecParser.NormalizeSpec(requery.Spc);
                        }

                        item.MaterialSyncStatus = MaterialSyncStatus.Synced;
                        item.ErpPrdNo = requery.PrdNo;
                        item.SyncedAt = DateTime.Now;
                        synced++;
                        createdCount++;
                    }
                }
                else
                {
                    // 命中后回填：规格取接口返回的 SPC（规范化：去材质留孔型、减号结构转乘号，不为空时覆盖解析值）
                    if (!string.IsNullOrWhiteSpace(product.Spc))
                    {
                        item.Spec = SpecParser.NormalizeSpec(product.Spc);
                    }

                    item.MaterialSyncStatus = MaterialSyncStatus.Synced;
                    item.ErpPrdNo = product.PrdNo;
                    item.SyncedAt = DateTime.Now;
                    synced++;
                }
            }
            catch (Exception ex)
            {
                item.MaterialSyncStatus = MaterialSyncStatus.Failed;
                failed++;
                errors.Add($"第 {item.LineNo} 行：{ex.Message}");
                _logger.LogError(ex, "订单 {OrderNo} 第 {LineNo} 行物料同步失败", order.OrderNo, item.LineNo);
            }
        }

        _orderRepository.Update(order);
        await _orderRepository.SaveChangesAsync(cancellationToken);

        return new MaterialSyncResultDto
        {
            OrderId = order.Id,
            Total = targets.Count,
            Synced = synced,
            Created = createdCount,
            NotFound = notFound,
            Failed = failed,
            ErrorMessage = errors.Count > 0 ? string.Join("；", errors) : null
        };
    }

    /// <summary>删除订单：已推送（含部分推送）的订单禁止删除。</summary>
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new BusinessException("订单不存在");

        if (order.PushStatus is PushStatus.Pushed or PushStatus.PartialPushed)
        {
            throw new BusinessException("已推送（含部分推送）的订单不可删除");
        }

        await _orderRepository.DeleteAsync(id, cancellationToken);
    }

    /// <summary>是否三可客户。</summary>
    private static bool IsSanke(string? customerName)
    {
        return !string.IsNullOrWhiteSpace(customerName) && customerName.Contains("三可");
    }

    /// <summary>是否创达客户。</summary>
    private static bool IsChuangda(string? customerName)
    {
        return !string.IsNullOrWhiteSpace(customerName) && customerName.Contains("创达");
    }

    /// <summary>三可客户图号：物料编码/收口（收口为空或 0 时仅物料编码；收口固定带 .0 与「收口」后缀，如 JSJ0406437/4.0收口）。</summary>
    private static string BuildSankeKhth(string materialCode, string shouKou)
    {
        var sk = NormalizeShouKou(shouKou);
        if (sk.Length == 0 || sk == "0" || sk == "不收口")
        {
            return materialCode;
        }

        return $"{materialCode}/{sk}收口";
    }

    /// <summary>收口数值规范化：4 → 4.0、4.50 → 4.5；非数值文本原样返回（不含「收口」后缀，后缀在拼接处统一加）。</summary>
    private static string NormalizeShouKou(string? shouKou)
    {
        var sk = (shouKou ?? string.Empty).Trim();
        if (sk.Length == 0)
        {
            return string.Empty;
        }

        if (decimal.TryParse(sk, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var value))
        {
            // 保留一位小数，如 4 → 4.0、4.50 → 4.5
            return value.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
        }

        return sk;
    }

    /// <summary>从行备注提取「XXX根」数量（创达：如「202608032B，6410根」→ 6410）；无则返回 null。</summary>
    private static decimal? ExtractQuantityFromRemark(string? remark)
    {
        if (string.IsNullOrWhiteSpace(remark))
        {
            return null;
        }

        var m = System.Text.RegularExpressions.Regex.Match(remark, @"(\d+(?:\.\d+)?)\s*根");
        return m.Success && decimal.TryParse(m.Groups[1].Value, out var qty) ? qty : null;
    }

    /// <summary>推送 Ydd 预交日：有交货日期用交货日期；否则推送日期 +7 天。</summary>
    private static string FormatYdd(DateTime? receiveDate, DateTime pushTime)
    {
        return (receiveDate ?? pushTime.AddDays(7)).ToString("yyyy-MM-dd");
    }
}