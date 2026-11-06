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

    /// <summary>推送订单（行级：只推未推送且已匹配的行，itemIds 非空时仅推勾选的行）。已推送行自动跳过。无论成败均返回详细结果与 ERP 报文。</summary>
    Task<PushResultDto> PushAsync(Guid orderId, ICollection<Guid>? itemIds, CancellationToken cancellationToken);

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
    private readonly ICustomerPartRepository _customerPartRepository;
    private readonly IOrderPushLogRepository _pushLogRepository;
    private readonly IErpPushService _erpPushService;
    private readonly IMapper _mapper;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        IOrderRepository orderRepository,
        ICustomerRepository customerRepository,
        ICustomerPartRepository customerPartRepository,
        IOrderPushLogRepository pushLogRepository,
        IErpPushService erpPushService,
        IMapper mapper,
        ILogger<OrderService> logger)
    {
        _orderRepository = orderRepository;
        _customerRepository = customerRepository;
        _customerPartRepository = customerPartRepository;
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

        // 受单号优先取明细行已回填值（整单只生成一个受单号，历史分批数据可能有多个，去重后逗号拼接），无明细值时回退订单表头号
        var orderIds = items.Select(o => o.Id).ToList();
        var itemOsNoMap = await _orderRepository.GetItemErpOsNosByOrderIdsAsync(orderIds, cancellationToken);

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
            ErpOsNo = itemOsNoMap.TryGetValue(o.Id, out var itemOsNo) && itemOsNo.Length > 0 ? itemOsNo : o.ErpOsNo,
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
                // 摘要：三可按下推规则现算（明细不落库），华尔达取客户资料「摘要」列
                Summary = BuildOrderItemSummary(customer?.Name, i),
                MatchStatus = i.MatchStatus,
                ErpPrdNo = i.ErpPrdNo,
                MaterialSyncStatus = i.MaterialSyncStatus,
                ItemPushStatus = i.ItemPushStatus,
                ErpOsNo = i.ErpOsNo
            }).ToList()
        };

        return dto;
    }

    /// <summary>
    /// 推送订单（行级）：只推送「未推送且已匹配图号」的明细行，勾选推送时（itemIds 非空）仅推勾选行，
    /// 未勾选行跳过。已推送行自动跳过，全部行推送完成后订单标记已推送，不再允许推送。
    /// <para>
    /// 受单号口径：<b>同一上传文件（同一 <see cref="OrderMain.SourceFileId"/> 批次）共用一个受单号</b>。
    /// 一份文件被拆成多个订单时（如创达按子订单号拆单），首个推送的订单调 MF_POS 建表头，
    /// 其余订单复用该号，<c>ITM</c> 在该受单号内全局续号，保证 ERP 里只有一张受订单。
    /// </para>
    /// </summary>
    public async Task<PushResultDto> PushAsync(Guid orderId, ICollection<Guid>? itemIds, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdAsync(orderId, cancellationToken)
            ?? throw new BusinessException("订单不存在");

        if (order.PushStatus == PushStatus.Pushed)
        {
            throw new BusinessException("订单已全部推送，请勿重复推送");
        }

        // 待推送行：未推送（或推送失败可重试）且已匹配图号；勾选推送时仅取勾选的明细行
        var pendingItems = order.Items
            .Where(i => i.ItemPushStatus != ItemPushStatus.Pushed && i.MatchStatus == MatchStatus.Matched)
            .Where(i => itemIds is null || itemIds.Contains(i.Id))
            .OrderBy(i => i.LineNo)
            .ToList();

        if (pendingItems.Count == 0)
        {
            throw new BusinessException(itemIds is null
                ? "没有可推送的明细行（未关联图号的行需先完成匹配）"
                : "勾选的明细行均已推送或不可推送（需先关联图号）");
        }

        var customer = await _customerRepository.GetByIdAsync(order.CustomerId, cancellationToken);
        if (customer is null)
        {
            throw new BusinessException("订单客户不存在，无法推送");
        }

        var pushedItems = new List<object>();
        var cusNo = string.Empty;
        // 整单只用一个受订单号：订单已有受订单号时直接复用（补推不再新建表头）
        var osNo = order.ErpOsNo ?? string.Empty;
        var errors = new List<string>();
        var pushedCount = 0;

        // 同一上传文件共用一个受单号：本订单还没号时，先复用同批次其他订单已取得的号。
        // 一份文件（如创达按子订单号拆单）在 ERP 里只应出现一张受订单，不再一单建一个表头。
        var batchOrders = new List<OrderMain>();
        if (order.SourceFileId.HasValue)
        {
            batchOrders = await _orderRepository.ListBySourceFileIdAsync(order.SourceFileId.Value, cancellationToken);
            if (string.IsNullOrWhiteSpace(osNo))
            {
                osNo = batchOrders
                    .Where(o => o.Id != order.Id)
                    .Select(o => o.ErpOsNo)
                    .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;
            }
        }

        try
        {
            // 1. 根据客户名称获取客户 ID
            var erpCustomer = await _erpPushService.GetCustomerAsync(customer.Name, cancellationToken);
            cusNo = erpCustomer.CusNo;
            if (string.IsNullOrWhiteSpace(cusNo))
            {
                throw new BusinessException($"ERP 接口 get_cus 未找到客户：{customer.Name}");
            }

            // 2. 首次推送生成受订单表头取得受订单号；已有受订单号则复用，保证整单只有一个单号
            if (string.IsNullOrWhiteSpace(osNo))
            {
                var header = await _erpPushService.CreateOrderHeaderAsync(cusNo, cancellationToken);
                osNo = header.OsNo;
                if (string.IsNullOrWhiteSpace(osNo))
                {
                    throw new BusinessException("ERP 接口 MF_POS 未返回受订单号，生成表头失败");
                }
            }

            // 3. 逐行获取货品代号并生成受订单表身（单行失败不中断其余行）
            //    ITM 项次在同一受订单号内必须唯一：起点 = 该受单号下已成功推送的行数
            //    （同批次其他订单可能已往这张单据推过行，需接着续号；历史多号数据只统计本号下的行）
            var itm = batchOrders
                    .Where(o => o.Id != order.Id)
                    .SelectMany(o => o.Items)
                    .Count(i => i.ItemPushStatus == ItemPushStatus.Pushed && IsSameOsNo(i.ErpOsNo, osNo))
                + order.Items.Count(i => i.ItemPushStatus == ItemPushStatus.Pushed && IsSameOsNo(i.ErpOsNo, osNo));
            foreach (var item in pendingItems)
            {
                itm++;
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

                    // 华尔达 / 发润达：货品规格带长度说明档案是错的（如 16*2*530、1.4*12*547），
                    // 推送会在受订单里把长度当成货品规格带出来，直接拦下该行，要求先修正货品资料。
                    if (OrderSpecCarriesLength(customer.Name) && SpecParser.LooksLikeLength(product.Spc))
                    {
                        throw new InvalidOperationException($"ERP 货品 {product.PrdNo} 的规格「{product.Spc}」带长度，拒绝推送；请先在 ERP 货品资料改成「外径*壁厚*模数」");
                    }

                    item.ErpPrdNo = product.PrdNo;
                    // 创达数量以行备注「XXX根」为准
                    var pushQty = IsChuangda(customer.Name)
                        ? ExtractQuantityFromRemark(item.Remark) ?? item.Quantity
                        : item.Quantity;
                    // 推送规格：统一规范化（去材质留孔型、减号结构转乘号丢长度）；发润达反序规格重排为「外径*壁厚*长度」
                    var pushSpec = SpecParser.NormalizeSpec(item.Spec, IsFarada(customer.Name));
                    // 客户图号：三可为 物料编码-收口，华尔达不需要（传空），其余用匹配到的客户图号；客户订单号即本平台订单号
                    var khth = IsSanke(customer.Name)
                        ? BuildSankeKhth(item.MaterialCode, item.ShouKou)
                        : IsHuaruda(customer.Name)
                            ? string.Empty
                            : item.CustomerPartNo;
                    // 摘要（REM）：仅三可 = 收口+「收口」；华尔达的「摘要」列只在平台明细展示，按业务要求不传给 ERP
                    var summary = BuildOrderItemSummary(customer.Name, item);
                    await _erpPushService.CreateOrderItemAsync(new ErpOrderItemRequest
                    {
                        SoNo = osNo,
                        Itm = itm,
                        PrdNo = product.PrdNo,
                        Qty = pushQty,
                        Ydd = FormatYdd(item.ReceiveDate, DateTime.Now),
                        Khth = khth,
                        CusOs = order.OrderNo,
                        Rem = summary
                    }, cancellationToken);

                    item.ItemPushStatus = ItemPushStatus.Pushed;
                    item.ErpOsNo = osNo;
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
                        CusOs = order.OrderNo,
                        Rem = summary
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
                var r = await PushAsync(id, null, cancellationToken);
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

        // ERP 货品规格口径统一是「外径*壁厚*模数」，只有客户资料里的规格是这个口径：
        //   华尔达 / 发润达：订单规格第三段是长度（16*2*530、1.4*12*585），直接写 SPC 会把长度带进货品档案；
        //   马鞍山仪达：订单 PDF 没有规格列（Spec 为空），只能取资料规格；
        //   创达 / 三可：订单规格规范化后就是「外径*壁厚*模数」，可直接用，资料作为兜底。
        // 因此按 NEST 图号 / 客户图号建立「图号 → 资料规格」字典，供新建货品时取用。
        var partSpecByNo = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var customerParts = await _customerPartRepository.ListByCustomerAsync(order.CustomerId, cancellationToken);
        foreach (var part in customerParts)
        {
            var partSpec = (part.Spec ?? string.Empty).Trim();
            if (partSpec.Length == 0)
            {
                continue;
            }

            foreach (var key in new[] { part.NestPartNo, part.CustomerPartNo })
            {
                var no = (key ?? string.Empty).Trim();
                if (no.Length > 0)
                {
                    partSpecByNo.TryAdd(no, partSpec);
                }
            }
        }

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
                    // 新建货品的规格（SPC）取「外径*壁厚*模数」口径：
                    //   - 华尔达 / 发润达：订单规格第三段是长度，必须取客户资料里该图号的规格（16*2*10、12*1.4*7）；
                    //   - 马鞍山仪达：订单没有规格（Spec 为空），同样回退客户资料；
                    //   - 创达 / 三可：订单规格规范化后即为「外径*壁厚*模数」，优先用订单，资料兜底。
                    // 拿不到合规规格时判失败并提示补资料，绝不把长度或空规格写进 ERP 货品档案。
                    var orderSpec = SpecParser.NormalizeSpec(item.Spec);
                    var useOrderSpec = !IsFarada(customerName) && !IsHuaruda(customerName);
                    var createSpc = useOrderSpec ? orderSpec : string.Empty;

                    if (string.IsNullOrWhiteSpace(createSpc)
                        && partSpecByNo.TryGetValue(th, out var partSpec)
                        && !string.IsNullOrWhiteSpace(partSpec))
                    {
                        createSpc = SpecParser.NormalizeSpec(partSpec);
                    }

                    if (string.IsNullOrWhiteSpace(createSpc))
                    {
                        item.MaterialSyncStatus = MaterialSyncStatus.Failed;
                        item.ErpPrdNo = string.Empty;
                        failed++;
                        errors.Add($"第 {item.LineNo} 行：客户资料缺少图号 {th} 的规格，无法新建货品（需为「外径*壁厚*模数」）");
                        _logger.LogWarning("订单 {OrderNo} 第 {LineNo} 行：客户资料缺少图号 {NestNo} 的规格，跳过新建货品", order.OrderNo, item.LineNo, th);
                        continue;
                    }

                    if (SpecParser.LooksLikeLength(createSpc))
                    {
                        item.MaterialSyncStatus = MaterialSyncStatus.Failed;
                        item.ErpPrdNo = string.Empty;
                        failed++;
                        errors.Add($"第 {item.LineNo} 行：图号 {th} 的规格「{createSpc}」第三段像长度，不能作为货品规格（应为「外径*壁厚*模数」）");
                        _logger.LogWarning("订单 {OrderNo} 第 {LineNo} 行：图号 {NestNo} 的规格「{Spec}」疑似带长度，跳过新建货品", order.OrderNo, item.LineNo, th, createSpc);
                        continue;
                    }

                    // ERP 中不存在该货品：调 PRD_TB 新建（米重系统内无数据，不传），再复查回填
                    await _erpPushService.CreateProductAsync(new ErpProductCreateRequest
                    {
                        Th = th,
                        Pic = pic,
                        Spc = createSpc,
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
                        // 复查结果同样校验：万一 ERP 返回的规格带长度，也不能当成功
                        if (SpecParser.LooksLikeLength(requery.Spc))
                        {
                            item.MaterialSyncStatus = MaterialSyncStatus.Failed;
                            item.ErpPrdNo = string.Empty;
                            failed++;
                            errors.Add($"第 {item.LineNo} 行：ERP 货品 {requery.PrdNo} 的规格「{requery.Spc}」带长度，需先在 ERP 货品资料改成「外径*壁厚*模数」");
                            _logger.LogWarning("订单 {OrderNo} 第 {LineNo} 行：新建后复查货品 {PrdNo} 规格「{Spec}」带长度", order.OrderNo, item.LineNo, requery.PrdNo, requery.Spc);
                            continue;
                        }

                        // 命中后回填：规格取接口返回的 SPC（规范化：去材质留孔型、减号结构转乘号，不为空时覆盖解析值）
                        // 华尔达 / 发润达例外：订单规格第三段是长度，与 ERP 的「外径*壁厚*模数」不同口径，
                        // 覆盖后会出现「规格里没有长度、长度列却有值」的错位，因此这两家保留订单原规格。
                        if (!OrderSpecCarriesLength(customerName) && !string.IsNullOrWhiteSpace(requery.Spc))
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
                    // 已有货品：规格带长度说明档案是错的（如 1.4*12*547、16*2*530），
                    // 继续用它推送会在受订单里把长度当成货品规格带出来，必须先在 ERP 货品资料更正。
                    if (SpecParser.LooksLikeLength(product.Spc))
                    {
                        item.MaterialSyncStatus = MaterialSyncStatus.Failed;
                        item.ErpPrdNo = string.Empty;
                        failed++;
                        errors.Add($"第 {item.LineNo} 行：ERP 货品 {product.PrdNo} 的规格「{product.Spc}」带长度，需先在 ERP 货品资料改成「外径*壁厚*模数」");
                        _logger.LogWarning("订单 {OrderNo} 第 {LineNo} 行：ERP 货品 {PrdNo} 规格「{Spec}」带长度，跳过同步", order.OrderNo, item.LineNo, product.PrdNo, product.Spc);
                        continue;
                    }

                    // 命中后回填：规格取接口返回的 SPC（规范化：去材质留孔型、减号结构转乘号，不为空时覆盖解析值）
                    // 华尔达 / 发润达例外：订单规格第三段是长度，与 ERP 的「外径*壁厚*模数」不同口径，
                    // 覆盖后会出现「规格里没有长度、长度列却有值」的错位，因此这两家保留订单原规格。
                    if (!OrderSpecCarriesLength(customerName) && !string.IsNullOrWhiteSpace(product.Spc))
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

    /// <summary>判断明细行回填的受单号是否与目标受单号一致（忽略空白与大小写）。</summary>
    private static bool IsSameOsNo(string? itemOsNo, string? osNo)
    {
        if (string.IsNullOrWhiteSpace(itemOsNo) || string.IsNullOrWhiteSpace(osNo))
        {
            return false;
        }

        return string.Equals(itemOsNo.Trim(), osNo.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>是否创达客户。</summary>
    private static bool IsChuangda(string? customerName)
    {
        return !string.IsNullOrWhiteSpace(customerName) && customerName.Contains("创达");
    }

    /// <summary>是否发润达客户（含原名称法拉达）。</summary>
    private static bool IsFarada(string? customerName)
    {
        return !string.IsNullOrWhiteSpace(customerName) && (customerName.Contains("发润达") || customerName.Contains("法拉达"));
    }

    /// <summary>是否华尔达客户。</summary>
    private static bool IsHuaruda(string? customerName)
    {
        return !string.IsNullOrWhiteSpace(customerName) && customerName.Contains("华尔达");
    }

    /// <summary>
    /// 订单规格第三段是「长度」（而不是模数）的客户：华尔达、发润达（法拉达）。
    /// 这两家的订单规格与 ERP 货品规格（外径*壁厚*模数）不是同一口径，
    /// 因此明细规格保留订单原值、新建货品必须取客户资料规格。
    /// </summary>
    private static bool OrderSpecCarriesLength(string? customerName)
    {
        return IsHuaruda(customerName) || IsFarada(customerName);
    }

    /// <summary>
    /// 明细摘要（ERP 受订单表身 REM），仅三可发送：
    /// 三可 = 收口 + 「收口」（如 4.0收口 / 6.9收口），无收口时为空不发送；
    /// 华尔达的客户资料「摘要」列（如 40支/把）只在平台明细里展示，按业务要求不再传给 ERP。
    /// </summary>
    private static string BuildOrderItemSummary(string? customerName, OrderItem item)
    {
        if (IsSanke(customerName))
        {
            var sk = NormalizeShouKou(item.ShouKou);
            if (sk.Length == 0 || sk == "0" || sk == "不收口")
            {
                return string.Empty;
            }

            return $"{sk}收口";
        }

        return string.Empty;
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