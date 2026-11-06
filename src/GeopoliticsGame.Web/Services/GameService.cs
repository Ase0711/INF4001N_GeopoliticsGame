using GeopoliticsGame.Core.Abstractions;
using GeopoliticsGame.Core.Domain;
using GeopoliticsGame.Core.Engine;

namespace GeopoliticsGame.Web.Services;

/// <summary>
/// Application service for gameplay. It locks, loads, checks ownership, runs the engine,
/// runs the NPCs, then saves the game and updates stats. All rules are enforced in
/// <see cref="GameEngine"/>; this class only coordinates.
/// </summary>
public sealed class GameService
{
    private const int MaxNpcTurnsPerRequest = 60;

    private readonly IGameRepository _games;
    private readonly IUserRepository _users;
    private readonly ICountryRepository _countries;
    private readonly ICommentaryService _commentary;
    private readonly IDice _dice;
    private readonly GameLocks _locks;

    public GameService(
        IGameRepository games,
        IUserRepository users,
        ICountryRepository countries,
        ICommentaryService commentary,
        IDice dice,
        GameLocks locks)
    {
        _games = games;
        _users = users;
        _countries = countries;
        _commentary = commentary;
        _dice = dice;
        _locks = locks;
    }

    public bool CommentaryIsLive => _commentary.IsLive;

    // ───────────────────────────── Queries ─────────────────────────────

    public Task<IReadOnlyList<CountryInfo>> GetCountriesAsync(CancellationToken ct = default) =>
        _countries.GetAllAsync(ct);

    public Task<GameState?> GetAsync(string id, CancellationToken ct = default) =>
        _games.FindByIdAsync(id, ct);

    public Task<IReadOnlyList<GameState>> GetUserGamesAsync(string userId, CancellationToken ct = default) =>
        _games.FindByOwnerAsync(userId, ct);

    public Task<IReadOnlyList<GameState>> GetAllGamesAsync(CancellationToken ct = default) =>
        _games.FindAllAsync(ct);

    // ───────────────────────────── Commands ────────────────────────────

    public async Task<GameState> CreateAsync(string userId, string username, string countryCode, CancellationToken ct = default)
    {
        var catalogue = await _countries.GetAllAsync(ct);
        var state = GameEngine.CreateGame(userId, username, countryCode, catalogue, _dice);
        await _games.CreateAsync(state, ct);
        await _users.IncrementStatsAsync(userId, new UserStatsDelta { GamesPlayed = 1 }, ct);
        return state;
    }

    public Task<GameActionResult> DeployAsync(string id, string userId, IReadOnlyDictionary<string, int> allocation, CancellationToken ct = default) =>
        ExecuteAsync(id, userId, (engine, _) =>
        {
            engine.Deploy(GameEngine.HumanId, allocation);
            return Task.CompletedTask;
        }, runNpcs: false, ct);

    public Task<GameActionResult> TransferAsync(string id, string userId, string from, string to, int amount, CancellationToken ct = default) =>
        ExecuteAsync(id, userId, (engine, _) =>
        {
            engine.Transfer(GameEngine.HumanId, from, to, amount);
            return Task.CompletedTask;
        }, runNpcs: false, ct);

    public Task<GameActionResult> StartAttackAsync(string id, string userId, string from, string to, CancellationToken ct = default) =>
        ExecuteAsync(id, userId, (engine, _) =>
        {
            engine.StartAttack(GameEngine.HumanId, from, to);
            return Task.CompletedTask;
        }, runNpcs: false, ct);

    public Task<GameActionResult> RollAsync(string id, string userId, CancellationToken ct = default) =>
        ExecuteAsync(id, userId, async (engine, result) =>
        {
            var pending = engine.State.PendingAttack
                          ?? throw new GameRuleException("There is no attack in progress.");
            var from = engine.GetTerritory(pending.FromCode);
            var to = engine.GetTerritory(pending.ToCode);
            var defenderName = engine.GetPlayer(to.OwnerId).Name;

            var roll = engine.RollAttack(GameEngine.HumanId);
            var outcome = roll.Outcome.ToString().ToLowerInvariant();

            var commentary = await _commentary.DescribeRoundAsync(new RoundContext(
                engine.GetPlayer(GameEngine.HumanId).Name, from.Name, defenderName, to.Name,
                roll.RoundNumber, roll.Round, outcome), ct);
            engine.RecordCommentary(commentary);

            result.Outcome = outcome;
            result.Round = roll.Round;
            result.Commentary = commentary;
            result.Reward = roll.Outcome == AttackOutcome.Conquered ? roll.Reward : null;
        }, runNpcs: false, ct);

    /// <summary>Calling off an attack ends the turn, so the NPCs then play.</summary>
    public Task<GameActionResult> RetreatAsync(string id, string userId, CancellationToken ct = default) =>
        ExecuteAsync(id, userId, (engine, _) =>
        {
            engine.Retreat(GameEngine.HumanId);
            return Task.CompletedTask;
        }, runNpcs: true, ct);

    public Task<GameActionResult> EndTurnAsync(string id, string userId, CancellationToken ct = default) =>
        ExecuteAsync(id, userId, (engine, _) =>
        {
            engine.EndTurn(GameEngine.HumanId);
            return Task.CompletedTask;
        }, runNpcs: true, ct);

    /// <summary>Runs pending NPC turns, e.g. when an NPC moves first in a new game.</summary>
    public Task<GameActionResult> RunNpcTurnsAsync(string id, string userId, CancellationToken ct = default) =>
        ExecuteAsync(id, userId, (_, _) => Task.CompletedTask, runNpcs: true, ct);

    // ───────────────────────────── Pipeline ────────────────────────────

    private async Task<GameActionResult> ExecuteAsync(
        string id,
        string userId,
        Func<GameEngine, GameActionResult, Task> action,
        bool runNpcs,
        CancellationToken ct)
    {
        using var _ = await _locks.AcquireAsync(id, ct);

        var state = await _games.FindByIdAsync(id, ct) ?? throw new GameNotFoundException();
        if (state.OwnerUserId != userId) throw new GameAccessDeniedException();

        var statusBefore = state.Status;
        var statsBefore = Copy(state.Stats);
        var engine = new GameEngine(state, _dice);
        var result = new GameActionResult();

        await action(engine, result);

        if (runNpcs) await RunNpcsAsync(engine, result, ct);

        await _games.SaveAsync(state, ct);
        await _users.IncrementStatsAsync(userId, StatsDelta(statusBefore, state, statsBefore), ct);

        result.Game = state;
        return result;
    }

    private async Task RunNpcsAsync(GameEngine engine, GameActionResult result, CancellationToken ct)
    {
        void Capture(LogEntry entry) => result.Frames.Add(Snapshot(engine.State, entry));
        engine.Logged += Capture;
        try
        {
            var runner = new NpcTurnRunner(_commentary);
            for (var i = 0; i < MaxNpcTurnsPerRequest && engine.IsActive && !engine.IsHumanTurn; i++)
            {
                await runner.PlayTurnAsync(engine, ct);
            }
        }
        finally
        {
            engine.Logged -= Capture;
        }
    }

    private static ReplayFrame Snapshot(GameState state, LogEntry entry) => new()
    {
        Entry = entry,
        CurrentPlayerId = state.TurnOrder[state.CurrentTurnIndex],
        Round = state.Round,
        EliminatedPlayerIds = state.Players.Where(p => p.Eliminated).Select(p => p.Id).ToList(),
        Board = state.Board.Select(t => new TerritorySnapshot
        {
            Code = t.CountryCode,
            OwnerId = t.OwnerId,
            Soldiers = t.Soldiers
        }).ToList()
    };

    private static UserStatsDelta StatsDelta(GameStatus before, GameState after, GameStats statsBefore)
    {
        var finishedNow = before == GameStatus.Active && after.Status != GameStatus.Active;
        return new UserStatsDelta
        {
            GamesWon = finishedNow && after.Status == GameStatus.Won ? 1 : 0,
            GamesLost = finishedNow && after.Status == GameStatus.Lost ? 1 : 0,
            BattlesWon = after.Stats.BattlesWon - statsBefore.BattlesWon,
            BattlesLost = after.Stats.BattlesLost - statsBefore.BattlesLost,
            CountriesConquered = after.Stats.CountriesConquered - statsBefore.CountriesConquered
        };
    }

    private static GameStats Copy(GameStats s) => new()
    {
        BattlesWon = s.BattlesWon,
        BattlesLost = s.BattlesLost,
        CountriesConquered = s.CountriesConquered,
        DiceRolled = s.DiceRolled
    };
}
