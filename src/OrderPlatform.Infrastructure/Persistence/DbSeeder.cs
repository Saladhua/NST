using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OrderPlatform.Application.Auth;
using OrderPlatform.Domain.Entities;
using OrderPlatform.Domain.Enums;

namespace OrderPlatform.Infrastructure.Persistence;

/// <summary>数据库种子数据：启动时创建管理员账号与示例客户图号资料。</summary>
public static class DbSeeder
{
    /// <summary>种子 JSON 中的客户图号数据行。</summary>
    private sealed class CustomerPartSeed
    {
        public string Customer { get; set; } = string.Empty;
        public string NestPartNo { get; set; } = string.Empty;
        public string CustomerPartNo { get; set; } = string.Empty;
        public string Spray { get; set; } = string.Empty;
        public string Alloy { get; set; } = string.Empty;
        public string Spec { get; set; } = string.Empty;
        public decimal? Length { get; set; }
    }

    /// <summary>执行全部种子逻辑。</summary>
    public static async Task SeedAsync(OrderDbContext dbContext)
    {
        await SeedAdminAsync(dbContext);
        await SeedCustomersAsync(dbContext);
    }

    /// <summary>写入默认管理员 admin / 123456（已存在则跳过）。</summary>
    private static async Task SeedAdminAsync(OrderDbContext dbContext)
    {
        if (await dbContext.Users.AnyAsync(x => x.UserName == "admin"))
        {
            return;
        }

        var passwordHasher = new Security.BCryptPasswordHasher();
        var admin = new User
        {
            Id = Guid.NewGuid(),
            UserName = "admin",
            PasswordHash = passwordHasher.Hash("123456"),
            DisplayName = "系统管理员",
            Role = UserRole.Admin,
            Status = UserStatus.Active,
            CreatedAt = DateTime.Now
        };

        await dbContext.Users.AddAsync(admin);
        await dbContext.SaveChangesAsync();
    }

    /// <summary>从 SeedData/customer_parts.json 导入示例客户图号资料（已有客户数据则跳过）。</summary>
    private static async Task SeedCustomersAsync(OrderDbContext dbContext)
    {
        if (await dbContext.Customers.AnyAsync())
        {
            return;
        }

        var seedFile = Path.Combine(AppContext.BaseDirectory, "SeedData", "customer_parts.json");
        if (!File.Exists(seedFile))
        {
            return;
        }

        // 解析种子 JSON，失败时静默跳过
        List<CustomerPartSeed>? seeds;
        try
        {
            seeds = JsonSerializer.Deserialize<List<CustomerPartSeed>>(
                await File.ReadAllTextAsync(seedFile),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            return;
        }

        if (seeds is null || seeds.Count == 0)
        {
            return;
        }

        // 先去重生成客户，再生成对应的图号资料
        var now = DateTime.Now;
        var customers = seeds
            .Select(x => x.Customer)
            .Distinct()
            .Select(name => new Customer
            {
                Id = Guid.NewGuid(),
                Name = name,
                CreatedAt = now
            })
            .ToList();

        await dbContext.Customers.AddRangeAsync(customers);
        await dbContext.SaveChangesAsync();

        var customerMap = customers.ToDictionary(x => x.Name, x => x.Id);
        var parts = seeds.Select(x => new CustomerPart
        {
            Id = Guid.NewGuid(),
            CustomerId = customerMap[x.Customer],
            NestPartNo = x.NestPartNo,
            CustomerPartNo = x.CustomerPartNo,
            Spray = x.Spray,
            Alloy = x.Alloy,
            Spec = x.Spec,
            Length = x.Length,
            Raw = $"{x.NestPartNo}|{x.CustomerPartNo}|{x.Spray}|{x.Alloy}|{x.Spec}",
            CreatedAt = now
        }).ToList();

        await dbContext.CustomerParts.AddRangeAsync(parts);
        await dbContext.SaveChangesAsync();
    }
}