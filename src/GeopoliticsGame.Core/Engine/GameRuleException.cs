namespace GeopoliticsGame.Core.Engine;

public sealed class GameRuleException : Exception
{
    public GameRuleException(string message) : base(message) { }
}
