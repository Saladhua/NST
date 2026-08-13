using AutoMapper;
using OrderPlatform.Application.Upload.Dtos;
using OrderPlatform.Domain.Entities;
using OrderPlatform.Domain.Enums;
using OrderPlatform.Domain.Interfaces;
using OrderPlatform.Shared.Api;

namespace OrderPlatform.Application.Orders;

public interface IOrderService
{
    Task<PagedResult<OrderListDto>> ListAsync(int page, int pageSize, string? keyword, Guid? customerId, PushStatus? pushStatus, MatchStatus? parseStatus, CancellationToken cancellationToken);

    Task<OrderDetailDto> GetDetailAsync(Guid id, CancellationToken cancellationToken);

    Task<PushResultDto> PushAsync(Guid orderId, CancellationToken cancellationToken);
}

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
}
