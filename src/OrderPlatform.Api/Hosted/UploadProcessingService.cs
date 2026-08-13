using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrderPlatform.Application.Upload;

namespace OrderPlatform.Api.Hosted;

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