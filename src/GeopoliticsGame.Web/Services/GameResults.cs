using GeopoliticsGame.Core.Domain;

namespace GeopoliticsGame.Web.Services;


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


public sealed class GameActionResult
{
    public GameState Game { get; set; } = new();

 
    public string? Outcome { get; set; }

    public BattleRound? Round { get; set; }
    public string? Commentary { get; set; }
    public int? Reward { get; set; }


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
