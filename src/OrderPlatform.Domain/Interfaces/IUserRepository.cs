using OrderPlatform.Domain.Entities;

namespace OrderPlatform.Domain.Interfaces;

/// <summary>用户仓储接口，定义用户数据的持久化操作。</summary>
public interface IUserRepository
{
    /// <summary>按用户名查询用户。</summary>
    Task<User?> GetByUserNameAsync(string userName, CancellationToken cancellationToken);

    /// <summary>按 ID 查询用户。</summary>
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>判断用户名是否已存在。</summary>
    Task<bool> ExistsByUserNameAsync(string userName, CancellationToken cancellationToken);

    /// <summary>新增用户。</summary>
    Task AddAsync(User user, CancellationToken cancellationToken);

    /// <summary>分页查询用户。</summary>
    Task<List<User>> ListAsync(int page, int pageSize, CancellationToken cancellationToken);

    /// <summary>统计用户总数。</summary>
    Task<int> CountAsync(CancellationToken cancellationToken);

    /// <summary>更新用户。</summary>
    void Update(User user);

    /// <summary>删除用户。</summary>
    void Delete(User user);

    /// <summary>保存变更。</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}