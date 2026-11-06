using GeopoliticsGame.Core.Domain;

namespace GeopoliticsGame.Core.Abstractions;

/// <summary>
/// Data-access contracts. The web layer and the engine depend only on these interfaces;
/// MongoDB (or the in-memory fallback) lives behind them in the infrastructure layer.
/// </summary>
public interface IUserRepository
{
    Task<User?> FindByIdAsync(string id, CancellationToken ct = default);
    Task<User?> FindByUsernameOrEmailAsync(string usernameOrEmail, CancellationToken ct = default);
    Task<bool> UsernameExistsAsync(string username, CancellationToken ct = default);
    Task<bool> EmailExistsAsync(string email, CancellationToken ct = default);
    Task CreateAsync(User user, CancellationToken ct = default);
    Task IncrementStatsAsync(string userId, UserStatsDelta delta, CancellationToken ct = default);
}

public interface IGameRepository
{
    /// <summary>Full game, including the battle log.</summary>
    Task<GameState?> FindByIdAsync(string id, CancellationToken ct = default);

    /// <summary>A user's games, most recently played first. The log is omitted to keep lists light.</summary>
    Task<IReadOnlyList<GameState>> FindByOwnerAsync(string ownerUserId, CancellationToken ct = default);

    /// <summary>Every game on the platform (for analytics / the public list). The log is omitted.</summary>
    Task<IReadOnlyList<GameState>> FindAllAsync(CancellationToken ct = default);

    Task CreateAsync(GameState game, CancellationToken ct = default);
    Task SaveAsync(GameState game, CancellationToken ct = default);
}

public interface ICountryRepository
{
    Task<IReadOnlyList<CountryInfo>> GetAllAsync(CancellationToken ct = default);
}
