using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using GeopoliticsGame.Core.Abstractions;
using Microsoft.Extensions.Options;

namespace GeopoliticsGame.Web.Services.Commentary;


public sealed class LlmCommentaryService : ICommentaryService
{
    private const string SystemPrompt =
        "You are a dramatic, witty war correspondent narrating a Risk-style board game live. " +
        "Reply with ONE or TWO vivid sentences (max 40 words). Name the countries involved. " +
        "Base everything strictly on the facts given; do not invent numbers. No emojis, no hashtags, no preamble.";

    private readonly HttpClient _http;
    private readonly AiOptions _options;
    private readonly TemplateCommentaryService _fallback;
    private readonly ILogger<LlmCommentaryService> _logger;

    public LlmCommentaryService(
        HttpClient http,
        IOptions<AiOptions> options,
        TemplateCommentaryService fallback,
        ILogger<LlmCommentaryService> logger)
    {
        _http = http;
        _options = options.Value;
        _fallback = fallback;
        _logger = logger;
    }

    public bool IsLive => !string.IsNullOrWhiteSpace(_options.ApiKey);

    public async Task<string> DescribeRoundAsync(RoundContext c, CancellationToken ct = default)
    {
        if (!IsLive) return await _fallback.DescribeRoundAsync(c, ct);

        var r = c.Round;
        var facts =
            $"Round {c.RoundNumber}. {c.AttackerPlayer} attacks from {c.AttackerCountry} into {c.DefenderCountry} " +
            $"(held by {c.DefenderPlayer}). Attacker dice: {string.Join(", ", r.AttackerDice)}. " +
            $"Defender dice: {string.Join(", ", r.DefenderDice)} (die 1 fights die 1, and so on). " +
            $"Attacker lost {r.AttackerLosses}, defender lost {r.DefenderLosses}, ties {r.Ties}. " +
            $"Now {c.AttackerCountry} has {r.AttackerRemaining} soldiers and {c.DefenderCountry} has {r.DefenderRemaining}. " +
            $"Status: {Describe(c.Outcome, c)}.";

        return await CompleteAsync(facts, ct) ?? await _fallback.DescribeRoundAsync(c, ct);
    }

    public async Task<string> DescribeBattleAsync(BattleSummaryContext c, CancellationToken ct = default)
    {
        if (!IsLive) return await _fallback.DescribeBattleAsync(c, ct);

        var rounds = string.Join(" | ", c.Rounds.Select((r, i) =>
            $"R{i + 1}: [{string.Join(",", r.AttackerDice)}] vs [{string.Join(",", r.DefenderDice)}] " +
            $"→ {r.AttackerRemaining} v {r.DefenderRemaining}"));
        var facts =
            $"Whole battle summary. {c.AttackerPlayer} attacked {c.DefenderCountry} ({c.DefenderPlayer}) from " +
            $"{c.AttackerCountry}. Rounds: {rounds}. Result: {c.Outcome}.";

        return await CompleteAsync(facts, ct) ?? await _fallback.DescribeBattleAsync(c, ct);
    }

    private async Task<string?> CompleteAsync(string facts, CancellationToken ct)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _options.TimeoutSeconds)));

            var body = new
            {
                model = _options.Model,
                temperature = 0.9,
                max_tokens = 90,
                messages = new object[]
                {
                    new { role = "system", content = SystemPrompt },
                    new { role = "user", content = facts }
                }
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl.TrimEnd('/')}/chat/completions")
            {
                Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

            using var response = await _http.SendAsync(request, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("LLM commentary request failed with HTTP {Status}.", (int)response.StatusCode);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: timeout.Token);
            var text = json.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            return string.IsNullOrWhiteSpace(text) ? null : text.Trim().Trim('"');
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException
                                       or KeyNotFoundException or InvalidOperationException or IndexOutOfRangeException)
        {
            _logger.LogWarning("LLM commentary unavailable ({Error}); using built-in commentary.", ex.Message);
            return null;
        }
    }

    private static string Describe(string outcome, RoundContext c) => outcome switch
    {
        "conquered" => $"{c.DefenderCountry} has fallen — {c.AttackerPlayer} conquers it",
        "repelled" => $"the attack has failed; {c.AttackerCountry} is down to 1 soldier",
        _ => "the battle continues"
    };
}
