namespace GeopoliticsGame.Web.Services.Commentary;


public sealed class AiOptions
{
    public const string SectionName = "Ai";


    public string? ApiKey { get; set; }

    public string BaseUrl { get; set; } = "https://api.openai.com/v1";

    public string Model { get; set; } = "gpt-4o-mini";

    public int TimeoutSeconds { get; set; } = 8;
}
