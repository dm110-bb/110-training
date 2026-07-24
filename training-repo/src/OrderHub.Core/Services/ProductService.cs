using OrderHub.Core.Common;
using OrderHub.Core.Domain;
using OrderHub.Core.Interfaces;

namespace OrderHub.Core.Services;

public class ProductService : IProductService
{
    private readonly IProductRepository _productRepository;
    private readonly IOrderRepository _orderRepository;

    public ProductService(
        IProductRepository productRepository,
        IOrderRepository orderRepository)
    {
        _productRepository = productRepository;
        _orderRepository = orderRepository;
    }

    public Task<IReadOnlyList<Product>> GetAllAsync() => _productRepository.GetAllAsync();

    public Task<IReadOnlyList<Product>> GetActiveAsync() => _productRepository.GetActiveAsync();

    public async Task<ServiceResult<IReadOnlyList<LowStockProductSummary>>> GetLowStockAsync(int threshold)
    {
        if (threshold <= 0)
            return ServiceResult<IReadOnlyList<LowStockProductSummary>>.Fail("庫存門檻必須大於 0");

        var products = await _productRepository.GetActiveBelowStockAsync(threshold);
        if (products.Count == 0)
            return ServiceResult<IReadOnlyList<LowStockProductSummary>>.Ok(
                Array.Empty<LowStockProductSummary>());

        var soldSinceUtc = DateTime.UtcNow.AddDays(-30);
        var productIds = products.Select(p => p.Id).ToArray();
        var soldByProduct = await _orderRepository.GetSoldQuantitiesSinceAsync(productIds, soldSinceUtc);
        var summaries = products
            .Select(p => new LowStockProductSummary(
                p.Sku,
                p.Name,
                p.StockQuantity,
                soldByProduct.GetValueOrDefault(p.Id)))
            .ToList();

        return ServiceResult<IReadOnlyList<LowStockProductSummary>>.Ok(summaries);
    }
}
