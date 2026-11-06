using GeopoliticsGame.Core.Abstractions;
using GeopoliticsGame.Core.Domain;
using GeopoliticsGame.Web.Models;

namespace GeopoliticsGame.Web.Services;


public sealed class AnalyticsService
{
    private readonly IGameRepository _games;
    private readonly IUserRepository _users;

    public AnalyticsService(IGameRepository games, IUserRepository users)
    {
        _games = games;
        _users = users;
    }

    public async Task<AnalyticsViewModel> BuildAsync(string? userId, CancellationToken ct = default)
    {
        var all = await _games.FindAllAsync(ct);
        var player = userId is null ? null : await BuildPlayerAsync(userId, all, ct);
        return new AnalyticsViewModel { Platform = BuildPlatform(all), Player = player };
    }

    private static PlatformStats BuildPlatform(IReadOnlyList<GameState> games)
    {
        var finished = games.Where(g => g.Status != GameStatus.Active).ToList();
        var wins = finished.Count(g => g.Status == GameStatus.Won);

        return new PlatformStats
        {
            TotalGames = games.Count,
            ActiveGames = games.Count(g => g.Status == GameStatus.Active),
            FinishedGames = finished.Count,
            Players = games.Select(g => g.OwnerUserId).Distinct().Count(),
            HumanWins = wins,
            NpcWins = finished.Count - wins,
            HumanWinRate = Percent(wins, finished.Count),
            AverageRoundsToFinish = finished.Count == 0 ? 0 : Math.Round(finished.Average(g => g.Round), 1),
            TotalDiceRolled = games.Sum(g => g.Stats.DiceRolled),
            TotalConquests = games.Sum(g => g.Stats.CountriesConquered),
            MostPickedCountries = CountryStats(games).OrderByDescending(c => c.Count).ThenBy(c => c.Name).Take(5).ToList(),
            BestStartingCountries = CountryStats(finished)
                .Where(c => c.Count > 0)
                .OrderByDescending(c => c.WinRate).ThenByDescending(c => c.Count)
                .Take(5).ToList(),
            Leaderboard = games
                .GroupBy(g => g.OwnerUsername)
                .Select(grp => new LeaderboardRow(
                    grp.Key,
                    grp.Count(g => g.Status == GameStatus.Won),
                    grp.Count(),
                    grp.Sum(g => g.Stats.CountriesConquered)))
                .OrderByDescending(r => r.Wins).ThenByDescending(r => r.Conquests)
                .Take(10).ToList()
        };
    }

    private async Task<PlayerAnalytics?> BuildPlayerAsync(string userId, IReadOnlyList<GameState> all, CancellationToken ct)
    {
        var user = await _users.FindByIdAsync(userId, ct);
        if (user is null) return null;

        var mine = all.Where(g => g.OwnerUserId == userId).ToList();
        var won = mine.Where(g => g.Status == GameStatus.Won).ToList();
        var lost = mine.Count(g => g.Status == GameStatus.Lost);
        var battlesWon = mine.Sum(g => g.Stats.BattlesWon);
        var battlesLost = mine.Sum(g => g.Stats.BattlesLost);

        return new PlayerAnalytics
        {
            Username = user.Username,
            GamesPlayed = mine.Count,
            GamesWon = won.Count,
            GamesLost = lost,
            GamesActive = mine.Count(g => g.Status == GameStatus.Active),
            WinRate = Percent(won.Count, won.Count + lost),
            BattlesWon = battlesWon,
            BattlesLost = battlesLost,
            BattleWinRate = Percent(battlesWon, battlesWon + battlesLost),
            CountriesConquered = mine.Sum(g => g.Stats.CountriesConquered),
            FastestWinRounds = won.Count == 0 ? null : won.Min(g => g.Round),
            FavouriteCountry = CountryStats(mine).OrderByDescending(c => c.Count).FirstOrDefault(),
            RecentGames = mine.OrderByDescending(g => g.UpdatedAt).Take(5).Select(GameSummary.From).ToList()
        };
    }

    private static IEnumerable<CountryStat> CountryStats(IEnumerable<GameState> games) =>
        games.GroupBy(g => g.HumanCountryCode).Select(grp =>
        {
            var sample = grp.First().Board.FirstOrDefault(t => t.CountryCode == grp.Key);
            return new CountryStat(grp.Key, sample?.Name ?? grp.Key, sample?.Flag ?? string.Empty,
                grp.Count(), grp.Count(g => g.Status == GameStatus.Won));
        });

    private static int Percent(int part, int whole) => whole == 0 ? 0 : (int)Math.Round(part * 100.0 / whole);
}
