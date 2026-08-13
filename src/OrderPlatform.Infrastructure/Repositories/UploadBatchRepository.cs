using Microsoft.EntityFrameworkCore;
using OrderPlatform.Domain.Entities;
using OrderPlatform.Domain.Interfaces;
using OrderPlatform.Infrastructure.Persistence;

namespace OrderPlatform.Infrastructure.Repositories;

public class UploadBatchRepository : IUploadBatchRepository
{
    private readonly OrderDbContext _dbContext;

    public UploadBatchRepository(OrderDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<UploadBatch?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.UploadBatches.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task<List<UploadBatch>> ListAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        return _dbContext.UploadBatches
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountAsync(CancellationToken cancellationToken)
    {
        return _dbContext.UploadBatches.CountAsync(cancellationToken);
    }

    public async Task AddAsync(UploadBatch batch, CancellationToken cancellationToken)
    {
        await _dbContext.UploadBatches.AddAsync(batch, cancellationToken);
    }

    public void Update(UploadBatch batch)
    {
        _dbContext.UploadBatches.Update(batch);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
