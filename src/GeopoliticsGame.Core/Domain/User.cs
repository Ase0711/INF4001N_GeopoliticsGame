namespace GeopoliticsGame.Core.Domain;

public class User
{
    public string Id { get; set; } = string.Empty;

    /// <summary>Stored lower-case; unique.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Stored lower-case; unique.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>PBKDF2 hash — never the plain password.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public UserStats Stats { get; set; } = new();
}

public class UserStats
{
    public int GamesPlayed { get; set; }
    public int GamesWon { get; set; }
    public int GamesLost { get; set; }
    public int CountriesConquered { get; set; }
    public int BattlesWon { get; set; }
    public int BattlesLost { get; set; }
}

/// <summary>Increment-only change to <see cref="UserStats"/> (maps to an atomic $inc in MongoDB).</summary>
public sealed class UserStatsDelta
{
    public int GamesPlayed { get; init; }
    public int GamesWon { get; init; }
    public int GamesLost { get; init; }
    public int CountriesConquered { get; init; }
    public int BattlesWon { get; init; }
    public int BattlesLost { get; init; }

    public bool IsEmpty =>
        GamesPlayed == 0 && GamesWon == 0 && GamesLost == 0 &&
        CountriesConquered == 0 && BattlesWon == 0 && BattlesLost == 0;
}
