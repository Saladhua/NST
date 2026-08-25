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
                var grids = ExcelReader.Read(savedPath);
                if (_excelOrderParser.IsOrderWorkbook(grids))
                {
                    await CreateOrderFromParseAsync(batch, _excelOrderParser.Parse(grids), cancellationToken);
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
                    Alloy = Get(row, "合金") ?? string.Empty,
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

        // 无文字层（文字被曲线化，如创达）→ 渲染 + OCR 解析
        if (pdf.Rows.Count == 0 && _ocrOrderParser.NeedsOcr(savedPath))
        {
            _logger.LogInformation("PDF {File} 无文字层，转 OCR 解析", batch.FileName);
            batch.Progress = 60;
            await _batchRepository.SaveChangesAsync(cancellationToken);
            pdf = _ocrOrderParser.Parse(savedPath, cancellationToken);
        }

        await CreateOrderFromParseAsync(batch, pdf, cancellationToken);
    }

    /// <summary>
    /// 由解析结果（PDF / OCR / Excel 订单统一结构）生成订单：订单号去重、客户识别、按客户策略匹配图号。
    /// </summary>
    private async Task CreateOrderFromParseAsync(UploadBatch batch, PdfParseResult pdf, CancellationToken cancellationToken)
    {
        var orderNo = string.IsNullOrEmpty(pdf.OrderNo)
            ? $"ORDER-{DateTime.Now:yyyyMMddHHmmss}"
            : pdf.OrderNo;

        // 客户识别：解析出的客户名精确匹配，否则尝试包含匹配
        var customer = await FindCustomerAsync(pdf.BuyerName, batch.FileName, cancellationToken);

        // 创达：订单号在行备注中（如 202608031B），优先于表头编号
        if (IsChuangda(customer?.Name) || IsChuangda(pdf.BuyerName))
        {
            var remarkOrderNo = PdfParser.ExtractOrderNoFromRemarks(pdf.Rows);
            if (remarkOrderNo.Length > 0)
            {
                orderNo = remarkOrderNo;
            }
        }

        // 订单号去重：已存在则跳过创建
        var existingOrder = await _orderRepository.GetByOrderNoAsync(orderNo, cancellationToken);
        if (existingOrder is not null)
        {
            batch.ErrorMessage = $"订单「{orderNo}」已存在，已跳过重复导入";
            batch.RawDataJson = JsonSerializer.Serialize(pdf);
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

        var parts = customer is null
            ? new List<CustomerPart>()
            : await _partRepository.ListByCustomerAsync(customer.Id, cancellationToken);
        var strategyName = customer?.Name ?? pdf.BuyerName;

        // 逐行解析明细并匹配客户图号
        var allMatched = true;
        foreach (var row in pdf.Rows)
        {
            var match = MatchService.Match(strategyName, row, parts);
            if (match.Status != MatchStatus.Matched)
            {
                allMatched = false;
            }

            var spec = SpecParser.ParseOrderSpec(row.Spec);
            var orderMaterial = MatchService.ExtractMaterial(row.Material);
            if (orderMaterial.Length == 0)
            {
                orderMaterial = spec.Material;
            }

            order.Items.Add(new OrderItem
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                LineNo = row.LineNo,
                MaterialCode = row.MaterialCode,
                MaterialName = row.MaterialName,
                Spec = row.Spec,
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
                Quantity = row.Quantity,
                Unit = row.Unit,
                Price = row.Price,
                Amount = row.Amount,
                ReceiveDate = row.ReceiveDate,
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
        }
        else
        {
            // 未解析出任何明细行：明确记录「未生成订单」，避免列表页误标为「订单已删除」
            batch.ErrorMessage = "未解析出有效订单明细，未生成订单";
        }

        batch.CustomerId = customer?.Id;
        batch.RawDataJson = JsonSerializer.Serialize(pdf);
    }

    /// <summary>是否创达客户。</summary>
    private static bool IsChuangda(string? name)
    {
        return !string.IsNullOrWhiteSpace(name) && name.Contains("创达");
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
        var items = new List<UploadBatchDto>();
        foreach (var batch in batches)
        {
            items.Add(await ToDtoAsync(batch, orderCounts.GetValueOrDefault(batch.Id, 0), cancellationToken));
        }

        return new PagedResult<UploadBatchDto>(items, total);
    }

    /// <summary>查看 Excel 批次解析明细（从 RawDataJson 反序列化）。</summary>
    public async Task<ExcelBatchDetailDto> GetBatchExcelAsync(Guid batchId, CancellationToken cancellationToken)
    {
        var batch = await _batchRepository.GetByIdAsync(batchId, cancellationToken)
            ?? throw new BusinessException("批次不存在");
        if (batch.FileType != "Excel" || string.IsNullOrEmpty(batch.RawDataJson))
        {
            throw new BusinessException("该批次不是 Excel 文件或尚无解析数据");
        }

        var result = JsonSerializer.Deserialize<ExcelParseResult>(batch.RawDataJson);
        return new ExcelBatchDetailDto
        {
            BatchId = batch.Id,
            BatchNo = batch.BatchNo,
            FileName = batch.FileName,
            Sheets = result?.Sheets.Select(s => new ExcelSheetDetailDto
            {
                SheetName = s.SheetName,
                Headers = s.Headers,
                Rows = s.Rows
            }).ToList() ?? new List<ExcelSheetDetailDto>()
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
        foreach (var order in orders)
        {
            Customer? customer = null;
            if (order.CustomerId != Guid.Empty)
            {
                customer = await _customerRepository.GetByIdAsync(order.CustomerId, cancellationToken);
            }

            result.Add(new OrderGeneratedDto
            {
                OrderId = order.Id,
                OrderNo = order.OrderNo,
                CustomerId = order.CustomerId,
                CustomerName = customer?.Name,
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

    /// <summary>批次实体转 DTO，计算订单删除标记（已完成但无订单且非重复导入）。</summary>
    private async Task<UploadBatchDto> ToDtoAsync(UploadBatch batch, int orderCount, CancellationToken cancellationToken)
    {
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
            OrderDeleted = batch.FileType == "PDF"
                && batch.Status == UploadStatus.Completed
                && orderCount == 0
                && !(batch.ErrorMessage?.Contains("已存在") ?? false),
            CreatedAt = batch.CreatedAt
        };

        if (batch.CustomerId.HasValue)
        {
            var customer = await _customerRepository.GetByIdAsync(batch.CustomerId.Value, cancellationToken);
            dto.CustomerName = customer?.Name;
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