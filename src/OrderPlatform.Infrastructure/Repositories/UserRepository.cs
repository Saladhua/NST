using Microsoft.EntityFrameworkCore;
using OrderPlatform.Domain.Entities;
using OrderPlatform.Domain.Interfaces;
using OrderPlatform.Infrastructure.Persistence;

namespace OrderPlatform.Infrastructure.Repositories;

/// <summary>用户仓储实现。</summary>
public class UserRepository : IUserRepository
{
    private readonly OrderDbContext _dbContext;

    public UserRepository(OrderDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>按用户名查询用户。</summary>
    public Task<User?> GetByUserNameAsync(string userName, CancellationToken cancellationToken)
    {
        return _dbContext.Users.FirstOrDefaultAsync(x => x.UserName == userName, cancellationToken);
    }

    /// <summary>按 ID 查询用户。</summary>
    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.Users.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    /// <summary>判断用户名是否已存在。</summary>
    public Task<bool> ExistsByUserNameAsync(string userName, CancellationToken cancellationToken)
    {
        return _dbContext.Users.AnyAsync(x => x.UserName == userName, cancellationToken);
    }

    /// <summary>新增用户。</summary>
    public async Task AddAsync(User user, CancellationToken cancellationToken)
    {
        await _dbContext.Users.AddAsync(user, cancellationToken);
    }

    /// <summary>分页查询用户（按创建时间排序）。</summary>
    public Task<List<User>> ListAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        return _dbContext.Users
            .OrderBy(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    /// <summary>统计用户总数。</summary>
    public Task<int> CountAsync(CancellationToken cancellationToken)
    {
        return _dbContext.Users.CountAsync(cancellationToken);
    }

    /// <summary>更新用户。</summary>
    public void Update(User user)
    {
        _dbContext.Users.Update(user);
    }

    /// <summary>删除用户。</summary>
    public void Delete(User user)
    {
        _dbContext.Users.Remove(user);
    }

    /// <summary>保存变更。</summary>
    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}