using GeopoliticsGame.Core.Domain;

namespace GeopoliticsGame.Core.Abstractions;

/// <summary>Context for narrating a single round of combat.</summary>
public sealed record RoundContext(
    string AttackerPlayer,
    string AttackerCountry,
    string DefenderPlayer,
    string DefenderCountry,
    int RoundNumber,
    BattleRound Round,
    string Outcome);

/// <summary>Context for narrating a whole battle in one go (used for fast NPC turns).</summary>
public sealed record BattleSummaryContext(
    string AttackerPlayer,
    string AttackerCountry,
    string DefenderPlayer,
    string DefenderCountry,
    IReadOnlyList<BattleRound> Rounds,
    string Outcome);

/// <summary>Generates dynamic battle commentary (LLM, with an offline fallback).</summary>
public interface ICommentaryService
{
    /// <summary>True when a live LLM is configured.</summary>
    bool IsLive { get; }

    Task<string> DescribeRoundAsync(RoundContext context, CancellationToken ct = default);

    Task<string> DescribeBattleAsync(BattleSummaryContext context, CancellationToken ct = default);
}
