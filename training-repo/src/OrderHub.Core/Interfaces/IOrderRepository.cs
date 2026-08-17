using OrderHub.Core.Ai;
using OrderHub.Core.Common;
using OrderHub.Core.Domain;

namespace OrderHub.Core.Interfaces;

public interface IOrderRepository
{
    Task<PagedResult<Order>> GetPagedAsync(int page, int pageSize, OrderStatus? status);
    Task<Order?> GetWithDetailsAsync(int id);
    Task<IReadOnlyList<Order>> GetByCustomerAsync(int customerId);
    Task<IReadOnlyList<Order>> SearchAsync(
        OrderSearchQuery query,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<int, int>> GetSoldQuantitiesSinceAsync(
        IReadOnlyCollection<int> productIds,
        DateTime sinceUtc);
    Task AddAsync(Order order);
    Task SaveChangesAsync();
}
