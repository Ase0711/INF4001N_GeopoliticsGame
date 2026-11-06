namespace GeopoliticsGame.Core.Domain;

public enum GameStatus
{
    Active,
    Won,
    Lost
}

public enum PlayerType
{
    Human,
    Npc
}

public enum NpcProfile
{
    Balanced,
    Aggressive,
    Defensive
}

public enum LogType
{
    System,
    Reinforce,
    Deploy,
    Transfer,
    Attack,
    Round,
    Conquer,
    Repelled,
    Retreat,
    Eliminate,
    Win,
    Decision,
    Commentary,
    EndTurn
}
