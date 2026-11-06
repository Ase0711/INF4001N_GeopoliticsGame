using GeopoliticsGame.Core.Abstractions;
using GeopoliticsGame.Core.Data;
using GeopoliticsGame.Core.Domain;
using GeopoliticsGame.Core.Engine;

namespace GeopoliticsGame.Tests;

internal sealed class SilentCommentary : ICommentaryService
{
    public bool IsLive => false;
    public Task<string> DescribeRoundAsync(RoundContext context, CancellationToken ct = default) => Task.FromResult("round");
    public Task<string> DescribeBattleAsync(BattleSummaryContext context, CancellationToken ct = default) => Task.FromResult("battle");
}

internal static class TestGames
{
    public static GameState NewGame(string country = "ZAF", int seed = 1) =>
        GameEngine.CreateGame("user-1", "tester", country, CountryCatalogue.All, new ScriptedDice(Array.Empty<int>(), seed));

    /// <summary>Puts the human first in turn order so tests can act immediately.</summary>
    public static GameState NewGameHumanFirst(string country = "ZAF", int seed = 1)
    {
        var state = NewGame(country, seed);
        state.TurnOrder.Remove(GameEngine.HumanId);
        state.TurnOrder.Insert(0, GameEngine.HumanId);
        state.CurrentTurnIndex = 0;
        return state;
    }

    public static Territory Human(GameState s) => s.Board.First(t => t.OwnerId == GameEngine.HumanId);

    public static Territory Enemy(GameState s, int index = 0) => s.Board.Where(t => t.OwnerId != GameEngine.HumanId).ElementAt(index);
}
