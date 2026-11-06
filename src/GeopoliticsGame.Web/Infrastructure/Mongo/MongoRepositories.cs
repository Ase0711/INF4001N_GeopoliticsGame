using GeopoliticsGame.Core.Abstractions;
using GeopoliticsGame.Core.Data;
using GeopoliticsGame.Core.Domain;
using MongoDB.Driver;

namespace GeopoliticsGame.Web.Infrastructure.Mongo;

public sealed class MongoUserRepository : IUserRepository
{
    private readonly IMongoCollection<User> _users;

    public MongoUserRepository(MongoContext context) => _users = context.Users;

    public async Task<User?> FindByIdAsync(string id, CancellationToken ct = default) =>
        await _users.Find(u => u.Id == id).FirstOrDefaultAsync(ct);

    public async Task<User?> FindByUsernameOrEmailAsync(string usernameOrEmail, CancellationToken ct = default)
    {
        var key = usernameOrEmail.Trim().ToLowerInvariant();
        return await _users.Find(u => u.Username == key || u.Email == key).FirstOrDefaultAsync(ct);
    }

    public async Task<bool> UsernameExistsAsync(string username, CancellationToken ct = default) =>
        await _users.Find(u => u.Username == username).AnyAsync(ct);

    public async Task<bool> EmailExistsAsync(string email, CancellationToken ct = default) =>
        await _users.Find(u => u.Email == email).AnyAsync(ct);

    public Task CreateAsync(User user, CancellationToken ct = default) =>
        _users.InsertOneAsync(user, cancellationToken: ct);

    public async Task IncrementStatsAsync(string userId, UserStatsDelta d, CancellationToken ct = default)
    {
        if (d.IsEmpty) return;

        // A single atomic $inc: concurrent games can't overwrite each other's stats.
        var update = Builders<User>.Update
            .Inc(u => u.Stats.GamesPlayed, d.GamesPlayed)
            .Inc(u => u.Stats.GamesWon, d.GamesWon)
            .Inc(u => u.Stats.GamesLost, d.GamesLost)
            .Inc(u => u.Stats.CountriesConquered, d.CountriesConquered)
            .Inc(u => u.Stats.BattlesWon, d.BattlesWon)
            .Inc(u => u.Stats.BattlesLost, d.BattlesLost);

        await _users.UpdateOneAsync(u => u.Id == userId, update, cancellationToken: ct);
    }
}

public sealed class MongoGameRepository : IGameRepository
{
    private readonly IMongoCollection<GameState> _games;

    public MongoGameRepository(MongoContext context) => _games = context.Games;

    public async Task<GameState?> FindByIdAsync(string id, CancellationToken ct = default) =>
        await _games.Find(g => g.Id == id).FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<GameState>> FindByOwnerAsync(string ownerUserId, CancellationToken ct = default) =>
        WithoutLogs(await _games.Find(g => g.OwnerUserId == ownerUserId)
            .SortByDescending(g => g.UpdatedAt)
            .ToListAsync(ct));

    public async Task<IReadOnlyList<GameState>> FindAllAsync(CancellationToken ct = default) =>
        WithoutLogs(await _games.Find(FilterDefinition<GameState>.Empty)
            .SortByDescending(g => g.UpdatedAt)
            .ToListAsync(ct));

    // List views don't need the battle log; drop it so the objects passed around stay small.
    private static List<GameState> WithoutLogs(List<GameState> games)
    {
        foreach (var g in games) g.Log = new List<LogEntry>();
        return games;
    }

    public Task CreateAsync(GameState game, CancellationToken ct = default) =>
        _games.InsertOneAsync(game, cancellationToken: ct);

    public Task SaveAsync(GameState game, CancellationToken ct = default) =>
        _games.ReplaceOneAsync(g => g.Id == game.Id, game, new ReplaceOptions { IsUpsert = false }, ct);
}

/// <summary>Country catalogue in MongoDB. Seeded automatically on first use.</summary>
public sealed class MongoCountryRepository : ICountryRepository
{
    private readonly IMongoCollection<CountryInfo> _countries;
    private IReadOnlyList<CountryInfo>? _cache;

    public MongoCountryRepository(MongoContext context) => _countries = context.Countries;

    public async Task<IReadOnlyList<CountryInfo>> GetAllAsync(CancellationToken ct = default)
    {
        if (_cache is not null) return _cache;

        var countries = await _countries.Find(FilterDefinition<CountryInfo>.Empty)
            .SortBy(c => c.Name)
            .ToListAsync(ct);

        if (countries.Count == 0)
        {
            await _countries.InsertManyAsync(CountryCatalogue.All, cancellationToken: ct);
            countries = CountryCatalogue.All.OrderBy(c => c.Name).ToList();
        }

        _cache = countries;
        return _cache;
    }
}
