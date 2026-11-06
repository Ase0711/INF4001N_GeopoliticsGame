using System.Collections.Concurrent;
using System.Text.Json;
using GeopoliticsGame.Core.Abstractions;
using GeopoliticsGame.Core.Data;
using GeopoliticsGame.Core.Domain;

namespace GeopoliticsGame.Web.Infrastructure.Memory;

/// <summary>
/// In-memory implementation of the repositories, used for offline demos or when MongoDB is
/// not configured. Objects are deep-copied in and out, so it behaves like a real database
/// (callers never share references with what is "stored"). Data is lost on restart.
/// </summary>
internal static class DeepCopy
{
    private static readonly JsonSerializerOptions Options = new();

    public static T Of<T>(T value) =>
        JsonSerializer.Deserialize<T>(JsonSerializer.SerializeToUtf8Bytes(value, Options), Options)!;
}

public sealed class MemoryUserRepository : IUserRepository
{
    private readonly ConcurrentDictionary<string, User> _users = new();

    public Task<User?> FindByIdAsync(string id, CancellationToken ct = default) =>
        Task.FromResult(_users.TryGetValue(id, out var u) ? DeepCopy.Of(u) : null);

    public Task<User?> FindByUsernameOrEmailAsync(string usernameOrEmail, CancellationToken ct = default)
    {
        var key = usernameOrEmail.Trim().ToLowerInvariant();
        var user = _users.Values.FirstOrDefault(u => u.Username == key || u.Email == key);
        return Task.FromResult(user is null ? null : DeepCopy.Of(user));
    }

    public Task<bool> UsernameExistsAsync(string username, CancellationToken ct = default) =>
        Task.FromResult(_users.Values.Any(u => u.Username == username));

    public Task<bool> EmailExistsAsync(string email, CancellationToken ct = default) =>
        Task.FromResult(_users.Values.Any(u => u.Email == email));

    public Task CreateAsync(User user, CancellationToken ct = default)
    {
        if (!_users.TryAdd(user.Id, DeepCopy.Of(user)))
            throw new InvalidOperationException("Duplicate user id.");
        return Task.CompletedTask;
    }

    public Task IncrementStatsAsync(string userId, UserStatsDelta d, CancellationToken ct = default)
    {
        if (_users.TryGetValue(userId, out var u))
        {
            lock (u)
            {
                u.Stats.GamesPlayed += d.GamesPlayed;
                u.Stats.GamesWon += d.GamesWon;
                u.Stats.GamesLost += d.GamesLost;
                u.Stats.CountriesConquered += d.CountriesConquered;
                u.Stats.BattlesWon += d.BattlesWon;
                u.Stats.BattlesLost += d.BattlesLost;
            }
        }
        return Task.CompletedTask;
    }
}

public sealed class MemoryGameRepository : IGameRepository
{
    private readonly ConcurrentDictionary<string, GameState> _games = new();

    public Task<GameState?> FindByIdAsync(string id, CancellationToken ct = default) =>
        Task.FromResult(_games.TryGetValue(id, out var g) ? DeepCopy.Of(g) : null);

    public Task<IReadOnlyList<GameState>> FindByOwnerAsync(string ownerUserId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<GameState>>(_games.Values
            .Where(g => g.OwnerUserId == ownerUserId)
            .OrderByDescending(g => g.UpdatedAt)
            .Select(WithoutLog)
            .ToList());

    public Task<IReadOnlyList<GameState>> FindAllAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<GameState>>(_games.Values
            .OrderByDescending(g => g.UpdatedAt)
            .Select(WithoutLog)
            .ToList());

    public Task CreateAsync(GameState game, CancellationToken ct = default)
    {
        if (!_games.TryAdd(game.Id, DeepCopy.Of(game)))
            throw new InvalidOperationException("Duplicate game id.");
        return Task.CompletedTask;
    }

    public Task SaveAsync(GameState game, CancellationToken ct = default)
    {
        _games[game.Id] = DeepCopy.Of(game);
        return Task.CompletedTask;
    }

    private static GameState WithoutLog(GameState g)
    {
        var copy = DeepCopy.Of(g);
        copy.Log = new List<LogEntry>();
        return copy;
    }
}

public sealed class MemoryCountryRepository : ICountryRepository
{
    public Task<IReadOnlyList<CountryInfo>> GetAllAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<CountryInfo>>(CountryCatalogue.All.Select(DeepCopy.Of).ToList());
}
