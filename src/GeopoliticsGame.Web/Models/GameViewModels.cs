using System.ComponentModel.DataAnnotations;
using GeopoliticsGame.Core.Domain;
using GeopoliticsGame.Core.Engine;

namespace GeopoliticsGame.Web.Models;

/// <summary>A light, list-friendly view of a game (dashboard, analytics, home page).</summary>
public sealed class GameSummary
{
    public string Id { get; init; } = string.Empty;
    public string OwnerUsername { get; init; } = string.Empty;
    public GameStatus Status { get; init; }
    public int Round { get; init; }
    public string CountryCode { get; init; } = string.Empty;
    public string CountryName { get; init; } = string.Empty;
    public string CountryFlag { get; init; } = string.Empty;
    public int CountriesHeld { get; init; }
    public int SoldiersHeld { get; init; }
    public int PlayersRemaining { get; init; }
    public bool IsYourTurn { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }

    public static GameSummary From(GameState g)
    {
        var home = g.Board.FirstOrDefault(t => t.CountryCode == g.HumanCountryCode);
        return new GameSummary
        {
            Id = g.Id,
            OwnerUsername = g.OwnerUsername,
            Status = g.Status,
            Round = g.Round,
            CountryCode = g.HumanCountryCode,
            CountryName = home?.Name ?? g.HumanCountryCode,
            CountryFlag = home?.Flag ?? string.Empty,
            CountriesHeld = g.Board.Count(t => t.OwnerId == GameEngine.HumanId),
            SoldiersHeld = g.Board.Where(t => t.OwnerId == GameEngine.HumanId).Sum(t => t.Soldiers),
            PlayersRemaining = g.Players.Count(p => !p.Eliminated),
            IsYourTurn = g.Status == GameStatus.Active && g.TurnOrder.Count > 0 &&
                         g.TurnOrder[g.CurrentTurnIndex] == GameEngine.HumanId,
            CreatedAt = g.CreatedAt,
            UpdatedAt = g.UpdatedAt
        };
    }
}

public sealed class DashboardViewModel
{
    public IReadOnlyList<GameSummary> Games { get; init; } = Array.Empty<GameSummary>();
    public int TotalGames { get; init; }
    public string? Status { get; init; }
    public string? Search { get; init; }
    public string Sort { get; init; } = "recent";
    public NewGameViewModel NewGame { get; init; } = new();
}

public sealed class NewGameViewModel
{
    [Required(ErrorMessage = "Please choose a country.")]
    [RegularExpression("^[A-Za-z]{3}$", ErrorMessage = "Please choose a country from the list.")]
    [Display(Name = "Your country")]
    public string? CountryCode { get; set; }

    public IReadOnlyList<CountryInfo> Countries { get; set; } = Array.Empty<CountryInfo>();
}

public sealed class HomeViewModel
{
    public NewGameViewModel NewGame { get; init; } = new();
    public IReadOnlyList<GameSummary> LiveGames { get; init; } = Array.Empty<GameSummary>();
}

public sealed class PlayViewModel
{
    public string GameId { get; init; } = string.Empty;
    public string OwnerUsername { get; init; } = string.Empty;
    public bool CanPlay { get; init; }
    public bool IsSignedIn { get; init; }
    public bool AiIsLive { get; init; }
}
