using System.Security.Cryptography;

namespace GeopoliticsGame.Core.Engine;

public interface IDice
{
    int Roll();

    int Next(int maxExclusive);
}

public sealed class SecureDice : IDice
{
    public int Roll() => RandomNumberGenerator.GetInt32(1, 7);

    public int Next(int maxExclusive) => RandomNumberGenerator.GetInt32(0, maxExclusive);
}

public sealed class ScriptedDice : IDice
{
    private readonly Queue<int> _rolls;
    private readonly Random _fallback;

    public ScriptedDice(IEnumerable<int> rolls, int seed = 42)
    {
        _rolls = new Queue<int>(rolls);
        _fallback = new Random(seed);
    }

    public int Roll() => _rolls.Count > 0 ? _rolls.Dequeue() : _fallback.Next(1, 7);

    public int Next(int maxExclusive) => _fallback.Next(maxExclusive);
}
