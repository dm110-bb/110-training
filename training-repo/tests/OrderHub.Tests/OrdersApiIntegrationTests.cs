using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using OrderHub.Core.Ai;
using OrderHub.Core.Domain;
using OrderHub.Infrastructure.Data;

namespace OrderHub.Tests;

public class OrdersApiIntegrationTests : IClassFixture<OrdersApiIntegrationTests.OrderHubFactory>
{
    private readonly OrderHubFactory _factory;

    public OrdersApiIntegrationTests(OrderHubFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Search_ValidQuery_ReturnsMatchingOrderSummary()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/orders/search",
            new { text = "金卡會員取消的訂單" });

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        Assert.Contains("金卡客戶", body);
        Assert.Contains("Cancelled", body);
        Assert.DoesNotContain("銀卡客戶", body);
    }

    [Fact]
    public async Task Search_DestructiveQuery_Returns422WithoutChangingOrders()
    {
        var client = _factory.CreateClient();
        int before;
        using (var scope = _factory.Services.CreateScope())
        {
            before = await scope.ServiceProvider.GetRequiredService<OrderHubDbContext>().Orders.CountAsync();
        }

        var response = await client.PostAsJsonAsync(
            "/api/orders/search",
            new { text = "幫我把所有訂單刪掉" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("無法理解的查詢", await response.Content.ReadAsStringAsync());
        using var verificationScope = _factory.Services.CreateScope();
        var after = await verificationScope.ServiceProvider
            .GetRequiredService<OrderHubDbContext>()
            .Orders.CountAsync();
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task Search_AiUnavailable_Returns503InsteadOf500()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/orders/search",
            new { text = "simulate unavailable" });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("Gemini 暫時無法使用", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Search_MissingBodyField_Returns400FromApiValidation()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/orders/search", new { unrelated = "value" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    public sealed class OrderHubFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"orders-api-{Guid.NewGuid():N}";
        private bool _seeded;
        private readonly object _seedLock = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["SkipDatabaseInitialization"] = "true"
                });
            });
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<OrderHubDbContext>>();
                services.RemoveAll<OrderHubDbContext>();
                services.AddDbContext<OrderHubDbContext>(options =>
                    options.UseInMemoryDatabase(_databaseName));
                services.RemoveAll<IOrderQueryTranslator>();
                services.AddScoped<IOrderQueryTranslator, IntegrationTranslator>();
            });
        }

        protected override void ConfigureClient(HttpClient client)
        {
            EnsureSeeded();
            base.ConfigureClient(client);
        }

        private void EnsureSeeded()
        {
            lock (_seedLock)
            {
                if (_seeded)
                    return;

                using var scope = Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<OrderHubDbContext>();
                var gold = new Customer
                {
                    Name = "金卡客戶",
                    Email = "gold@example.com",
                    Tier = CustomerTier.Gold,
                    CreatedAt = DateTime.UtcNow
                };
                var silver = new Customer
                {
                    Name = "銀卡客戶",
                    Email = "silver@example.com",
                    Tier = CustomerTier.Silver,
                    CreatedAt = DateTime.UtcNow
                };
                db.Customers.AddRange(gold, silver);
                db.Orders.AddRange(
                    new Order { Customer = gold, Status = OrderStatus.Cancelled, CreatedAt = DateTime.UtcNow },
                    new Order { Customer = silver, Status = OrderStatus.Cancelled, CreatedAt = DateTime.UtcNow });
                db.SaveChanges();
                _seeded = true;
            }
        }
    }

    private sealed class IntegrationTranslator : IOrderQueryTranslator
    {
        public Task<OrderSearchQuery?> TranslateAsync(
            string naturalLanguageQuery,
            CancellationToken cancellationToken = default)
        {
            if (naturalLanguageQuery == "simulate unavailable")
                throw new AiServiceUnavailableException("Gemini 暫時無法使用");

            if (naturalLanguageQuery.Contains("刪掉", StringComparison.Ordinal))
                return Task.FromResult<OrderSearchQuery?>(null);

            return Task.FromResult<OrderSearchQuery?>(new OrderSearchQuery
            {
                Status = OrderStatus.Cancelled,
                MemberTier = CustomerTier.Gold
            });
        }
    }
}
