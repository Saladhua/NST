using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using OrderPlatform.Application.Auth;
using OrderPlatform.Application.Config;
using OrderPlatform.Application.Dashboard;
using OrderPlatform.Application.Orders;
using OrderPlatform.Application.Parsers;
using OrderPlatform.Application.Upload;
using OrderPlatform.Application.Users;

namespace OrderPlatform.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddAutoMapper(Assembly.GetExecutingAssembly());
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IPdfParser, PdfParser>();
        services.AddScoped<IExcelParser, ExcelParser>();
        services.AddScoped<IUploadService, UploadService>();
        services.AddSingleton<IUploadJobQueue, UploadJobQueue>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IConfigService, ConfigService>();
        services.AddScoped<IDashboardService, DashboardService>();
        return services;
    }
}
