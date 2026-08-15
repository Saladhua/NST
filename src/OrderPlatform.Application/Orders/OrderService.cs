using AutoMapper;
using OrderPlatform.Application.Upload.Dtos;
using OrderPlatform.Domain.Entities;
using OrderPlatform.Domain.Enums;
using OrderPlatform.Domain.Interfaces;
using OrderPlatform.Shared.Api;

namespace OrderPlatform.Application.Orders;

/// <summary>订单服务接口：订单列表、详情、推送、删除。</summary>
public interface IOrderService
{
    /// <summary>分页查询订单列表（支持关键词/客户/推送状态/关联状态筛选）。</summary>
    Task<PagedResult<OrderListDto>> ListAsync(int page, int pageSize, string? keyword, Guid? customerId, PushStatus? pushStatus, MatchStatus? parseStatus, CancellationToken cancellationToken);

    /// <summary>查询订单详情（含明细，按行号排序）。</summary>
    Task<OrderDetailDto> GetDetailAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>推送订单（技术验证版为模拟推送，记录推送日志）。</summary>
    Task<PushResultDto> PushAsync(Guid orderId, CancellationToken cancellationToken);

    /// <summary>删除订单（已推送的订单不可删除）。</summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}

/// <summary>订单服务实现。</summary>
public class OrderService : IOrderService
{
    private readonly IOrderRepository _orderRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IOrderPushLogRepository _pushLogRepository;
    private readonly IMapper _mapper;

    public OrderService(
        IOrderRepository orderRepository,
        ICustomerRepository customerRepository,
        IOrderPushLogRepository pushLogRepository,
        IMapper mapper)
    {
        _orderRepository = orderRepository;
        _customerRepository = customerRepository;
        _pushLogRepository = pushLogRepository;
        _mapper = mapper;
    }

    /// <summary>分页查询订单并补充客户名称。</summary>
    public async Task<PagedResult<OrderListDto>> ListAsync(
        int page,
        int pageSize,
        string? keyword,
        Guid? customerId,
        PushStatus? pushStatus,
        MatchStatus? parseStatus,
        CancellationToken cancellationToken)
    {
        var items = await _orderRepository.QueryAsync(page, pageSize, keyword, customerId, pushStatus, parseStatus, cancellationToken);
        var total = await _orderRepository.CountAsync(keyword, customerId, pushStatus, parseStatus, cancellationToken);

        var customers = await _customerRepository.ListAsync(cancellationToken);
        var customerMap = customers.ToDictionary(c => c.Id, c => c.Name);

        var list = items.Select(o => new OrderListDto
        {
            Id = o.Id,
            OrderNo = o.OrderNo,
            CustomerId = o.CustomerId,
            CustomerName = customerMap.TryGetValue(o.CustomerId, out var name) ? name : string.Empty,
            OrderDate = o.OrderDate,
            TotalQuantity = o.TotalQuantity,
            TotalAmount = o.TotalAmount,
            ParseStatus = o.ParseStatus,
            PushStatus = o.PushStatus,
            CreatedAt = o.CreatedAt
        }).ToList();

        return new PagedResult<OrderListDto>(list, total);
    }

    /// <summary>查询订单详情，包含明细行与客户名称。</summary>
    public async Task<OrderDetailDto> GetDetailAsync(Guid id, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new BusinessException("订单不存在");

        var customer = await _customerRepository.GetByIdAsync(order.CustomerId, cancellationToken);

        var dto = new OrderDetailDto
        {
            Id = order.Id,
            OrderNo = order.OrderNo,
            CustomerId = order.CustomerId,
            CustomerName = customer?.Name ?? string.Empty,
            OrderDate = order.OrderDate,
            TotalQuantity = order.TotalQuantity,
            TotalAmount = order.TotalAmount,
            ParseStatus = order.ParseStatus,
            PushStatus = order.PushStatus,
            SourceFileId = order.SourceFileId,
            CreatedAt = order.CreatedAt,
            Items = order.Items.OrderBy(i => i.LineNo).Select(i => new OrderItemDto
            {
                Id = i.Id,
                LineNo = i.LineNo,
                MaterialCode = i.MaterialCode,
                MaterialName = i.MaterialName,
                Spec = i.Spec,
                CustomerPartNo = i.CustomerPartNo,
                NestPartNo = i.NestPartNo,
                Alloy = i.Alloy,
                Spray = i.Spray,
                Length = i.Length,
                Quantity = i.Quantity,
                Unit = i.Unit,
                Price = i.Price,
                Amount = i.Amount,
                ReceiveDate = i.ReceiveDate,
                MatchStatus = i.MatchStatus
            }).ToList()
        };

        return dto;
    }

    /// <summary>推送订单：技术验证版直接标记为成功并写入推送日志。</summary>
    public async Task<PushResultDto> PushAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdAsync(orderId, cancellationToken)
            ?? throw new BusinessException("订单不存在");

        var requestJson = System.Text.Json.JsonSerializer.Serialize(order);

        var log = new OrderPushLog
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            Target = "OrderPlatform",
            RequestJson = requestJson,
            Status = "Success",
            PushTime = DateTime.Now,
            ResponseJson = "{\"result\":\"ok\"}"
        };

        order.PushStatus = PushStatus.Pushed;
        _orderRepository.Update(order);
        await _pushLogRepository.AddAsync(log, cancellationToken);
        await _pushLogRepository.SaveChangesAsync(cancellationToken);

        return new PushResultDto
        {
            LogId = log.Id,
            OrderId = order.Id,
            Status = "Success",
            PushTime = log.PushTime
        };
    }

    /// <summary>删除订单：已推送的订单禁止删除。</summary>
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new BusinessException("订单不存在");

        if (order.PushStatus == PushStatus.Pushed)
        {
            throw new BusinessException("已推送的订单不可删除");
        }

        await _orderRepository.DeleteAsync(id, cancellationToken);
    }
}