using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OrderHub.Core.Ai;
using OrderHub.Infrastructure.Gemini;

namespace OrderHub.Tests;

public class GeminiInteractionsClientTests
{
    [Fact]
    public async Task GenerateJson_PostsStructuredRequestAndExtractsModelOutput()
    {
        string? requestUri = null;
        string? apiKey = null;
        string? requestBody = null;
        var handler = new DelegateHandler(async request =>
        {
            requestUri = request.RequestUri?.ToString();
            apiKey = request.Headers.GetValues("x-goog-api-key").Single();
            requestBody = await request.Content!.ReadAsStringAsync();
            return JsonResponse(HttpStatusCode.OK, """
                {"status":"completed","steps":[{"type":"model_output","content":[{"type":"text","text":"{\"intent\":\"search\",\"status\":\"Pending\"}"}]}]}
                """);
        });
        var client = CreateClient(handler, "test-key");

        var result = await client.GenerateJsonAsync("query", "{\"type\":\"object\"}");

        Assert.Equal("{\"intent\":\"search\",\"status\":\"Pending\"}", result);
        Assert.Equal("https://generativelanguage.googleapis.com/v1beta/interactions", requestUri);
        Assert.Equal("test-key", apiKey);
        Assert.Contains("\"response_format\"", requestBody);
        Assert.Contains("\"mime_type\":\"application/json\"", requestBody);
    }

    [Fact]
    public async Task GenerateJson_MissingApiKey_ThrowsClearUnavailableError()
    {
        var original = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        Environment.SetEnvironmentVariable("GEMINI_API_KEY", null);
        try
        {
            var client = CreateClient(new DelegateHandler(_ => throw new InvalidOperationException()), null);

            var exception = await Assert.ThrowsAsync<AiServiceUnavailableException>(
                () => client.GenerateJsonAsync("query", "{\"type\":\"object\"}"));

            Assert.Contains("API key 未設定", exception.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GEMINI_API_KEY", original);
        }
    }

    [Fact]
    public async Task GenerateJson_Unauthorized_DoesNotRetry()
    {
        var calls = 0;
        var handler = new DelegateHandler(_ =>
        {
            calls++;
            return Task.FromResult(JsonResponse(HttpStatusCode.Unauthorized, "{}"));
        });
        var client = CreateClient(handler, "bad-key", maxRetries: 4);

        var exception = await Assert.ThrowsAsync<AiServiceUnavailableException>(
            () => client.GenerateJsonAsync("query", "{\"type\":\"object\"}"));

        Assert.Equal(1, calls);
        Assert.Contains("拒絕存取", exception.Message);
    }

    private static GeminiInteractionsClient CreateClient(
        HttpMessageHandler handler,
        string? apiKey,
        int maxRetries = 0) =>
        new(
            new HttpClient(handler),
            Options.Create(new GeminiOptions { ApiKey = apiKey, MaxRetries = maxRetries }),
            NullLogger<GeminiInteractionsClient>.Instance);

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string body) =>
        new(statusCode) { Content = new StringContent(body) };

    private sealed class DelegateHandler(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => handler(request);
    }
}
