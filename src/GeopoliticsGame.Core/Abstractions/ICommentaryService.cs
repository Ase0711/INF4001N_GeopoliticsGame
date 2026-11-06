using GeopoliticsGame.Core.Domain;

namespace GeopoliticsGame.Core.Abstractions;

public sealed record RoundContext(
    string AttackerPlayer,
    string AttackerCountry,
    string DefenderPlayer,
    string DefenderCountry,
    int RoundNumber,
    BattleRound Round,
    string Outcome);


public sealed record BattleSummaryContext(
    string AttackerPlayer,
    string AttackerCountry,
    string DefenderPlayer,
    string DefenderCountry,
    IReadOnlyList<BattleRound> Rounds,
    string Outcome);


public interface ICommentaryService
{
 
    bool IsLive { get; }

    Task<string> DescribeRoundAsync(RoundContext context, CancellationToken ct = default);

    Task<string> DescribeBattleAsync(BattleSummaryContext context, CancellationToken ct = default);
}
