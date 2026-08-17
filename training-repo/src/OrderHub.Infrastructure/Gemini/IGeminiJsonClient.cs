namespace OrderHub.Infrastructure.Gemini;

public interface IGeminiJsonClient
{
    Task<string> GenerateJsonAsync(
        string input,
        string responseSchemaJson,
        CancellationToken cancellationToken = default);
}
