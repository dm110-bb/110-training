namespace OrderHub.Core.Ai;

public interface IOrderQueryTranslator
{
    Task<OrderSearchQuery?> TranslateAsync(
        string naturalLanguageQuery,
        CancellationToken cancellationToken = default);
}
