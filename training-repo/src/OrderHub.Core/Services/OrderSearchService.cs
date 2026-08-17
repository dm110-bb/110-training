using OrderHub.Core.Ai;
using OrderHub.Core.Common;
using OrderHub.Core.Domain;
using OrderHub.Core.Interfaces;

namespace OrderHub.Core.Services;

public class OrderSearchService : IOrderSearchService
{
    private readonly IOrderQueryTranslator _translator;
    private readonly IOrderRepository _orderRepository;

    public OrderSearchService(IOrderQueryTranslator translator, IOrderRepository orderRepository)
    {
        _translator = translator;
        _orderRepository = orderRepository;
    }

    public async Task<ServiceResult<IReadOnlyList<Order>>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return ServiceResult<IReadOnlyList<Order>>.Fail("請輸入查詢內容");

        var parsed = await _translator.TranslateAsync(query, cancellationToken);
        if (parsed is null || !parsed.HasAnyFilter)
            return ServiceResult<IReadOnlyList<Order>>.Fail("無法理解的查詢");

        if (parsed.DateFrom.HasValue && parsed.DateTo.HasValue && parsed.DateFrom > parsed.DateTo)
            return ServiceResult<IReadOnlyList<Order>>.Fail("無法理解的查詢");

        var orders = await _orderRepository.SearchAsync(parsed, cancellationToken);
        return ServiceResult<IReadOnlyList<Order>>.Ok(orders);
    }
}
