using OrderPlatform.Domain.Entities;

namespace OrderPlatform.Domain.Interfaces;

public interface IUploadBatchRepository
{
    Task<UploadBatch?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<List<UploadBatch>> ListAsync(int page, int pageSize, CancellationToken cancellationToken);

    Task<int> CountAsync(CancellationToken cancellationToken);

    Task AddAsync(UploadBatch batch, CancellationToken cancellationToken);

    void Update(UploadBatch batch);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
