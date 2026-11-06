namespace GeopoliticsGame.Core.Engine;

/// <summary>Thrown when a move breaks a game rule. The message is safe to show to the player.</summary>
public sealed class GameRuleException : Exception
{
    public GameRuleException(string message) : base(message) { }
}
