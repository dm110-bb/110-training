namespace OrderHub.Core.Services;

public sealed record LowStockProductSummary(
    string Sku,
    string Name,
    int StockQuantity,
    int SoldQuantityLast30Days);
