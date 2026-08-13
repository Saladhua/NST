using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrderPlatform.Application.Auth;
using OrderPlatform.Domain.Interfaces;
using OrderPlatform.Infrastructure.Persistence;
using OrderPlatform.Infrastructure.Repositories;
using OrderPlatform.Infrastructure.Security;

namespace OrderPlatform.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<OrderDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("Default")));

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ICustomerPartRepository, CustomerPartRepository>();
        services.AddScoped<IUploadBatchRepository, UploadBatchRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IOrderPushLogRepository, OrderPushLogRepository>();
        services.AddScoped<IConfigRepository, ConfigRepository>();
        services.AddScoped<IDashboardRepository, DashboardRepository>();
        services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
        services.AddScoped<ITokenService, JwtTokenService>();

        return services;
    }
}
