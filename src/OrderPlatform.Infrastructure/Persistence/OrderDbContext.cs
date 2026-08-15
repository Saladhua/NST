using Microsoft.EntityFrameworkCore;
using OrderPlatform.Domain.Entities;
using OrderPlatform.Domain.Enums;

namespace OrderPlatform.Infrastructure.Persistence;

/// <summary>EF Core 数据库上下文，集中配置实体到表的映射。</summary>
public class OrderDbContext : DbContext
{
    public OrderDbContext(DbContextOptions<OrderDbContext> options)
        : base(options)
    {
    }

    /// <summary>系统用户表。</summary>
    public DbSet<User> Users => Set<User>();

    /// <summary>客户表。</summary>
    public DbSet<Customer> Customers => Set<Customer>();

    /// <summary>客户图号资料表。</summary>
    public DbSet<CustomerPart> CustomerParts => Set<CustomerPart>();

    /// <summary>上传批次表。</summary>
    public DbSet<UploadBatch> UploadBatches => Set<UploadBatch>();

    /// <summary>上传文件记录表。</summary>
    public DbSet<UploadFile> UploadFiles => Set<UploadFile>();

    /// <summary>订单主表。</summary>
    public DbSet<OrderMain> Orders => Set<OrderMain>();

    /// <summary>订单明细表。</summary>
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    /// <summary>订单推送日志表。</summary>
    public DbSet<OrderPushLog> OrderPushLogs => Set<OrderPushLog>();

    /// <summary>系统配置表。</summary>
    public DbSet<SysConfig> SysConfigs => Set<SysConfig>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // 用户：用户名唯一
        var user = modelBuilder.Entity<User>();
        user.ToTable("sys_user");
        user.HasKey(x => x.Id);
        user.Property(x => x.UserName).IsRequired().HasMaxLength(50);
        user.HasIndex(x => x.UserName).IsUnique();
        user.Property(x => x.PasswordHash).IsRequired().HasMaxLength(100);
        user.Property(x => x.DisplayName).IsRequired().HasMaxLength(50);
        user.Property(x => x.Phone).HasMaxLength(20);
        user.Property(x => x.Email).HasMaxLength(100);
        user.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);
        user.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);

        // 客户：名称唯一
        var customer = modelBuilder.Entity<Customer>();
        customer.ToTable("customer");
        customer.HasKey(x => x.Id);
        customer.Property(x => x.Name).IsRequired().HasMaxLength(100);
        customer.HasIndex(x => x.Name).IsUnique();
        customer.Property(x => x.Code).HasMaxLength(50);
        customer.Property(x => x.Remark).HasMaxLength(200);

        // 客户图号资料：按客户查询
        var part = modelBuilder.Entity<CustomerPart>();
        part.ToTable("customer_part");
        part.HasKey(x => x.Id);
        part.Property(x => x.NestPartNo).HasMaxLength(100);
        part.Property(x => x.CustomerPartNo).HasMaxLength(100);
        part.Property(x => x.Spray).HasMaxLength(10);
        part.Property(x => x.Alloy).HasMaxLength(50);
        part.Property(x => x.Spec).HasMaxLength(255);
        part.Property(x => x.Length).HasColumnType("decimal(18,2)");
        part.Property(x => x.Raw);
        part.HasIndex(x => x.CustomerId);

        // 上传批次：批次号唯一
        var batch = modelBuilder.Entity<UploadBatch>();
        batch.ToTable("upload_batch");
        batch.HasKey(x => x.Id);
        batch.Property(x => x.BatchNo).IsRequired().HasMaxLength(50);
        batch.HasIndex(x => x.BatchNo).IsUnique();
        batch.Property(x => x.FileType).IsRequired().HasMaxLength(10);
        batch.Property(x => x.FileName).IsRequired().HasMaxLength(255);
        batch.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        batch.Property(x => x.ErrorMessage).HasMaxLength(1000);
        batch.Property(x => x.RawDataJson);
        batch.Property(x => x.OriginalPath).IsRequired().HasMaxLength(500);
        batch.HasIndex(x => x.CustomerId);

        // 上传文件记录
        var file = modelBuilder.Entity<UploadFile>();
        file.ToTable("upload_file");
        file.HasKey(x => x.Id);
        file.Property(x => x.FileName).IsRequired().HasMaxLength(255);
        file.Property(x => x.FilePath).IsRequired().HasMaxLength(500);
        file.Property(x => x.FileType).IsRequired().HasMaxLength(10);
        file.Property(x => x.Status).IsRequired().HasMaxLength(20);
        file.HasIndex(x => x.BatchId);

        // 订单：订单号唯一，按客户/来源批次索引
        var order = modelBuilder.Entity<OrderMain>();
        order.ToTable("order_main");
        order.HasKey(x => x.Id);
        order.Property(x => x.OrderNo).IsRequired().HasMaxLength(50);
        order.HasIndex(x => x.OrderNo).IsUnique();
        order.Property(x => x.TotalQuantity).HasColumnType("decimal(18,4)");
        order.Property(x => x.TotalAmount).HasColumnType("decimal(18,2)");
        order.Property(x => x.ParseStatus).HasConversion<string>().HasMaxLength(20);
        order.Property(x => x.PushStatus).HasConversion<string>().HasMaxLength(20);
        order.Property(x => x.PdfRawJson);
        order.HasIndex(x => x.CustomerId);
        order.HasIndex(x => x.SourceFileId);

        // 订单明细：按订单索引
        var item = modelBuilder.Entity<OrderItem>();
        item.ToTable("order_item");
        item.HasKey(x => x.Id);
        item.Property(x => x.MaterialCode).HasMaxLength(100);
        item.Property(x => x.MaterialName).HasMaxLength(255);
        item.Property(x => x.Spec).HasMaxLength(255);
        item.Property(x => x.CustomerPartNo).HasMaxLength(100);
        item.Property(x => x.NestPartNo).HasMaxLength(100);
        item.Property(x => x.Alloy).HasMaxLength(50);
        item.Property(x => x.Spray).HasMaxLength(10);
        item.Property(x => x.Length).HasColumnType("decimal(18,2)");
        item.Property(x => x.Quantity).HasColumnType("decimal(18,4)");
        item.Property(x => x.Unit).HasMaxLength(20);
        item.Property(x => x.Price).HasColumnType("decimal(18,4)");
        item.Property(x => x.Amount).HasColumnType("decimal(18,2)");
        item.Property(x => x.MatchStatus).HasConversion<string>().HasMaxLength(20);
        item.HasIndex(x => x.OrderId);

        // 推送日志：按订单索引
        var log = modelBuilder.Entity<OrderPushLog>();
        log.ToTable("order_push_log");
        log.HasKey(x => x.Id);
        log.Property(x => x.Target).IsRequired().HasMaxLength(100);
        log.Property(x => x.RequestJson);
        log.Property(x => x.ResponseJson);
        log.Property(x => x.Status).IsRequired().HasMaxLength(20);
        log.Property(x => x.ErrorMessage).HasMaxLength(1000);
        log.HasIndex(x => x.OrderId);

        // 系统配置：键唯一
        var config = modelBuilder.Entity<SysConfig>();
        config.ToTable("sys_config");
        config.HasKey(x => x.Id);
        config.Property(x => x.ConfigKey).IsRequired().HasMaxLength(100);
        config.HasIndex(x => x.ConfigKey).IsUnique();
        config.Property(x => x.ConfigValue).IsRequired().HasMaxLength(500);
        config.Property(x => x.Description).HasMaxLength(200);

        base.OnModelCreating(modelBuilder);
    }
}