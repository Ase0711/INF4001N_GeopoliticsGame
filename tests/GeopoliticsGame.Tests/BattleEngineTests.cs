using GeopoliticsGame.Core.Engine;

namespace GeopoliticsGame.Tests;

public class SoldierAllocatorTests
{
    [Theory]
    [InlineData(500, 1)]                 // Vatican City
    [InlineData(5_000_000, 1)]
    [InlineData(5_000_001, 2)]
    [InlineData(10_000_000, 2)]
    [InlineData(25_000_000, 3)]
    [InlineData(50_000_000, 4)]
    [InlineData(61_000_000, 5)]          // South Africa
    [InlineData(75_000_000, 5)]
    [InlineData(100_000_000, 6)]
    [InlineData(125_000_000, 7)]
    [InlineData(150_000_000, 8)]
    [InlineData(150_000_001, 9)]
    [InlineData(1_441_000_000, 9)]       // India
    public void Population_bands_map_to_soldiers(long population, int expected) =>
        Assert.Equal(expected, SoldierAllocator.SoldiersFor(population));
}

public class BattleEngineTests
{
    [Theory]
    [InlineData(10, 2, 2)] // case-study example 1
    [InlineData(3, 5, 2)]  // example 2
    [InlineData(2, 5, 1)]  // example 3
    [InlineData(1, 5, 0)]
    [InlineData(9, 9, 8)]
    public void Dice_count_is_the_least_soldiers_able_to_fight(int attacker, int defender, int expected) =>
        Assert.Equal(expected, BattleEngine.DiceCount(attacker, defender));

    [Theory]
    [InlineData(9, 4)] // India
    [InlineData(1, 2)] // Vatican City
    [InlineData(4, 2)]
    [InlineData(6, 3)]
    [InlineData(7, 3)]
    public void Conquest_reward_is_max_of_two_and_half_value(int value, int expected) =>
        Assert.Equal(expected, BattleEngine.ConquestReward(value));

    [Fact]
    public void Reproduces_the_case_study_example()
    {
        // Attacker (10) rolls 1 and 4; defender (2) rolls 2 and 3. Dice are interleaved a1, d1, a2, d2.
        var engine = new BattleEngine(new ScriptedDice(new[] { 1, 2, 4, 3 }));

        var round = engine.ResolveRound(10, 2);

        Assert.Equal(new List<int> { 1, 4 }, round.AttackerDice);
        Assert.Equal(new List<int> { 2, 3 }, round.DefenderDice);
        Assert.Equal(1, round.AttackerLosses);
        Assert.Equal(1, round.DefenderLosses);
        Assert.Equal(9, round.AttackerRemaining);
        Assert.Equal(1, round.DefenderRemaining);
    }

    [Fact]
    public void Dice_are_paired_in_roll_order_not_sorted()
    {
        // Attacker rolls 2, 5; defender rolls 3, 1 (two dice each: 3 attackers vs 2 defenders).
        // Roll order: 2v3 (attacker loses), 5v1 (defender loses) → one loss each.
        // Sorted it would be 5v3 and 2v1 → the defender loses both. The case study pairs in roll order.
        var engine = new BattleEngine(new ScriptedDice(new[] { 2, 3, 5, 1 }));

        var round = engine.ResolveRound(3, 2);

        Assert.Equal(1, round.AttackerLosses);
        Assert.Equal(1, round.DefenderLosses);
    }

    [Fact]
    public void Ties_kill_nobody()
    {
        var engine = new BattleEngine(new ScriptedDice(new[] { 4, 4, 2, 2 }));

        var round = engine.ResolveRound(3, 2);

        Assert.Equal(2, round.Ties);
        Assert.Equal(0, round.AttackerLosses);
        Assert.Equal(0, round.DefenderLosses);
    }

    [Fact]
    public void Cannot_attack_with_one_soldier()
    {
        var engine = new BattleEngine(new SecureDice());
        Assert.Throws<GameRuleException>(() => engine.ResolveRound(1, 3));
    }

    [Fact]
    public void Random_rounds_respect_the_rules()
    {
        var engine = new BattleEngine(new SecureDice());
        for (var i = 0; i < 2000; i++)
        {
            var round = engine.ResolveRound(8, 3);
            Assert.Equal(3, round.AttackerDice.Count);
            Assert.Equal(round.AttackerDice.Count, round.DefenderDice.Count);
            Assert.All(round.AttackerDice, d => Assert.InRange(d, 1, 6));
            Assert.All(round.DefenderDice, d => Assert.InRange(d, 1, 6));
            Assert.Equal(3, round.AttackerLosses + round.DefenderLosses + round.Ties);
        }
    }
}
