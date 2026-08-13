using System.Threading.Channels;

namespace OrderPlatform.Application.Upload;

public interface IUploadJobQueue
{
    void Enqueue(Guid batchId);

    ValueTask<Guid> DequeueAsync(CancellationToken cancellationToken);
}

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