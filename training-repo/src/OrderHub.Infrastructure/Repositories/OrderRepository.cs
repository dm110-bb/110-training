using Microsoft.EntityFrameworkCore;
using OrderHub.Core.Ai;
using OrderHub.Core.Common;
using OrderHub.Core.Domain;
using OrderHub.Core.Interfaces;
using OrderHub.Infrastructure.Data;

namespace OrderHub.Infrastructure.Repositories;

public class OrderRepository : IOrderRepository
{
    private readonly OrderHubDbContext _db;

    public OrderRepository(OrderHubDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<Order>> GetPagedAsync(int page, int pageSize, OrderStatus? status)
    {
        var query = _db.Orders
            .Include(o => o.Customer)
            .Include(o => o.Items)
            .AsQueryable();

        if (status.HasValue)
            query = query.Where(o => o.Status == status.Value);

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<Order>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public Task<Order?> GetWithDetailsAsync(int id) =>
        _db.Orders
            .Include(o => o.Customer)
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.Id == id);

    public async Task<IReadOnlyList<Order>> GetByCustomerAsync(int customerId) =>
        await _db.Orders
            .Include(o => o.Customer)
            .Include(o => o.Items)
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

    public async Task<IReadOnlyList<Order>> SearchAsync(
        OrderSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var orders = _db.Orders
            .Include(order => order.Customer)
            .Include(order => order.Items)
            .AsQueryable();

        if (query.Status.HasValue)
            orders = orders.Where(order => order.Status == query.Status.Value);
        if (query.MemberTier.HasValue)
            orders = orders.Where(order => order.Customer != null && order.Customer.Tier == query.MemberTier.Value);
        if (query.DateFrom.HasValue)
            orders = orders.Where(order => order.CreatedAt >= query.DateFrom.Value.Date);
        if (query.DateTo.HasValue)
        {
            var endExclusive = query.DateTo.Value.Date.AddDays(1);
            orders = orders.Where(order => order.CreatedAt < endExclusive);
        }

        return await orders
            .OrderByDescending(order => order.CreatedAt)
            .Take(100)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<int, int>> GetSoldQuantitiesSinceAsync(
        IReadOnlyCollection<int> productIds,
        DateTime sinceUtc)
    {
        var quantities = await _db.OrderItems
            .Where(i =>
                productIds.Contains(i.ProductId) &&
                i.Order!.CreatedAt >= sinceUtc &&
                i.Order.Status != OrderStatus.Cancelled)
            .GroupBy(i => i.ProductId)
            .Select(group => new
            {
                ProductId = group.Key,
                Quantity = group.Sum(i => i.Quantity)
            })
            .ToListAsync();

        return quantities.ToDictionary(row => row.ProductId, row => row.Quantity);
    }

    public async Task AddAsync(Order order) => await _db.Orders.AddAsync(order);

    public Task SaveChangesAsync() => _db.SaveChangesAsync();
}
