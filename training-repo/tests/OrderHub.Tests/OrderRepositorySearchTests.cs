using OrderHub.Core.Ai;
using OrderHub.Core.Domain;
using OrderHub.Infrastructure.Repositories;

namespace OrderHub.Tests;

public class OrderRepositorySearchTests
{
    [Fact]
    public async Task Search_DateToIncludesTheEntireDay()
    {
        using var db = TestSetup.CreateContext();
        var customer = TestSetup.AddCustomer(db);
        db.Orders.AddRange(
            new Order { CustomerId = customer.Id, Status = OrderStatus.Pending, CreatedAt = new DateTime(2026, 8, 17, 23, 59, 59, DateTimeKind.Utc) },
            new Order { CustomerId = customer.Id, Status = OrderStatus.Pending, CreatedAt = new DateTime(2026, 8, 18, 0, 0, 0, DateTimeKind.Utc) });
        db.SaveChanges();

        var results = await new OrderRepository(db).SearchAsync(new OrderSearchQuery
        {
            DateTo = new DateTime(2026, 8, 17, 0, 0, 0, DateTimeKind.Utc)
        });

        var order = Assert.Single(results);
        Assert.Equal(new DateTime(2026, 8, 17, 23, 59, 59, DateTimeKind.Utc), order.CreatedAt);
    }

    [Fact]
    public async Task Search_ReturnsNewestHundredOrdersAtMost()
    {
        using var db = TestSetup.CreateContext();
        var customer = TestSetup.AddCustomer(db);
        var start = new DateTime(2026, 8, 17, 12, 0, 0, DateTimeKind.Utc);
        for (var index = 0; index < 105; index++)
        {
            db.Orders.Add(new Order
            {
                CustomerId = customer.Id,
                Status = OrderStatus.Pending,
                CreatedAt = start.AddMinutes(-index)
            });
        }
        db.SaveChanges();

        var results = await new OrderRepository(db).SearchAsync(new OrderSearchQuery
        {
            Status = OrderStatus.Pending
        });

        Assert.Equal(100, results.Count);
        Assert.Equal(start, results[0].CreatedAt);
        Assert.Equal(start.AddMinutes(-99), results[^1].CreatedAt);
    }
}
