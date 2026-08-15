using Microsoft.EntityFrameworkCore;
using OrderPlatform.Domain.Entities;
using OrderPlatform.Domain.Interfaces;
using OrderPlatform.Infrastructure.Persistence;

namespace OrderPlatform.Infrastructure.Repositories;

/// <summary>上传批次仓储实现。</summary>
public class UploadBatchRepository : IUploadBatchRepository
{
    private readonly OrderDbContext _dbContext;

    public UploadBatchRepository(OrderDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>按 ID 查询批次。</summary>
    public Task<UploadBatch?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.UploadBatches.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    /// <summary>判断文件名是否已上传过。</summary>
    public Task<bool> ExistsByFileNameAsync(string fileName, CancellationToken cancellationToken)
    {
        return _dbContext.UploadBatches.AnyAsync(x => x.FileName == fileName, cancellationToken);
    }

    /// <summary>分页查询上传批次（按创建时间倒序）。</summary>
    public Task<List<UploadBatch>> ListAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        return _dbContext.UploadBatches
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    /// <summary>统计批次总数。</summary>
    public Task<int> CountAsync(CancellationToken cancellationToken)
    {
        return _dbContext.UploadBatches.CountAsync(cancellationToken);
    }

    /// <summary>新增批次。</summary>
    public async Task AddAsync(UploadBatch batch, CancellationToken cancellationToken)
    {
        await _dbContext.UploadBatches.AddAsync(batch, cancellationToken);
    }

    /// <summary>更新批次（状态/进度/结果）。</summary>
    public void Update(UploadBatch batch)
    {
        _dbContext.UploadBatches.Update(batch);
    }

    /// <summary>保存变更。</summary>
    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}