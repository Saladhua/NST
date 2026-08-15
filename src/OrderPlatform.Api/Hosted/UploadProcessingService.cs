using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrderPlatform.Application.Upload;

namespace OrderPlatform.Api.Hosted;

/// <summary>
/// 上传解析后台服务：从任务队列中不断取出批次 ID 并调用上传服务解析。
/// 通过独立作用域获取 IUploadService，避免与请求作用域冲突。
/// </summary>
public class UploadProcessingService : BackgroundService
{
    private readonly IUploadJobQueue _jobQueue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<UploadProcessingService> _logger;

    public UploadProcessingService(
        IUploadJobQueue jobQueue,
        IServiceScopeFactory scopeFactory,
        ILogger<UploadProcessingService> logger)
    {
        _jobQueue = jobQueue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("上传解析服务已启动");

        // 循环消费队列，直到应用停止
        while (!stoppingToken.IsCancellationRequested)
        {
            var batchId = await _jobQueue.DequeueAsync(stoppingToken);
            using var scope = _scopeFactory.CreateScope();
            var uploadService = scope.ServiceProvider.GetRequiredService<IUploadService>();
            try
            {
                await uploadService.ProcessBatchAsync(batchId, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理上传批次 {BatchId} 失败", batchId);
            }
        }
    }
}