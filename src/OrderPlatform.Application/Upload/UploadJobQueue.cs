using System.Threading.Channels;

namespace OrderPlatform.Application.Upload;

/// <summary>上传解析任务队列接口：生产者入队批次 ID，后台服务消费。</summary>
public interface IUploadJobQueue
{
    /// <summary>将批次 ID 加入队列。</summary>
    void Enqueue(Guid batchId);

    /// <summary>异步取出一个批次 ID（队列为空时阻塞等待）。</summary>
    ValueTask<Guid> DequeueAsync(CancellationToken cancellationToken);
}

/// <summary>基于 System.Threading.Channels 的无界内存任务队列。</summary>
public class UploadJobQueue : IUploadJobQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>();

    public void Enqueue(Guid batchId)
    {
        _channel.Writer.TryWrite(batchId);
    }

    public ValueTask<Guid> DequeueAsync(CancellationToken cancellationToken)
    {
        return _channel.Reader.ReadAsync(cancellationToken);
    }
}