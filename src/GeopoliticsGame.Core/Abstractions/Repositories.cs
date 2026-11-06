using GeopoliticsGame.Core.Domain;

namespace GeopoliticsGame.Core.Abstractions;

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
    Task<GameState?> FindByIdAsync(string id, CancellationToken ct = default);

   
    Task<IReadOnlyList<GameState>> FindByOwnerAsync(string ownerUserId, CancellationToken ct = default);

  
    Task<IReadOnlyList<GameState>> FindAllAsync(CancellationToken ct = default);

    Task CreateAsync(GameState game, CancellationToken ct = default);
    Task SaveAsync(GameState game, CancellationToken ct = default);
}

public interface ICountryRepository
{
    Task<IReadOnlyList<CountryInfo>> GetAllAsync(CancellationToken ct = default);
}
