using Microsoft.EntityFrameworkCore;
using OrderPlatform.Domain.Entities;
using OrderPlatform.Domain.Enums;

namespace OrderPlatform.Infrastructure.Persistence;

public class OrderDbContext : DbContext
{
    public OrderDbContext(DbContextOptions<OrderDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<CustomerPart> CustomerParts => Set<CustomerPart>();

    public DbSet<UploadBatch> UploadBatches => Set<UploadBatch>();

    public DbSet<UploadFile> UploadFiles => Set<UploadFile>();

    public DbSet<OrderMain> Orders => Set<OrderMain>();

    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    public DbSet<OrderPushLog> OrderPushLogs => Set<OrderPushLog>();

    public DbSet<SysConfig> SysConfigs => Set<SysConfig>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
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

        var customer = modelBuilder.Entity<Customer>();
        customer.ToTable("customer");
        customer.HasKey(x => x.Id);
        customer.Property(x => x.Name).IsRequired().HasMaxLength(100);
        customer.HasIndex(x => x.Name).IsUnique();
        customer.Property(x => x.Code).HasMaxLength(50);
        customer.Property(x => x.Remark).HasMaxLength(200);

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

        var batch = modelBuilder.Entity<UploadBatch>();
        batch.ToTable("upload_batch");
        batch.HasKey(x => x.Id);
        batch.Property(x => x.BatchNo).IsRequired().HasMaxLength(50);
        batch.HasIndex(x => x.BatchNo).IsUnique();
        batch.Property(x => x.FileType).IsRequired().HasMaxLength(10);
        batch.Property(x => x.FileName).IsRequired().HasMaxLength(255);
        batch.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        batch.Property(x => x.RawDataJson);
        batch.Property(x => x.OriginalPath).IsRequired().HasMaxLength(500);
        batch.HasIndex(x => x.CustomerId);

        var file = modelBuilder.Entity<UploadFile>();
        file.ToTable("upload_file");
        file.HasKey(x => x.Id);
        file.Property(x => x.FileName).IsRequired().HasMaxLength(255);
        file.Property(x => x.FilePath).IsRequired().HasMaxLength(500);
        file.Property(x => x.FileType).IsRequired().HasMaxLength(10);
        file.Property(x => x.Status).IsRequired().HasMaxLength(20);
        file.HasIndex(x => x.BatchId);

        var order = modelBuilder.Entity<OrderMain>();
        order.ToTable("order_main");
        order.HasKey(x => x.Id);
        order.Property(x => x.OrderNo).IsRequired().HasMaxLength(50);
        order.HasIndex(x => x.OrderNo);
        order.Property(x => x.TotalQuantity).HasColumnType("decimal(18,4)");
        order.Property(x => x.TotalAmount).HasColumnType("decimal(18,2)");
        order.Property(x => x.ParseStatus).HasConversion<string>().HasMaxLength(20);
        order.Property(x => x.PushStatus).HasConversion<string>().HasMaxLength(20);
        order.Property(x => x.PdfRawJson);
        order.HasIndex(x => x.CustomerId);
        order.HasIndex(x => x.SourceFileId);

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

        var log = modelBuilder.Entity<OrderPushLog>();
        log.ToTable("order_push_log");
        log.HasKey(x => x.Id);
        log.Property(x => x.Target).IsRequired().HasMaxLength(100);
        log.Property(x => x.RequestJson);
        log.Property(x => x.ResponseJson);
        log.Property(x => x.Status).IsRequired().HasMaxLength(20);
        log.Property(x => x.ErrorMessage).HasMaxLength(1000);
        log.HasIndex(x => x.OrderId);

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
