using GeopoliticsGame.Core.Domain;

namespace GeopoliticsGame.Web.Services;

/// <summary>Board state at the moment a log entry was written. Lets the browser replay NPC turns.</summary>
public sealed class TerritorySnapshot
{
    public string Code { get; init; } = string.Empty;
    public string OwnerId { get; init; } = string.Empty;
    public int Soldiers { get; init; }
}

public sealed class ReplayFrame
{
    public LogEntry Entry { get; init; } = new();
    public string CurrentPlayerId { get; init; } = string.Empty;
    public int Round { get; init; }
    public List<string> EliminatedPlayerIds { get; init; } = new();
    public List<TerritorySnapshot> Board { get; init; } = new();
}

/// <summary>What the API returns after any move.</summary>
public sealed class GameActionResult
{
    public GameState Game { get; set; } = new();

    /// <summary>"continue" | "conquered" | "repelled", for roll actions.</summary>
    public string? Outcome { get; set; }

    public BattleRound? Round { get; set; }
    public string? Commentary { get; set; }
    public int? Reward { get; set; }

    /// <summary>Step-by-step frames of the NPC turns that ran after this action (if any).</summary>
    public List<ReplayFrame> Frames { get; } = new();
}

public sealed class GameNotFoundException : Exception
{
    public GameNotFoundException() : base("Game not found.") { }
}

public sealed class GameAccessDeniedException : Exception
{
    public GameAccessDeniedException()
        : base("Only the player who started this game can make moves. Visitors can watch but not play.") { }
}
