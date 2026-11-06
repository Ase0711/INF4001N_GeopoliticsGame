namespace GeopoliticsGame.Core.Engine;

public static class SoldierAllocator
{
    private static readonly (long MaxPopulation, int Soldiers)[] Bands =
    {
        (5_000_000, 1),
        (10_000_000, 2),
        (25_000_000, 3),
        (50_000_000, 4),
        (75_000_000, 5),
        (100_000_000, 6),
        (125_000_000, 7),
        (150_000_000, 8),
    };

    public const int MaxSoldiers = 9;

    public static int SoldiersFor(long population)
    {
        if (population < 0) throw new ArgumentOutOfRangeException(nameof(population));
        foreach (var (max, soldiers) in Bands)
        {
            if (population <= max) return soldiers;
        }
        return MaxSoldiers; 
    }
}
