namespace GeopoliticsGame.Web.Services.Commentary;

/// <summary>
/// LLM settings, bound from the "Ai" section. Works with any OpenAI-compatible Chat Completions
/// API (OpenAI, Groq, OpenRouter, Azure OpenAI-compatible gateways, local Ollama, ...).
/// </summary>
public sealed class AiOptions
{
    public const string SectionName = "Ai";

    /// <summary>Leave empty to use the built-in (offline) commentator.</summary>
    public string? ApiKey { get; set; }

    public string BaseUrl { get; set; } = "https://api.openai.com/v1";

    public string Model { get; set; } = "gpt-4o-mini";

    public int TimeoutSeconds { get; set; } = 8;
}
