using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrderPlatform.Application.Auth;
using OrderPlatform.Application.Config;
using OrderPlatform.Application.Dashboard;
using OrderPlatform.Application.Orders;
using OrderPlatform.Application.Parsers;
using OrderPlatform.Application.Upload;
using OrderPlatform.Application.Users;

namespace OrderPlatform.Application;

/// <summary>应用层依赖注入注册：服务、解析器、校验器、AutoMapper、任务队列、ERP 推送客户端。</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        // 自动注册程序集内全部 AutoMapper Profile 与 FluentValidation 校验器
        services.AddAutoMapper(Assembly.GetExecutingAssembly());
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IPdfParser, PdfParser>();
        services.AddScoped<IExcelParser, ExcelParser>();
        services.AddScoped<IExcelOrderParser, ExcelOrderParser>();
        services.AddSingleton<IOcrOrderParser, OcrOrderParser>();
        services.AddScoped<IUploadService, UploadService>();
        services.AddSingleton<IUploadJobQueue, UploadJobQueue>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IConfigService, ConfigService>();
        services.AddScoped<IDashboardService, DashboardService>();

        // 注册 ERP 推送客户端（BaseAddress 指向 ERP 接口根路径）
        var erpBaseUrl = configuration["Erp:BaseUrl"]?.TrimEnd('/');
        if (!string.IsNullOrWhiteSpace(erpBaseUrl))
        {
            services.AddHttpClient<IErpPushService, ErpPushService>(client =>
            {
                client.BaseAddress = new Uri(erpBaseUrl + "/");
                client.Timeout = TimeSpan.FromSeconds(30);
            });
        }

        return services;
    }
}