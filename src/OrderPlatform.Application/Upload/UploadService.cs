using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using OrderPlatform.Application.Orders;
using OrderPlatform.Application.Parsers;
using OrderPlatform.Application.Upload.Dtos;
using OrderPlatform.Domain.Entities;
using OrderPlatform.Domain.Enums;
using OrderPlatform.Domain.Interfaces;
using OrderPlatform.Shared.Api;

namespace OrderPlatform.Application.Upload;

/// <summary>上传服务接口：接收文件、后台解析批次、查询批次与统计。</summary>
public interface IUploadService
{
    /// <summary>接收上传：重名检查、保存文件、创建 Pending 批次并入队，立即返回批次列表。</summary>
    Task<List<UploadBatchDto>> CreateBatchesAsync(IEnumerable<IFormFile> files, Guid userId, CancellationToken cancellationToken);

    /// <summary>后台解析指定批次，更新进度与状态。</summary>
    Task ProcessBatchAsync(Guid batchId, CancellationToken cancellationToken);

    /// <summary>查询批次解析状态与进度。</summary>
    Task<UploadBatchDto> GetBatchAsync(Guid batchId, CancellationToken cancellationToken);

    /// <summary>分页查询历史上传记录。</summary>
    Task<PagedResult<UploadBatchDto>> ListBatchesAsync(int page, int pageSize, CancellationToken cancellationToken);

    /// <summary>查看 Excel 批次解析明细。</summary>
    Task<ExcelBatchDetailDto> GetBatchExcelAsync(Guid batchId, CancellationToken cancellationToken);

    /// <summary>客户图号统计列表（客户 + 订单明细匹配去重图号数）。</summary>
    Task<List<CustomerImportDto>> GetCustomersAsync(CancellationToken cancellationToken);

    /// <summary>查看 PDF 批次生成的订单。</summary>
    Task<List<OrderGeneratedDto>> GetBatchOrdersAsync(Guid batchId, CancellationToken cancellationToken);

    /// <summary>软删除客户资料，保留历史订单关联与图号。</summary>
    Task DeleteCustomerAsync(Guid customerId, CancellationToken cancellationToken);
}

/// <summary>
/// 上传服务实现。核心流程：
/// 1. 接收文件（PDF 订单 / Excel 客户资料 / Excel 订单），同名去重；
/// 2. 创建上传批次并入内存队列，由后台服务异步解析；
/// 3. PDF → 文字层解析（无文字层时走 OCR）；Excel → 自动识别「订单」或「客户资料」；
/// 4. 订单解析后按客户策略匹配图号生成订单；资料导入后回头对未关联订单执行补匹配。
/// </summary>
public class UploadService : IUploadService
{
    private const string RootDirectory = "Uploads";
    private readonly IWebHostEnvironment _env;
    private readonly IUploadBatchRepository _batchRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly ICustomerPartRepository _partRepository;
    private readonly IOrderRepository _orderRepository;
    private readonly IPdfParser _pdfParser;
    private readonly IExcelParser _excelParser;
    private readonly IExcelOrderParser _excelOrderParser;
    private readonly IOcrOrderParser _ocrOrderParser;
    private readonly IUploadJobQueue _jobQueue;
    private readonly IOrderService _orderService;
    private readonly ILogger<UploadService> _logger;

    public UploadService(
        IWebHostEnvironment env,
        IUploadBatchRepository batchRepository,
        ICustomerRepository customerRepository,
        ICustomerPartRepository partRepository,
        IOrderRepository orderRepository,
        IPdfParser pdfParser,
        IExcelParser excelParser,
        IExcelOrderParser excelOrderParser,
        IOcrOrderParser ocrOrderParser,
        IUploadJobQueue jobQueue,
        IOrderService orderService,
        ILogger<UploadService> logger)
    {
        _env = env;
        _batchRepository = batchRepository;
        _customerRepository = customerRepository;
        _partRepository = partRepository;
        _orderRepository = orderRepository;
        _pdfParser = pdfParser;
        _excelParser = excelParser;
        _excelOrderParser = excelOrderParser;
        _ocrOrderParser = ocrOrderParser;
        _jobQueue = jobQueue;
        _orderService = orderService;
        _logger = logger;
    }

    /// <summary>
    /// 接收上传：校验文件非空、同批同类型，逐个保存并创建批次、入队，返回批次列表。
    /// </summary>
    public async Task<List<UploadBatchDto>> CreateBatchesAsync(IEnumerable<IFormFile> files, Guid userId, CancellationToken cancellationToken)
    {
        var fileList = files.Where(f => f.Length > 0).ToList();
        if (fileList.Count == 0)
        {
            throw new BusinessException("未接收到有效文件");
        }

        // 一次上传只能为同一类型（全部 PDF 或全部 Excel）
        var group = fileList.GroupBy(f => GetFileType(f.FileName)).ToList();
        if (group.Count > 1)
        {
            throw new BusinessException("同一批上传的文件须为同一类型（全部 PDF 或全部 Excel）");
        }

        // 同一次请求内不允许出现重名文件（防御前端异常重复提交）
        var duplicateName = fileList
            .GroupBy(f => f.FileName, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(g => g.Count() > 1);
        if (duplicateName is not null)
        {
            throw new BusinessException($"同一批上传中存在重名文件「{duplicateName.Key}」，同名文件不可重复上传");
        }

        var results = new List<UploadBatchDto>();
        foreach (var file in fileList)
        {
            var batch = await SaveFileAsync(file, userId, cancellationToken);
            _jobQueue.Enqueue(batch.Id);
            results.Add(await ToDtoAsync(batch, 0, cancellationToken));
        }

        return results;
    }

    /// <summary>解析批次：更新为 Parsing 状态，按文件类型分派解析，完成后更新状态与进度。</summary>
    public async Task ProcessBatchAsync(Guid batchId, CancellationToken cancellationToken)
    {
        var batch = await _batchRepository.GetByIdAsync(batchId, cancellationToken)
            ?? throw new BusinessException("批次不存在");

        batch.Status = UploadStatus.Parsing;
        batch.Progress = 30;
        await _batchRepository.SaveChangesAsync(cancellationToken);

        var savedPath = Path.Combine(_env.ContentRootPath, batch.OriginalPath);
        try
        {
            if (batch.FileType == "PDF")
            {
                await ProcessPdfAsync(batch, savedPath, cancellationToken);
            }
            else
            {
                // Excel 自动识别：订单文件（含采购单号/订单编号）或客户资料
                _logger.LogInformation("Excel {File} 开始读取工作簿", batch.FileName);
                batch.Progress = 40;
                await _batchRepository.SaveChangesAsync(cancellationToken);
                var grids = ExcelReader.Read(savedPath);
                if (_excelOrderParser.IsOrderWorkbook(grids))
                {
                    // Excel 订单：解析 + 生成订单
                    batch.Progress = 60;
                    await _batchRepository.SaveChangesAsync(cancellationToken);
                    var excelPdf = _excelOrderParser.Parse(grids);
                    _logger.LogInformation("Excel {File} 订单解析成功，明细 {RowCount} 行", batch.FileName, excelPdf.Rows.Count);
                    await CreateOrderFromParseAsync(batch, excelPdf, cancellationToken);
                }
                else
                {
                    await ProcessExcelAsync(batch, savedPath, cancellationToken);
                }
            }

            batch.Status = UploadStatus.Completed;
            batch.Progress = 100;
            // 注意：此处不能清空 ErrorMessage——解析中写入的「订单已存在，已跳过」等提示
            // 是列表页区分「重复导入跳过」与「订单已被删除」的唯一依据
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "解析上传批次 {BatchId} 失败：{Message}", batchId, ex.Message);
            batch.Status = UploadStatus.Failed;
            batch.Progress = 100;
            batch.ErrorMessage = ex.Message;
        }

        await _batchRepository.SaveChangesAsync(cancellationToken);
    }

    /// <summary>保存文件：创建批次记录（Pending），将文件写入 Uploads/yyyy/MM/dd 目录。</summary>
    private async Task<UploadBatch> SaveFileAsync(IFormFile file, Guid userId, CancellationToken cancellationToken)
    {
        var fileType = GetFileType(file.FileName);
        var batchNo = $"{DateTime.Now:yyyyMMddHHmmss}{Guid.NewGuid():N}"[..24];

        var batch = new UploadBatch
        {
            Id = Guid.NewGuid(),
            BatchNo = batchNo,
            FileType = fileType,
            FileName = file.FileName,
            UploadUserId = userId,
            Status = UploadStatus.Pending,
            Progress = 10,
            CreatedAt = DateTime.Now
        };
        await _batchRepository.AddAsync(batch, cancellationToken);
        await _batchRepository.SaveChangesAsync(cancellationToken);

        // 按日期分目录保存，文件名使用 GUID 避免重名覆盖
        var dir = Path.Combine(_env.ContentRootPath, RootDirectory, DateTime.Now.ToString("yyyy"), DateTime.Now.ToString("MM"), DateTime.Now.ToString("dd"));
        Directory.CreateDirectory(dir);
        var savedName = $"{Guid.NewGuid():N}{Path.GetExtension(file.FileName)}";
        var savedPath = Path.Combine(dir, savedName);
        await using (var stream = File.Create(savedPath))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        batch.OriginalPath = Path.GetRelativePath(_env.ContentRootPath, savedPath);
        batch.Progress = 20;
        await _batchRepository.SaveChangesAsync(cancellationToken);
        return batch;
    }

    /// <summary>解析 Excel 客户资料：每个 sheet 视为一个客户，重建其图号资料，并补匹配历史未关联订单。</summary>
    private async Task ProcessExcelAsync(UploadBatch batch, string savedPath, CancellationToken cancellationToken)
    {
        var result = await _excelParser.ParseAsync(savedPath, cancellationToken);
        batch.RawDataJson = JsonSerializer.Serialize(result);
        batch.Progress = 50;

        foreach (var sheet in result.Sheets)
        {
            // sheet 名即客户名，不存在则新建客户
            var customer = await _customerRepository.GetByNameAsync(sheet.SheetName, cancellationToken);
            if (customer is null)
            {
                customer = new Customer
                {
                    Id = Guid.NewGuid(),
                    Name = sheet.SheetName,
                    CreatedAt = DateTime.Now
                };
                await _customerRepository.AddAsync(customer, cancellationToken);
            }

            // 重建该客户的图号资料（先清后插，保证与 Excel 一致）
            await _partRepository.DeleteByCustomerAsync(customer.Id, cancellationToken);
            var parts = new List<CustomerPart>();
            foreach (var row in sheet.Rows)
            {
                var nest = Get(row, "NEST图号") ?? string.Empty;
                var customerPartNo = Get(row, "客户图号") ?? Get(row, "客户新图号") ?? string.Empty;
                var spec = Get(row, "规格") ?? string.Empty;
                // 图号、客户图号、规格全为空的行视为无效行，跳过
                if (string.IsNullOrEmpty(nest) && string.IsNullOrEmpty(customerPartNo) && string.IsNullOrEmpty(spec))
                {
                    continue;
                }

                parts.Add(new CustomerPart
                {
                    Id = Guid.NewGuid(),
                    CustomerId = customer.Id,
                    NestPartNo = nest,
                    CustomerPartNo = customerPartNo,
                    Spray = Get(row, "喷锌") ?? string.Empty,
                    Alloy = Get(row, "材质") ?? Get(row, "合金") ?? string.Empty,
                    Spec = spec,
                    Length = ParseNullableDecimal(Get(row, "长度（mm)") ?? Get(row, "长度")),
                    ShouKou = Get(row, "收口") ?? string.Empty,
                    Raw = string.Join(" | ", row.Values),
                    CreatedAt = DateTime.Now
                });
            }

            if (parts.Count > 0)
            {
                await _partRepository.AddRangeAsync(parts, cancellationToken);
            }
        }

        await _customerRepository.SaveChangesAsync(cancellationToken);
        await _partRepository.SaveChangesAsync(cancellationToken);

        // 补匹配：客户资料导入后，回头关联先前上传的未匹配订单
        await RematchPendingOrdersAsync(cancellationToken);
    }

    /// <summary>补匹配：对未完全关联的订单，依据其 PDF 原始 JSON 重新匹配最新客户图号资料。</summary>
    private async Task RematchPendingOrdersAsync(CancellationToken cancellationToken)
    {
        var orders = await _orderRepository.ListPendingMatchAsync(cancellationToken);
        if (orders.Count == 0)
        {
            return;
        }

        foreach (var order in orders)
        {
            if (string.IsNullOrEmpty(order.PdfRawJson))
            {
                continue;
            }

            var pdf = JsonSerializer.Deserialize<PdfParseResult>(order.PdfRawJson);
            if (pdf is null || string.IsNullOrWhiteSpace(pdf.BuyerName))
            {
                continue;
            }

            var customer = await FindCustomerAsync(pdf.BuyerName, string.Empty, cancellationToken);
            if (customer is null)
            {
                continue;
            }

            order.CustomerId = customer.Id;
            var parts = await _partRepository.ListByCustomerAsync(customer.Id, cancellationToken);

            var allMatched = true;
            foreach (var item in order.Items)
            {
                var row = pdf.Rows.FirstOrDefault(r => r.LineNo == item.LineNo);
                if (row is null)
                {
                    continue;
                }

                var match = MatchService.Match(customer.Name, row, parts);
                var spec = SpecParser.ParseOrderSpec(row.Spec);
                item.OuterDiameter = spec.OuterDiameter;
                item.WallThickness = spec.WallThickness;
                item.Module = spec.Module;
                item.ShouKou = spec.ShouKou;
                item.Material = MatchService.ExtractMaterial(row.Material) is var m && m.Length > 0 ? m : spec.Material;
                item.CustomerPartNo = match.CustomerPartNo;
                item.NestPartNo = match.NestPartNo;
                item.Alloy = match.Alloy;
                item.Spray = match.Spray;
                item.Length = spec.Length ?? match.Length;
                item.Remark = row.Remark;
                item.MatchStatus = match.Status;
                if (match.Status != MatchStatus.Matched)
                {
                    allMatched = false;
                }
            }

            if (order.Items.Count == 0)
            {
                continue;
            }

            // 根据匹配结果汇总订单关联状态
            order.ParseStatus = allMatched
                ? MatchStatus.Matched
                : order.Items.Any(i => i.MatchStatus == MatchStatus.Matched)
                    ? MatchStatus.Partial
                    : MatchStatus.Unmatched;

            // 同步来源批次上的客户
            if (order.SourceFileId.HasValue)
            {
                var batch = await _batchRepository.GetByIdAsync(order.SourceFileId.Value, cancellationToken);
                if (batch is not null && batch.CustomerId != customer.Id)
                {
                    batch.CustomerId = customer.Id;
                    _batchRepository.Update(batch);
                }
            }

            _orderRepository.Update(order);
        }

        await _batchRepository.SaveChangesAsync(cancellationToken);
        await _orderRepository.SaveChangesAsync(cancellationToken);
    }

    /// <summary>解析 PDF 订单：文字层解析（无文字层时 OCR），去重后匹配图号并生成订单。</summary>
    private async Task ProcessPdfAsync(UploadBatch batch, string savedPath, CancellationToken cancellationToken)
    {
        var pdf = await _pdfParser.ParseAsync(savedPath, cancellationToken);

        // 文字层解析命中：记录行数并推进进度
        if (pdf.Rows.Count > 0)
        {
            _logger.LogInformation("PDF {File} 文字层解析成功，明细 {RowCount} 行", batch.FileName, pdf.Rows.Count);
        }

        // 无文字层（文字被曲线化，如创达）→ 渲染 + OCR 解析
        if (pdf.Rows.Count == 0 && _ocrOrderParser.NeedsOcr(savedPath))
        {
            _logger.LogInformation("PDF {File} 无文字层，转 OCR 解析", batch.FileName);
            batch.Progress = 60;
            await _batchRepository.SaveChangesAsync(cancellationToken);
            pdf = _ocrOrderParser.Parse(savedPath, cancellationToken);
            _logger.LogInformation("PDF {File} OCR 完成，明细 {RowCount} 行", batch.FileName, pdf.Rows.Count);
        }

        await CreateOrderFromParseAsync(batch, pdf, cancellationToken);
    }

    /// <summary>
    /// 由解析结果（PDF / OCR / Excel 订单统一结构）生成订单：客户识别、按客户策略匹配图号。
    /// 创达按行备注中的子订单号拆分为多个订单，其余客户维持单个订单。
    /// </summary>
    private async Task CreateOrderFromParseAsync(UploadBatch batch, PdfParseResult pdf, CancellationToken cancellationToken)
    {
        // 客户识别：解析出的客户名精确匹配，否则尝试包含匹配
        var customer = await FindCustomerAsync(pdf.BuyerName, batch.FileName, cancellationToken);

        var strategyName = customer?.Name ?? pdf.BuyerName;
        var parts = customer is null
            ? new List<CustomerPart>()
            : await _partRepository.ListByCustomerAsync(customer.Id, cancellationToken);

        // 创达：订单号在行备注中，按子订单号拆分为多个订单
        if (IsChuangda(customer?.Name) || IsChuangda(pdf.BuyerName))
        {
            await CreateChuangdaOrdersAsync(batch, pdf, customer, strategyName, parts, cancellationToken);
        }
        else
        {
            var orderNo = string.IsNullOrEmpty(pdf.OrderNo)
                ? $"ORDER-{DateTime.Now:yyyyMMddHHmmss}"
                : pdf.OrderNo;
            await CreateSingleOrderAsync(batch, pdf, customer, orderNo, pdf.Rows, strategyName, parts, cancellationToken);
        }

        batch.CustomerId = customer?.Id;
        batch.RawDataJson = JsonSerializer.Serialize(pdf);
    }

    /// <summary>创达订单：按行备注中的子订单号拆分为多个订单；空备注行并入上方最近有编号的行所在组。</summary>
    private async Task CreateChuangdaOrdersAsync(
        UploadBatch batch,
        PdfParseResult pdf,
        Customer? customer,
        string strategyName,
        List<CustomerPart> parts,
        CancellationToken cancellationToken)
    {
        var groups = GroupChuangdaRows(pdf.Rows);
        var fallbackOrderNo = string.IsNullOrEmpty(pdf.OrderNo)
            ? $"ORDER-{DateTime.Now:yyyyMMddHHmmss}"
            : pdf.OrderNo;

        if (groups.Count == 0)
        {
            // 未提取到任何子订单号：回退单订单（保持原行为）
            await CreateSingleOrderAsync(batch, pdf, customer, fallbackOrderNo, pdf.Rows, strategyName, parts, cancellationToken);
            return;
        }

        foreach (var (orderNo, rows) in groups)
        {
            var no = string.IsNullOrWhiteSpace(orderNo) ? fallbackOrderNo : orderNo;
            await CreateSingleOrderAsync(batch, pdf, customer, no, rows, strategyName, parts, cancellationToken);
        }
    }

    /// <summary>把创达明细行按行备注中的子订单号分组，空备注行并入上方最近有编号的行所在组。</summary>
    private static List<(string OrderNo, List<PdfParseRow> Rows)> GroupChuangdaRows(IEnumerable<PdfParseRow> rows)
    {
        var groups = new List<(string OrderNo, List<PdfParseRow> Rows)>();
        var pendingNo = string.Empty;
        List<PdfParseRow>? pending = null;

        foreach (var row in rows)
        {
            var no = PdfParser.ExtractOrderNoFromRemark(row.Remark);
            if (no.Length > 0 && no != pendingNo)
            {
                // 子订单号变化：收尾上一组，开启新组；相同编号的连续行并入当前组
                if (pending is not null && pending.Count > 0)
                {
                    groups.Add((pendingNo, pending));
                }

                pendingNo = no;
                pending = new List<PdfParseRow>();
            }

            if (pending is null)
            {
                pending = new List<PdfParseRow>();
            }

            if (pendingNo.Length == 0)
            {
                // 首行即无编号（异常场景）：单独记为一组兜底，编号为空时回退表头订单号
                groups.Add((string.Empty, new List<PdfParseRow> { row }));
                continue;
            }

            pending.Add(row);
        }

        if (pending is not null && pending.Count > 0)
        {
            groups.Add((pendingNo, pending));
        }

        return groups;
    }

    /// <summary>由解析行构建单个订单：订单号去重、逐行匹配生成明细、汇总保存。</summary>
    private async Task CreateSingleOrderAsync(
        UploadBatch batch,
        PdfParseResult pdf,
        Customer? customer,
        string orderNo,
        List<PdfParseRow> rows,
        string strategyName,
        List<CustomerPart> parts,
        CancellationToken cancellationToken)
    {
        // 订单号去重：已存在则跳过创建
        var existingOrder = await _orderRepository.GetByOrderNoAsync(orderNo, cancellationToken);
        if (existingOrder is not null)
        {
            batch.ErrorMessage = $"订单「{orderNo}」已存在，已跳过重复导入";
            return;
        }

        var order = new OrderMain
        {
            Id = Guid.NewGuid(),
            OrderNo = orderNo,
            CustomerId = customer?.Id ?? Guid.Empty,
            OrderDate = pdf.OrderDate,
            SourceFileId = batch.Id,
            ParseStatus = MatchStatus.Unmatched,
            PushStatus = PushStatus.NotPushed,
            PdfRawJson = JsonSerializer.Serialize(pdf),
            CreatedAt = DateTime.Now
        };

        // 逐行解析明细并匹配客户图号
        var allMatched = true;
        var isChuangda = IsChuangda(customer?.Name) || IsChuangda(strategyName);
        foreach (var row in rows)
        {
            var match = MatchService.Match(strategyName, row, parts);
            if (match.Status != MatchStatus.Matched)
            {
                allMatched = false;
            }

            var spec = SpecParser.ParseOrderSpec(row.Spec);

            // 材质：三可取订单文件材质列（3F03+Zn-H112 → 3F03），其余客户取客户资料匹配到的合金
            var orderMaterial = ResolveOrderMaterial(strategyName, row, match);

            // 创达数量以行备注「XXX根」为准（如「202608235A，1840根」→ 1840）
            var quantity = isChuangda
                ? ExtractQuantityFromRemark(row.Remark) ?? row.Quantity
                : row.Quantity;

            // 规格统一规范化：去括号内材质留孔型、「外径*壁厚-模数*长度」减号转乘号并丢长度
            // （如创达 18*1.8-14*210 → 18*1.8*14；三可 25.4*2-13*1686*4 → 25.4*2*13）
            var specText = SpecParser.NormalizeSpec(row.Spec);

            order.Items.Add(new OrderItem
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                LineNo = row.LineNo,
                MaterialCode = row.MaterialCode,
                MaterialName = row.MaterialName,
                Spec = specText,
                OuterDiameter = spec.OuterDiameter,
                WallThickness = spec.WallThickness,
                Module = spec.Module,
                ShouKou = spec.ShouKou,
                Material = orderMaterial,
                CustomerPartNo = match.CustomerPartNo,
                NestPartNo = match.NestPartNo,
                Alloy = match.Alloy,
                Spray = match.Spray,
                Length = spec.Length ?? match.Length,
                Quantity = quantity,
                Unit = row.Unit,
                Price = row.Price,
                Amount = row.Amount,
                ReceiveDate = ResolveReceiveDate(strategyName, row.ReceiveDate),
                Remark = row.Remark,
                MatchStatus = match.Status,
                MaterialSyncStatus = MaterialSyncStatus.NotSynced,
                ItemPushStatus = ItemPushStatus.NotPushed,
                CreatedAt = DateTime.Now
            });
        }

        if (order.Items.Count > 0)
        {
            // 汇总订单关联状态与合计
            if (allMatched)
            {
                order.ParseStatus = MatchStatus.Matched;
            }
            else if (order.Items.Any(i => i.MatchStatus == MatchStatus.Matched))
            {
                order.ParseStatus = MatchStatus.Partial;
            }
            else
            {
                order.ParseStatus = MatchStatus.Unmatched;
            }

            order.TotalQuantity = order.Items.Sum(i => i.Quantity);
            order.TotalAmount = order.Items.Sum(i => i.Amount);
            await _orderRepository.AddAsync(order, cancellationToken);
            await _orderRepository.SaveChangesAsync(cancellationToken);

            // 拆分多订单时，本次存在新创建订单则清掉先前任务「已存在，已跳过」的提示
            if (batch.ErrorMessage?.Contains("已存在") == true)
            {
                batch.ErrorMessage = null;
            }

            // 自动物料同步（异步后台任务内执行）：对已匹配行调 get_PRD（NEST 图号+长度）查询货品代号，
            // ERP 中存在则回填规格并标记已同步，不存在则自动新建货品后复查。
            // 失败不中断批次解析，仅记录日志（可在订单详情手动重新同步）。
            if (order.Items.Any(i => i.MatchStatus == MatchStatus.Matched))
            {
                try
                {
                    var syncResult = await _orderService.SyncMaterialAsync(order.Id, null, cancellationToken);
                    _logger.LogInformation(
                        "订单 {OrderNo} 自动物料同步完成：共 {Total} 行，同步成功 {Synced} 行（新建 {Created}），未找到 {NotFound}，失败 {Failed}",
                        order.OrderNo, syncResult.Total, syncResult.Synced, syncResult.Created, syncResult.NotFound, syncResult.Failed);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "订单 {OrderNo} 自动物料同步失败：{Message}", order.OrderNo, ex.Message);
                }
            }
        }
        else
        {
            // 未解析出任何明细行：明确记录「未生成订单」，避免列表页误标为「订单已删除」
            batch.ErrorMessage = "未解析出有效订单明细，未生成订单";
        }
    }

    /// <summary>是否创达客户。</summary>
    private static bool IsChuangda(string? name)
    {
        return !string.IsNullOrWhiteSpace(name) && name.Contains("创达");
    }

    /// <summary>是否三可客户。</summary>
    private static bool IsSanke(string? name)
    {
        return !string.IsNullOrWhiteSpace(name) && name.Contains("三可");
    }

    /// <summary>是否发润达客户（含原名称法拉达）。</summary>
    private static bool IsFarada(string? name)
    {
        return !string.IsNullOrWhiteSpace(name) && (name.Contains("发润达") || name.Contains("法拉达"));
    }

    /// <summary>明细交货日期：发润达提前 5 天交期，其余客户按单据原值。</summary>
    private static DateTime? ResolveReceiveDate(string? customerName, DateTime? receiveDate)
    {
        if (IsFarada(customerName) && receiveDate.HasValue)
        {
            return receiveDate.Value.AddDays(-5);
        }

        return receiveDate;
    }

    /// <summary>
    /// 明细材质：三可取订单文件材质列并提取牌号（如 3F03+Zn-H112 → 3F03），缺失时回退到客户资料匹配到的合金；
    /// 其余客户取客户资料匹配到的合金。
    /// </summary>
    private static string ResolveOrderMaterial(string? customerName, PdfParseRow row, MatchResult match)
    {
        if (IsSanke(customerName))
        {
            var extracted = MatchService.ExtractMaterial(row.Material);
            if (extracted.Length > 0)
            {
                return extracted;
            }
        }

        return match.Alloy;
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

    /// <summary>按客户名识别客户：先精确匹配，再尝试包含匹配。</summary>
    private async Task<Customer?> FindCustomerAsync(string buyerName, string fileName, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(buyerName))
        {
            var byName = await _customerRepository.GetByNameAsync(buyerName, cancellationToken);
            if (byName is not null)
            {
                return byName;
            }

            // 模糊匹配：客户名是 sheet 名，可能是全称，尝试包含匹配
            var customers = await _customerRepository.ListAsync(cancellationToken);
            var match = customers.FirstOrDefault(c => buyerName.Contains(c.Name, StringComparison.Ordinal));
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }

    /// <summary>查询单批次状态（含生成的订单数）。</summary>
    public async Task<UploadBatchDto> GetBatchAsync(Guid batchId, CancellationToken cancellationToken)
    {
        var batch = await _batchRepository.GetByIdAsync(batchId, cancellationToken)
            ?? throw new BusinessException("批次不存在");
        var counts = await _orderRepository.CountBySourceFileIdsAsync(new[] { batch.Id }, cancellationToken);
        return await ToDtoAsync(batch, counts.GetValueOrDefault(batch.Id, 0), cancellationToken);
    }

    /// <summary>分页查询历史上传记录（含各批次生成的订单数）。</summary>
    public async Task<PagedResult<UploadBatchDto>> ListBatchesAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var batches = await _batchRepository.ListAsync(page, pageSize, cancellationToken);
        var total = await _batchRepository.CountAsync(cancellationToken);
        var orderCounts = await _orderRepository.CountBySourceFileIdsAsync(batches.Select(b => b.Id), cancellationToken);
        // 批量预取客户，避免每个批次一次 N+1 查询
        var customerMap = (await _customerRepository.ListAsync(cancellationToken)).ToDictionary(c => c.Id);
        var items = new List<UploadBatchDto>();
        foreach (var batch in batches)
        {
            items.Add(await ToDtoAsync(batch, orderCounts.GetValueOrDefault(batch.Id, 0), cancellationToken, customerMap));
        }

        return new PagedResult<UploadBatchDto>(items, total);
    }

    /// <summary>查看 Excel 批次解析明细（从 RawDataJson 反序列化，兼容客户资料与订单两类结构）。</summary>
    public async Task<ExcelBatchDetailDto> GetBatchExcelAsync(Guid batchId, CancellationToken cancellationToken)
    {
        var batch = await _batchRepository.GetByIdAsync(batchId, cancellationToken)
            ?? throw new BusinessException("批次不存在");
        if (batch.FileType != "Excel" || string.IsNullOrEmpty(batch.RawDataJson))
        {
            throw new BusinessException("该批次不是 Excel 文件或尚无解析数据");
        }

        // 客户资料：{"Sheets":[{SheetName,Headers,Rows}]}；订单：PdfParseResult 结构含 Sheets
        var sheets = new List<ExcelSheetDetailDto>();
        var parsed = JsonSerializer.Deserialize<ExcelParseResult>(batch.RawDataJson);
        if (parsed?.Sheets.Count > 0)
        {
            sheets.AddRange(parsed.Sheets.Select(s => new ExcelSheetDetailDto
            {
                SheetName = s.SheetName,
                Headers = s.Headers,
                Rows = s.Rows
            }));
        }
        else
        {
            var order = JsonSerializer.Deserialize<PdfParseResult>(batch.RawDataJson);
            if (order is null)
            {
                return new ExcelBatchDetailDto { BatchId = batch.Id, BatchNo = batch.BatchNo, FileName = batch.FileName };
            }

            if (order.Sheets is { Count: > 0 })
            {
                sheets.AddRange(order.Sheets.Select(s => new ExcelSheetDetailDto
                {
                    SheetName = s.SheetName,
                    Headers = s.Headers,
                    Rows = s.Rows
                }));
            }
            else if (order.Rows.Count > 0)
            {
                // 历史批次（未保存原始表）：由解析行回退生成可读明细表
                sheets.Add(new ExcelSheetDetailDto
                {
                    SheetName = order.BuyerName,
                    Headers = new List<string> { "行号", "存货编码", "存货名称", "规格型号", "材质", "单位", "数量", "单价", "金额", "备注", "原始行" },
                    Rows = order.Rows.Select(r => new Dictionary<string, string>
                    {
                        ["行号"] = r.LineNo.ToString(),
                        ["存货编码"] = r.MaterialCode,
                        ["存货名称"] = r.MaterialName,
                        ["规格型号"] = r.Spec,
                        ["材质"] = r.Material,
                        ["单位"] = r.Unit,
                        ["数量"] = r.Quantity.ToString(),
                        ["单价"] = r.Price.ToString(),
                        ["金额"] = r.Amount.ToString(),
                        ["备注"] = r.Remark,
                        ["原始行"] = r.Raw
                    }).ToList()
                });
            }
        }

        return new ExcelBatchDetailDto
        {
            BatchId = batch.Id,
            BatchNo = batch.BatchNo,
            FileName = batch.FileName,
            Sheets = sheets
        };
    }

    /// <summary>客户图号统计：按订单明细中已匹配的去重客户图号数统计。</summary>
    public async Task<List<CustomerImportDto>> GetCustomersAsync(CancellationToken cancellationToken)
    {
        var customers = await _customerRepository.ListAsync(cancellationToken);
        var counts = await _orderRepository.CountMatchedPartNosByCustomersAsync(customers.Select(c => c.Id), cancellationToken);
        return customers.Select(c => new CustomerImportDto
        {
            CustomerId = c.Id,
            CustomerName = c.Name,
            PartCount = counts.GetValueOrDefault(c.Id, 0)
        }).ToList();
    }

    /// <summary>查看 PDF 批次生成的订单（订单与批次通过 SourceFileId 关联）。</summary>
    public async Task<List<OrderGeneratedDto>> GetBatchOrdersAsync(Guid batchId, CancellationToken cancellationToken)
    {
        // 订单与批次通过 SourceFileId 关联，查询该批次生成的真实入库订单
        var orders = await _orderRepository.ListBySourceFileIdAsync(batchId, cancellationToken);
        if (orders.Count == 0)
        {
            return new List<OrderGeneratedDto>();
        }

        var result = new List<OrderGeneratedDto>();
        // 批量预取客户，避免每个订单一次 N+1 查询
        var customerNames = (await _customerRepository.ListAsync(cancellationToken)).ToDictionary(c => c.Id);
        foreach (var order in orders)
        {
            result.Add(new OrderGeneratedDto
            {
                OrderId = order.Id,
                OrderNo = order.OrderNo,
                CustomerId = order.CustomerId,
                CustomerName = order.CustomerId == Guid.Empty ? null : customerNames.GetValueOrDefault(order.CustomerId)?.Name,
                ItemCount = order.Items.Count,
                ParseStatus = order.ParseStatus,
                Items = order.Items.Select(i => new MatchResultItem
                {
                    LineNo = i.LineNo,
                    MaterialCode = i.MaterialCode,
                    Spec = i.Spec,
                    Quantity = i.Quantity,
                    Unit = i.Unit,
                    Remark = i.Remark,
                    CustomerPartNo = i.CustomerPartNo,
                    NestPartNo = i.NestPartNo,
                    MatchStatus = i.MatchStatus
                }).ToList()
            });
        }

        return result;
    }

    /// <summary>软删除客户（保留历史订单关联与图号统计）。</summary>
    public async Task DeleteCustomerAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var customer = await _customerRepository.GetByIdAsync(customerId, cancellationToken)
            ?? throw new BusinessException("客户不存在");

        await _customerRepository.SoftDeleteAsync(customerId, cancellationToken);
        await _customerRepository.SaveChangesAsync(cancellationToken);
    }

    /// <summary>批次实体转 DTO，计算订单删除标记（订单文件已完成但已无订单，且非重复导入 / 未解析出明细）。</summary>
    private async Task<UploadBatchDto> ToDtoAsync(
        UploadBatch batch,
        int orderCount,
        CancellationToken cancellationToken,
        Dictionary<Guid, Customer>? customerMap = null)
    {
        // 订单文件判定：PDF 一律为订单；Excel 订单会写入识别到的客户，Excel 客户资料不写（CustomerId 为空）
        var isOrderFile = batch.FileType == "PDF"
            || (batch.FileType == "Excel" && batch.CustomerId.HasValue);
        // 「已存在」= 重复导入跳过；「未解析出有效订单明细」= 从未生成订单。两者都不属于「订单已删除」
        var skippedAsDuplicate = batch.ErrorMessage?.Contains("已存在") ?? false;
        var noOrderRows = batch.ErrorMessage?.Contains("未解析出有效订单明细") ?? false;

        var dto = new UploadBatchDto
        {
            BatchId = batch.Id,
            BatchNo = batch.BatchNo,
            FileType = batch.FileType,
            FileName = batch.FileName,
            CustomerId = batch.CustomerId,
            Status = batch.Status.ToString(),
            Progress = batch.Progress,
            ErrorMessage = batch.ErrorMessage,
            OrderCount = orderCount,
            OrderDeleted = isOrderFile
                && batch.Status == UploadStatus.Completed
                && orderCount == 0
                && !skippedAsDuplicate
                && !noOrderRows,
            CreatedAt = batch.CreatedAt
        };

        if (batch.CustomerId.HasValue)
        {
            if (customerMap is not null)
            {
                dto.CustomerName = customerMap.GetValueOrDefault(batch.CustomerId.Value)?.Name;
            }
            else
            {
                var customer = await _customerRepository.GetByIdAsync(batch.CustomerId.Value, cancellationToken);
                dto.CustomerName = customer?.Name;
            }
        }

        return dto;
    }

    /// <summary>按扩展名判断文件类型。</summary>
    private static string GetFileType(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".pdf" => "PDF",
            ".xls" or ".xlsx" => "Excel",
            _ => throw new BusinessException($"不支持的文件格式：{ext}")
        };
    }

    /// <summary>从行字典中按列名取值（忽略列名首尾空格）。</summary>
    private static string? Get(Dictionary<string, string> row, string key)
    {
        foreach (var (k, v) in row)
        {
            if (k.Trim() == key.Trim())
            {
                return v;
            }
        }

        return null;
    }

    /// <summary>解析可空小数（去除千分位逗号）。</summary>
    private static decimal? ParseNullableDecimal(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return decimal.TryParse(text.Replace(",", string.Empty), out var v) ? v : null;
    }
}