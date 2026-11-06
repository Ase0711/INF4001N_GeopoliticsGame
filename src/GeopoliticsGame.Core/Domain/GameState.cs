namespace GeopoliticsGame.Core.Domain;


public class Territory
{
    public string CountryCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Flag { get; set; } = string.Empty;
    public long Population { get; set; }

    public int SoldierValue { get; set; }

    public int Soldiers { get; set; }
    public string OwnerId { get; set; } = string.Empty;
}

public class Player
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public PlayerType Type { get; set; }
    public string? UserId { get; set; }
    public NpcProfile Profile { get; set; } = NpcProfile.Balanced;
    public string Color { get; set; } = "#888888";
    public string HomeCountryCode { get; set; } = string.Empty;
    public bool Eliminated { get; set; }
    public int ReinforcementsPending { get; set; }
}


public class PendingAttack
{
    public string AttackerId { get; set; } = string.Empty;
    public string FromCode { get; set; } = string.Empty;
    public string ToCode { get; set; } = string.Empty;
    public int Rounds { get; set; }
}


public class BattleRound
{
    public List<int> AttackerDice { get; set; } = new();
    public List<int> DefenderDice { get; set; } = new();
    public int AttackerLosses { get; set; }
    public int DefenderLosses { get; set; }
    public int Ties { get; set; }
    public int AttackerRemaining { get; set; }
    public int DefenderRemaining { get; set; }
}

public class LogEntry
{
    public DateTime At { get; set; } = DateTime.UtcNow;
    public LogType Type { get; set; }
    public string Actor { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? FromCode { get; set; }
    public string? ToCode { get; set; }
    public BattleRound? Round { get; set; }
}

public class GameState
{
    public string Id { get; set; } = string.Empty;
    public string OwnerUserId { get; set; } = string.Empty;
    public string OwnerUsername { get; set; } = string.Empty;
    public string HumanCountryCode { get; set; } = string.Empty;

    public GameStatus Status { get; set; } = GameStatus.Active;
    public string? WinnerId { get; set; }

    public int Round { get; set; } = 1;
    public List<string> TurnOrder { get; set; } = new();
    public int CurrentTurnIndex { get; set; }
    public bool HasAttackedThisTurn { get; set; }
    public PendingAttack? PendingAttack { get; set; }

    public List<Player> Players { get; set; } = new();
    public List<Territory> Board { get; set; } = new();
    public List<LogEntry> Log { get; set; } = new();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? FinishedAt { get; set; }

  
    public GameStats Stats { get; set; } = new();
}

public class GameStats
{
    public int BattlesWon { get; set; }
    public int BattlesLost { get; set; }
    public int CountriesConquered { get; set; }
    public int DiceRolled { get; set; }
}
