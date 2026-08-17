namespace OrderHub.Infrastructure.Gemini;

public class GeminiOptions
{
    public const string SectionName = "Gemini";

    public string? ApiKey { get; set; }
    public string Model { get; set; } = "gemini-3.5-flash";
    public string Endpoint { get; set; } = "https://generativelanguage.googleapis.com/v1beta/interactions";
    public int MaxRetries { get; set; } = 4;
}
