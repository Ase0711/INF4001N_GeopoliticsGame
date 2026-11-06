namespace GeopoliticsGame.Web.Models;

public sealed class AnalyticsViewModel
{
    public PlatformStats Platform { get; init; } = new();
    public PlayerAnalytics? Player { get; init; }
}

public sealed class PlatformStats
{
    public int TotalGames { get; init; }
    public int ActiveGames { get; init; }
    public int FinishedGames { get; init; }
    public int Players { get; init; }
    public int HumanWins { get; init; }
    public int NpcWins { get; init; }
    public int HumanWinRate { get; init; }
    public double AverageRoundsToFinish { get; init; }
    public int TotalDiceRolled { get; init; }
    public int TotalConquests { get; init; }
    public List<CountryStat> MostPickedCountries { get; init; } = new();
    public List<CountryStat> BestStartingCountries { get; init; } = new();
    public List<LeaderboardRow> Leaderboard { get; init; } = new();
}

public sealed class PlayerAnalytics
{
    public string Username { get; init; } = string.Empty;
    public int GamesPlayed { get; init; }
    public int GamesWon { get; init; }
    public int GamesLost { get; init; }
    public int GamesActive { get; init; }
    public int WinRate { get; init; }
    public int BattlesWon { get; init; }
    public int BattlesLost { get; init; }
    public int BattleWinRate { get; init; }
    public int CountriesConquered { get; init; }
    public int? FastestWinRounds { get; init; }
    public CountryStat? FavouriteCountry { get; init; }
    public List<GameSummary> RecentGames { get; init; } = new();
}

public sealed record CountryStat(string Code, string Name, string Flag, int Count, int Wins)
{
    public int WinRate => Count == 0 ? 0 : (int)Math.Round(Wins * 100.0 / Count);
}

public sealed record LeaderboardRow(string Username, int Wins, int Games, int Conquests);
