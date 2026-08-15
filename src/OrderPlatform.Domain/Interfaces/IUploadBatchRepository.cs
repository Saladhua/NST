using OrderPlatform.Domain.Entities;

namespace OrderPlatform.Domain.Interfaces;

/// <summary>上传批次仓储接口。</summary>
public interface IUploadBatchRepository
{
    /// <summary>按 ID 查询批次。</summary>
    Task<UploadBatch?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>判断文件名是否已上传过（同名去重）。</summary>
    Task<bool> ExistsByFileNameAsync(string fileName, CancellationToken cancellationToken);

    /// <summary>分页查询上传批次（按创建时间倒序）。</summary>
    Task<List<UploadBatch>> ListAsync(int page, int pageSize, CancellationToken cancellationToken);

    /// <summary>统计批次总数。</summary>
    Task<int> CountAsync(CancellationToken cancellationToken);

    /// <summary>新增批次。</summary>
    Task AddAsync(UploadBatch batch, CancellationToken cancellationToken);

    /// <summary>更新批次（状态/进度/结果）。</summary>
    void Update(UploadBatch batch);

    /// <summary>保存变更。</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}