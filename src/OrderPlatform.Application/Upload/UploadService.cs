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

public interface IUploadService
{
    /// <summary>接收上传：重名检查、保存文件、创建 Pending 批次并入队，立即返回批次列表。</summary>
    Task<List<UploadBatchDto>> CreateBatchesAsync(IEnumerable<IFormFile> files, Guid userId, CancellationToken cancellationToken);

    /// <summary>后台解析指定批次，更新进度与状态。</summary>
    Task ProcessBatchAsync(Guid batchId, CancellationToken cancellationToken);

    Task<UploadBatchDto> GetBatchAsync(Guid batchId, CancellationToken cancellationToken);

    Task<PagedResult<UploadBatchDto>> ListBatchesAsync(int page, int pageSize, CancellationToken cancellationToken);

    Task<ExcelBatchDetailDto> GetBatchExcelAsync(Guid batchId, CancellationToken cancellationToken);

    Task<List<CustomerImportDto>> GetCustomersAsync(CancellationToken cancellationToken);

    Task<List<OrderGeneratedDto>> GetBatchOrdersAsync(Guid batchId, CancellationToken cancellationToken);
}

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
        _jobQueue = jobQueue;
        _logger = logger;
    }

    public async Task<List<UploadBatchDto>> CreateBatchesAsync(IEnumerable<IFormFile> files, Guid userId, CancellationToken cancellationToken)
    {
        var fileList = files.Where(f => f.Length > 0).ToList();
        if (fileList.Count == 0)
        {
            throw new BusinessException("未接收到有效文件");
        }

        var group = fileList.GroupBy(f => GetFileType(f.FileName)).ToList();
        if (group.Count > 1)
        {
            throw new BusinessException("同一批上传的文件须为同一类型（全部 PDF 或全部 Excel）");
        }

        foreach (var file in fileList)
        {
            if (await _batchRepository.ExistsByFileNameAsync(file.FileName, cancellationToken))
            {
                throw new BusinessException($"文件「{file.FileName}」已上传过，请勿重复上传");
            }
        }

        var results = new List<UploadBatchDto>();
        foreach (var file in fileList)
        {
            var batch = await SaveFileAsync(file, userId, cancellationToken);
            _jobQueue.Enqueue(batch.Id);
            results.Add(await ToDtoAsync(batch, cancellationToken));
        }

        return results;
    }

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
                await ProcessExcelAsync(batch, savedPath, cancellationToken);
            }

            batch.Status = UploadStatus.Completed;
            batch.Progress = 100;
            batch.ErrorMessage = null;
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

    private async Task ProcessExcelAsync(UploadBatch batch, string savedPath, CancellationToken cancellationToken)
    {
        var result = await _excelParser.ParseAsync(savedPath, cancellationToken);
        batch.RawDataJson = JsonSerializer.Serialize(result);
        batch.Progress = 50;

        foreach (var sheet in result.Sheets)
        {
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

            // 重建该客户的图号资料
            await _partRepository.DeleteByCustomerAsync(customer.Id, cancellationToken);
            var parts = new List<CustomerPart>();
            foreach (var row in sheet.Rows)
            {
                var nest = Get(row, "NEST图号") ?? string.Empty;
                var customerPartNo = Get(row, "客户图号") ?? Get(row, "客户新图号") ?? string.Empty;
                var spec = Get(row, "规格") ?? string.Empty;
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
    }

    private async Task ProcessPdfAsync(UploadBatch batch, string savedPath, CancellationToken cancellationToken)
    {
        var pdf = await _pdfParser.ParseAsync(savedPath, cancellationToken);
        var orderNo = string.IsNullOrEmpty(pdf.OrderNo)
            ? $"ORDER-{DateTime.Now:yyyyMMddHHmmss}"
            : pdf.OrderNo;

        // 订单号去重：已存在则跳过创建
        var existingOrder = await _orderRepository.GetByOrderNoAsync(orderNo, cancellationToken);
        if (existingOrder is not null)
        {
            batch.ErrorMessage = $"订单「{orderNo}」已存在，已跳过重复导入";
            batch.RawDataJson = JsonSerializer.Serialize(pdf);
            return;
        }

        // 客户识别：解析出的客户名精确匹配，否则尝试从文件名匹配
        var customer = await FindCustomerAsync(pdf.BuyerName, batch.FileName, cancellationToken);

        var order = new OrderMain
        {
            Id = Guid.NewGuid(),
            OrderNo = orderNo,
            CustomerId = customer?.Id ?? Guid.Empty,
            OrderDate = pdf.OrderDate,
            ParseStatus = MatchStatus.Unmatched,
            PushStatus = PushStatus.NotPushed,
            PdfRawJson = JsonSerializer.Serialize(pdf),
            CreatedAt = DateTime.Now
        };

        var parts = customer is null
            ? new List<CustomerPart>()
            : await _partRepository.ListByCustomerAsync(customer.Id, cancellationToken);

        var allMatched = true;
        foreach (var row in pdf.Rows)
        {
            var (customerPartNo, nestPartNo, alloy, spray, length, status) = MatchService.Match(row, parts);
            if (status != MatchStatus.Matched)
            {
                allMatched = false;
            }

            order.Items.Add(new OrderItem
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                LineNo = row.LineNo,
                MaterialCode = row.MaterialCode,
                MaterialName = row.MaterialName,
                Spec = row.Spec,
                CustomerPartNo = customerPartNo,
                NestPartNo = nestPartNo,
                Alloy = alloy,
                Spray = spray,
                Length = length,
                Quantity = row.Quantity,
                Unit = row.Unit,
                Price = row.Price,
                Amount = row.Amount,
                ReceiveDate = row.ReceiveDate,
                MatchStatus = status,
                CreatedAt = DateTime.Now
            });
        }

        if (order.Items.Count > 0)
        {
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

        batch.CustomerId = customer?.Id;
        batch.RawDataJson = JsonSerializer.Serialize(pdf);
    }

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

    public async Task<UploadBatchDto> GetBatchAsync(Guid batchId, CancellationToken cancellationToken)
    {
        var batch = await _batchRepository.GetByIdAsync(batchId, cancellationToken)
            ?? throw new BusinessException("批次不存在");
        return await ToDtoAsync(batch, cancellationToken);
    }

    public async Task<PagedResult<UploadBatchDto>> ListBatchesAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var batches = await _batchRepository.ListAsync(page, pageSize, cancellationToken);
        var total = await _batchRepository.CountAsync(cancellationToken);
        var items = new List<UploadBatchDto>();
        foreach (var batch in batches)
        {
            items.Add(await ToDtoAsync(batch, cancellationToken));
        }

        return new PagedResult<UploadBatchDto>(items, total);
    }

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

    public async Task<List<CustomerImportDto>> GetCustomersAsync(CancellationToken cancellationToken)
    {
        var customers = await _customerRepository.ListAsync(cancellationToken);
        var counts = await _partRepository.CountGroupByCustomerAsync(customers.Select(c => c.Id), cancellationToken);
        return customers.Select(c => new CustomerImportDto
        {
            CustomerId = c.Id,
            CustomerName = c.Name,
            PartCount = counts.GetValueOrDefault(c.Id, 0)
        }).ToList();
    }

    public async Task<List<OrderGeneratedDto>> GetBatchOrdersAsync(Guid batchId, CancellationToken cancellationToken)
    {
        // 简单实现：从该批次关联文件找订单（实际订单与批次通过 SourceFileId 关联，此处提供批次解析明细）
        var batch = await _batchRepository.GetByIdAsync(batchId, cancellationToken)
            ?? throw new BusinessException("批次不存在");
        if (string.IsNullOrEmpty(batch.RawDataJson) || batch.FileType != "PDF")
        {
            return new List<OrderGeneratedDto>();
        }

        var pdf = JsonSerializer.Deserialize<PdfParseResult>(batch.RawDataJson);
        if (pdf is null)
        {
            return new List<OrderGeneratedDto>();
        }

        return new List<OrderGeneratedDto>
        {
            new()
            {
                OrderId = Guid.Empty,
                OrderNo = pdf.OrderNo,
                CustomerId = batch.CustomerId,
                ItemCount = pdf.Rows.Count,
                Items = pdf.Rows.Select(r => new MatchResultItem
                {
                    LineNo = r.LineNo,
                    MaterialCode = r.MaterialCode,
                    Spec = r.Spec,
                    Quantity = r.Quantity,
                    Unit = r.Unit
                }).ToList()
            }
        };
    }

    private async Task<UploadBatchDto> ToDtoAsync(UploadBatch batch, CancellationToken cancellationToken)
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
            CreatedAt = batch.CreatedAt
        };

        if (batch.CustomerId.HasValue)
        {
            var customer = await _customerRepository.GetByIdAsync(batch.CustomerId.Value, cancellationToken);
            dto.CustomerName = customer?.Name;
        }

        return dto;
    }

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

    private static decimal? ParseNullableDecimal(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return decimal.TryParse(text.Replace(",", string.Empty), out var v) ? v : null;
    }
}