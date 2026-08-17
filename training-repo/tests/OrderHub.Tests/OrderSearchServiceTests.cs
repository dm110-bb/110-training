using OrderHub.Core.Ai;
using OrderHub.Core.Domain;
using OrderHub.Core.Services;
using OrderHub.Infrastructure.Repositories;

namespace OrderHub.Tests;

public class OrderSearchServiceTests
{
    [Fact]
    public async Task Search_BlankQuery_ReturnsValidationErrorWithoutCallingTranslator()
    {
        using var db = TestSetup.CreateContext();
        var translator = new StubTranslator(new OrderSearchQuery { Status = OrderStatus.Pending });
        var service = new OrderSearchService(translator, new OrderRepository(db));

        var result = await service.SearchAsync("   ");

        Assert.False(result.Success);
        Assert.Contains("請輸入查詢內容", result.Errors);
        Assert.False(translator.WasCalled);
    }

    [Fact]
    public async Task Search_UnsupportedOrFilterlessQuery_ReturnsUnprocessableError()
    {
        using var db = TestSetup.CreateContext();
        var repository = new OrderRepository(db);

        var unsupported = await new OrderSearchService(new StubTranslator(null), repository)
            .SearchAsync("刪除所有訂單");
        var filterless = await new OrderSearchService(new StubTranslator(new OrderSearchQuery()), repository)
            .SearchAsync("顯示訂單");

        Assert.False(unsupported.Success);
        Assert.Contains("無法理解的查詢", unsupported.Errors);
        Assert.False(filterless.Success);
        Assert.Contains("無法理解的查詢", filterless.Errors);
    }

    [Fact]
    public async Task Search_ReversedDateRange_ReturnsUnprocessableError()
    {
        using var db = TestSetup.CreateContext();
        var query = new OrderSearchQuery
        {
            DateFrom = new DateTime(2026, 8, 10),
            DateTo = new DateTime(2026, 8, 1)
        };
        var service = new OrderSearchService(new StubTranslator(query), new OrderRepository(db));

        var result = await service.SearchAsync("八月的訂單");

        Assert.False(result.Success);
        Assert.Contains("無法理解的查詢", result.Errors);
    }

    [Fact]
    public async Task Search_ValidFilters_ReturnsOnlyMatchingOrders()
    {
        using var db = TestSetup.CreateContext();
        var gold = TestSetup.AddCustomer(db, CustomerTier.Gold, "金卡客戶");
        var silver = TestSetup.AddCustomer(db, CustomerTier.Silver, "銀卡客戶");
        db.Orders.AddRange(
            new Order { CustomerId = gold.Id, Status = OrderStatus.Cancelled, CreatedAt = new DateTime(2026, 7, 15) },
            new Order { CustomerId = gold.Id, Status = OrderStatus.Shipped, CreatedAt = new DateTime(2026, 7, 15) },
            new Order { CustomerId = silver.Id, Status = OrderStatus.Cancelled, CreatedAt = new DateTime(2026, 7, 15) });
        db.SaveChanges();
        var query = new OrderSearchQuery
        {
            Status = OrderStatus.Cancelled,
            MemberTier = CustomerTier.Gold,
            DateFrom = new DateTime(2026, 7, 1),
            DateTo = new DateTime(2026, 7, 31)
        };
        var service = new OrderSearchService(new StubTranslator(query), new OrderRepository(db));

        var result = await service.SearchAsync("上個月金卡會員取消的訂單");

        Assert.True(result.Success);
        var order = Assert.Single(result.Value!);
        Assert.Equal(gold.Id, order.CustomerId);
        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.Equal("金卡客戶", order.Customer!.Name);
    }

    private sealed class StubTranslator(OrderSearchQuery? result) : IOrderQueryTranslator
    {
        public bool WasCalled { get; private set; }

        public Task<OrderSearchQuery?> TranslateAsync(
            string naturalLanguageQuery,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            return Task.FromResult(result);
        }
    }
}
