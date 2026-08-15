using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrderPlatform.Application.Auth;
using OrderPlatform.Domain.Interfaces;
using OrderPlatform.Infrastructure.Persistence;
using OrderPlatform.Infrastructure.Repositories;
using OrderPlatform.Infrastructure.Security;

namespace OrderPlatform.Infrastructure;

/// <summary>基础设施层依赖注入注册：DbContext、仓储、安全服务。</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // 注册 EF Core 数据库上下文（SQL Server）
        services.AddDbContext<OrderDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("Default")));

        // 读取 JWT 配置节
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

        // 注册全部仓储
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ICustomerPartRepository, CustomerPartRepository>();
        services.AddScoped<IUploadBatchRepository, UploadBatchRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IOrderPushLogRepository, OrderPushLogRepository>();
        services.AddScoped<IConfigRepository, ConfigRepository>();
        services.AddScoped<IDashboardRepository, DashboardRepository>();

        // 注册安全服务
        services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
        services.AddScoped<ITokenService, JwtTokenService>();

        return services;
    }
}