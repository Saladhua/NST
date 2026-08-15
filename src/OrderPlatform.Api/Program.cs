using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using OrderPlatform.Application;
using OrderPlatform.Infrastructure;
using OrderPlatform.Infrastructure.Persistence;
using OrderPlatform.Infrastructure.Security;
using Serilog;

// 应用启动入口：完成日志、配置、依赖注入、中间件装配。
var builder = WebApplication.CreateBuilder(args);

// 配置 Serilog：控制台 + 文件（按天滚动）
var logDirectory = builder.Configuration["Logging:LogDirectory"] ?? "logs";
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File(Path.Combine(logDirectory, "log-.txt"), rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// 读取 JWT 配置
var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

// 注册基础设施层（EF Core、仓储、安全服务）与应用层（业务服务、解析器、任务队列）
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();

// 后台服务：消费上传任务队列，异步解析上传批次
builder.Services.AddHostedService<OrderPlatform.Api.Hosted.UploadProcessingService>();

// MVC：统一返回过滤器 + 全局异常过滤器 + 枚举转字符串序列化
builder.Services.AddControllers(options =>
{
    options.Filters.Add<OrderPlatform.Api.Filters.ApiResponseFilter>();
    options.Filters.Add<OrderPlatform.Api.Filters.GlobalExceptionFilter>();
}).ConfigureApiBehaviorOptions(options =>
{
    options.SuppressModelStateInvalidFilter = true;
}).AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// JWT Bearer 认证
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = jwtOptions.BuildTokenValidationParameters();
    });

builder.Services.AddAuthorization();

// Swagger（含 Bearer 认证配置）
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "OrderPlatform API",
        Version = "v1",
        Description = "耐世拓订单平台（技术验证版）"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "请输入登录接口返回的 AccessToken"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// 跨域（技术验证版放开所有来源）
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

var app = builder.Build();

// 启动时自动迁移数据库并写入种子数据（管理员、示例客户图号）
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
    dbContext.Database.Migrate();
    await DbSeeder.SeedAsync(dbContext);
}

// 请求日志、跨域
app.UseSerilogRequestLogging();
app.UseCors();

// 仅开发环境启用 Swagger
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// 认证与授权中间件
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();