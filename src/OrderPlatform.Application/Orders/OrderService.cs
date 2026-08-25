using AutoMapper;
using Microsoft.Extensions.Logging;
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

    /// <summary>物料同步：按 图号+长度 调 ERP 查询货品代号并回填状态（单行或整单）。</summary>
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
                    var product = await _erpPushService.GetProductAsync(
                        item.CustomerPartNo.Length > 0 ? item.CustomerPartNo : item.NestPartNo,
                        item.Length?.ToString("0.##") ?? string.Empty,
                        cancellationToken);

                    if (string.IsNullOrWhiteSpace(product.PrdNo))
                    {
                        throw new InvalidOperationException($"ERP 接口 get_PRD 未匹配到货品代号（图号：{item.CustomerPartNo}）");
                    }

                    item.ErpPrdNo = product.PrdNo;
                    await _erpPushService.CreateOrderItemAsync(new ErpOrderItemRequest
                    {
                        SoNo = osNo,
                        Itm = item.LineNo,
                        PrdNo = product.PrdNo,
                        Qty = item.Quantity,
                        Qty1 = item.Quantity,
                        Ydd = item.ReceiveDate?.ToString("yyyy-MM-dd") ?? string.Empty
                    }, cancellationToken);

                    item.ItemPushStatus = ItemPushStatus.Pushed;
                    item.PushedAt = DateTime.Now;
                    pushedCount++;
                    pushedItems.Add(new
                    {
                        LineNo = item.LineNo,
                        PrdNo = product.PrdNo,
                        Qty = item.Quantity,
                        Qty1 = item.Quantity,
                        Ydd = item.ReceiveDate?.ToString("yyyy-MM-dd") ?? string.Empty
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
    /// 物料同步：按 图号 + 长度 调 ERP 查询货品代号，回填 ErpPrdNo 与同步状态。
    /// itemId 为空时同步订单内全部已匹配行。创建物料接口第三方暂未提供，查不到标记 NotFound。
    /// </summary>
    public async Task<MaterialSyncResultDto> SyncMaterialAsync(Guid orderId, Guid? itemId, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdAsync(orderId, cancellationToken)
            ?? throw new BusinessException("订单不存在");

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
        var notFound = 0;
        var failed = 0;
        var errors = new List<string>();

        foreach (var item in targets)
        {
            try
            {
                var product = await _erpPushService.GetProductAsync(
                    item.CustomerPartNo.Length > 0 ? item.CustomerPartNo : item.NestPartNo,
                    item.Length?.ToString("0.##") ?? string.Empty,
                    cancellationToken);

                if (string.IsNullOrWhiteSpace(product.PrdNo))
                {
                    item.MaterialSyncStatus = MaterialSyncStatus.NotFound;
                    item.ErpPrdNo = string.Empty;
                    notFound++;
                }
                else
                {
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
}