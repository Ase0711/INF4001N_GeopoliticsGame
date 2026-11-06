using System.Security.Cryptography;

namespace GeopoliticsGame.Core.Engine;

/// <summary>Source of randomness for the game. Abstracted so tests can use fixed rolls.</summary>
public interface IDice
{
    /// <summary>Returns a value from 1 to 6.</summary>
    int Roll();

    /// <summary>Returns an integer in [0, maxExclusive).</summary>
    int Next(int maxExclusive);
}

/// <summary>
/// Production dice backed by a cryptographically secure RNG, so rolls can't be predicted
/// from the client.
/// </summary>
public sealed class SecureDice : IDice
{
    public int Roll() => RandomNumberGenerator.GetInt32(1, 7);

    public int Next(int maxExclusive) => RandomNumberGenerator.GetInt32(0, maxExclusive);
}

/// <summary>Deterministic dice for tests: plays back a fixed sequence of rolls.</summary>
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
