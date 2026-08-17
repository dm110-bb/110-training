using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrderHub.Core.Ai;

namespace OrderHub.Infrastructure.Gemini;

public class GeminiInteractionsClient : IGeminiJsonClient
{
    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _options;
    private readonly ILogger<GeminiInteractionsClient> _logger;

    public GeminiInteractionsClient(
        HttpClient httpClient,
        IOptions<GeminiOptions> options,
        ILogger<GeminiInteractionsClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> GenerateJsonAsync(
        string input,
        string responseSchemaJson,
        CancellationToken cancellationToken = default)
    {
        var apiKey = _options.ApiKey ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new AiServiceUnavailableException(
                "Gemini API key 未設定：請設定 Gemini:ApiKey 或 GEMINI_API_KEY");

        using var schema = JsonDocument.Parse(responseSchemaJson);
        var body = JsonSerializer.Serialize(new
        {
            model = _options.Model,
            input,
            response_format = new
            {
                type = "text",
                mime_type = "application/json",
                schema = schema.RootElement
            }
        });

        for (var attempt = 0; attempt <= _options.MaxRetries; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, _options.Endpoint)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            request.Headers.Add("x-goog-api-key", apiKey);

            try
            {
                using var response = await _httpClient.SendAsync(request, cancellationToken);
                var payload = await response.Content.ReadAsStringAsync(cancellationToken);

                if (response.IsSuccessStatusCode)
                    return ExtractModelOutput(payload);

                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                    throw new AiServiceUnavailableException(
                        "Gemini 拒絕存取：API key 無效或專案權限不足");

                if (!IsTransient(response.StatusCode))
                    throw new AiServiceUnavailableException(
                        $"Gemini 呼叫失敗（HTTP {(int)response.StatusCode}）");

                if (attempt == _options.MaxRetries)
                    break;

                var delay = SuggestedRetryDelay(response, payload) ?? ExponentialBackoff(attempt);
                _logger.LogWarning(
                    "Gemini 暫時失敗，{Seconds:0.##} 秒後重試（第 {Attempt}/{MaxAttempts} 次）",
                    delay.TotalSeconds,
                    attempt + 1,
                    _options.MaxRetries);
                await Task.Delay(delay, cancellationToken);
            }
            catch (HttpRequestException ex) when (attempt < _options.MaxRetries)
            {
                var delay = ExponentialBackoff(attempt);
                _logger.LogWarning(
                    ex,
                    "Gemini 網路連線失敗，{Seconds:0.##} 秒後重試（第 {Attempt}/{MaxAttempts} 次）",
                    delay.TotalSeconds,
                    attempt + 1,
                    _options.MaxRetries);
                await Task.Delay(delay, cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                throw new AiServiceUnavailableException("無法連線至 Gemini，請稍後再試", ex);
            }
        }

        throw new AiServiceUnavailableException(
            $"Gemini 重試 {_options.MaxRetries} 次後仍失敗，請稍後再試");
    }

    private static bool IsTransient(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests ||
        (int)statusCode >= 500;

    private static TimeSpan ExponentialBackoff(int attempt)
    {
        var jitterMilliseconds = Random.Shared.Next(0, 250);
        return TimeSpan.FromSeconds(Math.Pow(2, attempt)) + TimeSpan.FromMilliseconds(jitterMilliseconds);
    }

    private static TimeSpan? SuggestedRetryDelay(HttpResponseMessage response, string payload)
    {
        if (response.Headers.RetryAfter?.Delta is { } retryAfter)
            return retryAfter;

        try
        {
            using var document = JsonDocument.Parse(payload);
            if (!document.RootElement.TryGetProperty("error", out var error) ||
                !error.TryGetProperty("details", out var details))
            {
                return null;
            }

            foreach (var detail in details.EnumerateArray())
            {
                if (detail.TryGetProperty("retryDelay", out var retryDelay) &&
                    retryDelay.GetString() is { } value &&
                    value.EndsWith('s') &&
                    double.TryParse(
                        value[..^1],
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out var seconds))
                {
                    return TimeSpan.FromSeconds(seconds);
                }
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private static string ExtractModelOutput(string payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            if (document.RootElement.TryGetProperty("steps", out var steps))
            {
                foreach (var step in steps.EnumerateArray())
                {
                    if (!step.TryGetProperty("type", out var type) ||
                        type.GetString() != "model_output" ||
                        !step.TryGetProperty("content", out var content))
                    {
                        continue;
                    }

                    foreach (var part in content.EnumerateArray())
                    {
                        if (part.TryGetProperty("text", out var text) &&
                            text.GetString() is { Length: > 0 } json)
                        {
                            return json;
                        }
                    }
                }
            }
        }
        catch (JsonException ex)
        {
            throw new AiServiceUnavailableException("Gemini 回應格式無效", ex);
        }

        throw new AiServiceUnavailableException("Gemini 回應中沒有可用的 model_output");
    }
}
