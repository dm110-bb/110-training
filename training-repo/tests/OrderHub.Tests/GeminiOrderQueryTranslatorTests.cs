using Microsoft.Extensions.Logging.Abstractions;
using OrderHub.Core.Domain;
using OrderHub.Infrastructure.Gemini;

namespace OrderHub.Tests;

public class GeminiOrderQueryTranslatorTests
{
    [Fact]
    public async Task Translate_ValidWhitelistJson_MapsTypedQueryAndCurrentDatePrompt()
    {
        var client = new StubGeminiClient("""
            {"intent":"search","status":"Cancelled","memberTier":"Gold","dateFrom":"2026-07-01","dateTo":"2026-07-31"}
            """);
        var translator = CreateTranslator(client);

        var result = await translator.TranslateAsync("上個月金卡會員取消的訂單");

        Assert.NotNull(result);
        Assert.Equal(OrderStatus.Cancelled, result.Status);
        Assert.Equal(CustomerTier.Gold, result.MemberTier);
        Assert.Equal(new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc), result.DateFrom);
        Assert.Contains("今天是 2026-08-17", client.Input);
        Assert.Contains("上個月金卡會員取消的訂單", client.Input);
        Assert.Contains("\"required\": [\"intent\"]", client.Schema);
    }

    [Theory]
    [InlineData("{\"intent\":\"unsupported\"}")]
    [InlineData("{\"intent\":\"search\",\"status\":\"99\"}")]
    [InlineData("{\"intent\":\"search\",\"dateFrom\":\"tomorrow\"}")]
    [InlineData("not-json")]
    public async Task Translate_UntrustedOrUnsupportedOutput_ReturnsNull(string output)
    {
        var translator = CreateTranslator(new StubGeminiClient(output));

        var result = await translator.TranslateAsync("任意輸入");

        Assert.Null(result);
    }

    private static GeminiOrderQueryTranslator CreateTranslator(StubGeminiClient client) =>
        new(
            client,
            new FixedTimeProvider(new DateTimeOffset(2026, 8, 17, 8, 0, 0, TimeSpan.Zero)),
            NullLogger<GeminiOrderQueryTranslator>.Instance);

    private sealed class StubGeminiClient(string output) : IGeminiJsonClient
    {
        public string Input { get; private set; } = string.Empty;
        public string Schema { get; private set; } = string.Empty;

        public Task<string> GenerateJsonAsync(
            string input,
            string responseSchemaJson,
            CancellationToken cancellationToken = default)
        {
            Input = input;
            Schema = responseSchemaJson;
            return Task.FromResult(output);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
