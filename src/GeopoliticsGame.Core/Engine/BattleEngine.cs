using GeopoliticsGame.Core.Domain;

namespace GeopoliticsGame.Core.Engine;

/// <summary>
/// Pure combat rules from the case study:
/// <list type="bullet">
///   <item>An attacker needs at least 2 soldiers, and must always leave 1 behind.</item>
///   <item>Dice per side = least soldiers available to fight = min(attacker − 1, defender).</item>
///   <item>Die i of the attacker is matched against die i of the defender (roll order).</item>
///   <item>Higher die wins and the other side loses one soldier; a tie kills nobody.</item>
///   <item>Conquest reward = max(2, soldier value / 2), rounded down.</item>
/// </list>
/// </summary>
public sealed class BattleEngine
{
    public const int MinimumSoldiersToAttack = 2;

    private readonly IDice _dice;

    public BattleEngine(IDice dice)
    {
        _dice = dice ?? throw new ArgumentNullException(nameof(dice));
    }

    public static bool CanAttack(int attackerSoldiers) => attackerSoldiers >= MinimumSoldiersToAttack;

    public static int DiceCount(int attackerSoldiers, int defenderSoldiers) =>
        Math.Max(0, Math.Min(attackerSoldiers - 1, defenderSoldiers));

    public static int ConquestReward(int conqueredSoldierValue) =>
        Math.Max(2, conqueredSoldierValue / 2);

    /// <summary>Rolls and resolves one round. Does not mutate any territory.</summary>
    public BattleRound ResolveRound(int attackerSoldiers, int defenderSoldiers)
    {
        if (!CanAttack(attackerSoldiers))
            throw new GameRuleException("At least 2 soldiers are needed to attack.");
        if (defenderSoldiers <= 0)
            throw new GameRuleException("The defending country has no soldiers left.");

        var count = DiceCount(attackerSoldiers, defenderSoldiers);
        var round = new BattleRound();

        for (var i = 0; i < count; i++)
        {
            round.AttackerDice.Add(_dice.Roll());
            round.DefenderDice.Add(_dice.Roll());
        }

        for (var i = 0; i < count; i++)
        {
            var a = round.AttackerDice[i];
            var d = round.DefenderDice[i];
            if (a > d) round.DefenderLosses++;
            else if (d > a) round.AttackerLosses++;
            else round.Ties++;
        }

        round.AttackerRemaining = attackerSoldiers - round.AttackerLosses;
        round.DefenderRemaining = defenderSoldiers - round.DefenderLosses;
        return round;
    }
}
