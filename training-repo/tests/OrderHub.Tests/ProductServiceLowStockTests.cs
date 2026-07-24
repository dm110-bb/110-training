using OrderHub.Core.Domain;
using OrderHub.Infrastructure.Data;

namespace OrderHub.Tests;

public class ProductServiceLowStockTests
{
    [Fact]
    public async Task GetLowStock_FiltersByThresholdAndSortsByStockAscending()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateProductService(db);
        TestSetup.AddProduct(db, stock: 9, sku: "SKU-009");
        TestSetup.AddProduct(db, stock: 3, sku: "SKU-003");
        TestSetup.AddProduct(db, stock: 10, sku: "SKU-010");

        var result = await service.GetLowStockAsync(10);

        Assert.True(result.Success);
        Assert.Collection(
            result.Value!,
            product => Assert.Equal(("SKU-003", 3), (product.Sku, product.StockQuantity)),
            product => Assert.Equal(("SKU-009", 9), (product.Sku, product.StockQuantity)));
    }

    [Fact]
    public async Task GetLowStock_ExcludesInactiveProducts()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateProductService(db);
        TestSetup.AddProduct(db, stock: 4, isActive: true, sku: "SKU-ACTIVE");
        TestSetup.AddProduct(db, stock: 2, isActive: false, sku: "SKU-INACTIVE");

        var result = await service.GetLowStockAsync(10);

        Assert.True(result.Success);
        Assert.Single(result.Value!);
        Assert.Equal("SKU-ACTIVE", result.Value![0].Sku);
    }

    [Fact]
    public async Task GetLowStock_SoldQuantityIncludesOnlyRelevantRecentNonCancelledOrders()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateProductService(db);
        var customer = TestSetup.AddCustomer(db);
        var lowStockProduct = TestSetup.AddProduct(db, stock: 4, sku: "SKU-LOW");
        var otherProduct = TestSetup.AddProduct(db, stock: 20, sku: "SKU-OTHER");
        var now = DateTime.UtcNow;

        AddOrder(db, customer.Id, lowStockProduct.Id, quantity: 2, OrderStatus.Confirmed, now.AddDays(-5));
        AddOrder(db, customer.Id, lowStockProduct.Id, quantity: 7, OrderStatus.Cancelled, now.AddDays(-3));
        AddOrder(db, customer.Id, lowStockProduct.Id, quantity: 11, OrderStatus.Shipped, now.AddDays(-31));
        AddOrder(db, customer.Id, otherProduct.Id, quantity: 13, OrderStatus.Shipped, now.AddDays(-1));

        var result = await service.GetLowStockAsync(10);

        Assert.True(result.Success);
        Assert.Equal(2, Assert.Single(result.Value!).SoldQuantityLast30Days);
    }

    [Fact]
    public async Task GetLowStock_NonPositiveThreshold_ReturnsValidationFailure()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateProductService(db);

        var result = await service.GetLowStockAsync(0);

        Assert.False(result.Success);
        Assert.Contains("大於 0", result.ErrorMessage);
    }

    private static void AddOrder(
        OrderHubDbContext db,
        int customerId,
        int productId,
        int quantity,
        OrderStatus status,
        DateTime createdAt)
    {
        db.Orders.Add(new Order
        {
            CustomerId = customerId,
            Status = status,
            CreatedAt = createdAt,
            Items =
            {
                new OrderItem
                {
                    ProductId = productId,
                    Quantity = quantity,
                    UnitPriceSnapshot = 100m
                }
            }
        });
        db.SaveChanges();
    }
}
