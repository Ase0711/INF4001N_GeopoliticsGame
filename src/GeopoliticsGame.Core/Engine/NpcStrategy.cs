using GeopoliticsGame.Core.Domain;

namespace GeopoliticsGame.Core.Engine;

public sealed record ReinforcementPlan(IReadOnlyDictionary<string, int> Allocation, string Justification);

public sealed record AttackDecision(bool Attack, string? FromCode, string? ToCode, string Justification)
{
    public static AttackDecision Hold(string why) => new(false, null, null, why);
}

public sealed record ContinueDecision(bool Continue, string Justification);

public sealed record TransferPlan(string FromCode, string ToCode, int Amount, string Justification);

public sealed class NpcStrategy
{
    private readonly NpcProfile _profile;

    public NpcStrategy(NpcProfile profile) => _profile = profile;

    public NpcProfile Profile => _profile;

    public double AttackThreshold => _profile switch
    {
        NpcProfile.Aggressive => 1.0,
        NpcProfile.Defensive => 2.0,
        _ => 1.4
    };

    public double ContinueThreshold => _profile switch
    {
        NpcProfile.Aggressive => 0.5,
        NpcProfile.Defensive => 1.1,
        _ => 0.8
    };


    public ReinforcementPlan PlanReinforcements(GameState state, string npcId, int amount)
    {
        var mine = state.Board.Where(t => t.OwnerId == npcId).ToList();
        var allocation = new Dictionary<string, int>();
        if (mine.Count == 0 || amount <= 0) return new ReinforcementPlan(allocation, "Nothing to deploy.");

        var enemies = state.Board.Where(t => t.OwnerId != npcId).ToList();

        var launcher = mine
            .Select(m => (Territory: m, Score: enemies.Count == 0
                ? m.Soldiers
                : enemies.Max(e => ScoreTarget(state, npcId, m.Soldiers + amount - 1, e))))
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Territory.Soldiers)
            .First().Territory;

        var forLauncher = _profile switch
        {
            NpcProfile.Aggressive => amount,
            NpcProfile.Defensive => (amount + 1) / 2,
            _ => (int)Math.Ceiling(amount * 2 / 3.0)
        };
        if (mine.Count == 1) forLauncher = amount;

        allocation[launcher.CountryCode] = forLauncher;

        var remaining = amount - forLauncher;
        var weakest = mine.Where(t => t.CountryCode != launcher.CountryCode)
            .OrderBy(t => t.Soldiers)
            .ToList();
        for (var i = 0; remaining > 0 && weakest.Count > 0; i++, remaining--)
        {
            var code = weakest[i % weakest.Count].CountryCode;
            allocation[code] = allocation.GetValueOrDefault(code) + 1;
        }

        string why;
        if (forLauncher == amount)
        {
            why = $"Massing all {amount} reinforcements on {launcher.Name} to build a strike force.";
        }
        else
        {
            var guarded = string.Join(", ", allocation.Keys
                .Where(k => k != launcher.CountryCode)
                .Select(k => state.Board.First(t => t.CountryCode == k).Name));
            why = $"Sending {forLauncher} of {amount} reinforcements to {launcher.Name} for the next attack " +
                  $"and {amount - forLauncher} to protect weaker holdings ({guarded}).";
        }

        return new ReinforcementPlan(allocation, why);
    }


    public AttackDecision PlanAttack(GameState state, string npcId)
    {
        var launchers = state.Board.Where(t => t.OwnerId == npcId && BattleEngine.CanAttack(t.Soldiers)).ToList();
        var enemies = state.Board.Where(t => t.OwnerId != npcId).ToList();

        if (enemies.Count == 0) return AttackDecision.Hold("No enemies remain.");
        if (launchers.Count == 0)
            return AttackDecision.Hold("None of my countries has the 2+ soldiers needed to attack — holding and waiting for reinforcements.");

        Territory? bestFrom = null, bestTo = null;
        double bestScore = double.MinValue, bestRatio = 0;
        Territory? oddsFrom = null, oddsTo = null;
        double topRatio = 0;

        foreach (var from in launchers)
        {
            var committable = from.Soldiers - 1;
            foreach (var target in enemies)
            {
                var ratio = committable / (double)target.Soldiers;
                if (ratio > topRatio)
                {
                    topRatio = ratio;
                    oddsFrom = from;
                    oddsTo = target;
                }

                if (ratio < AttackThreshold) continue;

                var score = ScoreTarget(state, npcId, committable, target);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestFrom = from;
                    bestTo = target;
                    bestRatio = ratio;
                }
            }
        }

        if (bestFrom is null || bestTo is null)
        {
            return AttackDecision.Hold(
                $"My best odds are {oddsFrom!.Name} ({oddsFrom.Soldiers - 1} attackers) vs {oddsTo!.Name} " +
                $"({oddsTo.Soldiers} defenders), {topRatio:0.0}× — below my {AttackThreshold:0.0}× threshold. " +
                "Holding back to build strength.");
        }

        var owner = state.Players.First(p => p.Id == bestTo.OwnerId);
        var reasons = new List<string>
        {
            $"{bestFrom.Soldiers - 1} attackers vs {bestTo.Soldiers} defenders ({bestRatio:0.0}× odds)"
        };
        if (state.Board.Count(t => t.OwnerId == owner.Id) == 1)
            reasons.Add($"it is {owner.Name}'s last country, so capturing it eliminates them");
        if (owner.Id == StrongestRival(state, npcId))
            reasons.Add($"{owner.Name} is the strongest rival and must be checked");
        reasons.Add($"capture is worth {BattleEngine.ConquestReward(bestTo.SoldierValue)} soldiers");

        return new AttackDecision(true, bestFrom.CountryCode, bestTo.CountryCode,
            $"Attacking {bestTo.Name} from {bestFrom.Name}: {string.Join("; ", reasons)}.");
    }

    public ContinueDecision ShouldContinue(int attackerSoldiers, int defenderSoldiers)
    {
        var committable = attackerSoldiers - 1;
        if (committable < 1 || defenderSoldiers <= 0)
            return new ContinueDecision(false, "The battle is decided.");

        if (defenderSoldiers == 1)
            return new ContinueDecision(true, "Only one defender left — pressing on to finish the job.");

        var ratio = committable / (double)defenderSoldiers;
        if (ratio >= ContinueThreshold)
            return new ContinueDecision(true, $"Odds are still {ratio:0.0}× — rolling again.");

        return new ContinueDecision(false,
            $"Odds have dropped to {ratio:0.0}× ({committable} vs {defenderSoldiers}); calling off the attack " +
            $"to keep {attackerSoldiers} soldiers alive.");
    }


    public TransferPlan? PlanTransfer(GameState state, string npcId)
    {
        var mine = state.Board.Where(t => t.OwnerId == npcId).ToList();
        if (mine.Count < 2) return null;

        var weakest = mine.OrderBy(t => t.Soldiers).First();
        var strongest = mine.OrderByDescending(t => t.Soldiers).First();
        if (weakest.CountryCode == strongest.CountryCode) return null;

        var donorMinimum = _profile switch
        {
            NpcProfile.Defensive => 4,
            NpcProfile.Aggressive => 8,
            _ => 6
        };

        if (weakest.Soldiers > 1 || strongest.Soldiers < donorMinimum) return null;

        var amount = Math.Max(1, (strongest.Soldiers - weakest.Soldiers) / 3);
        amount = Math.Min(amount, strongest.Soldiers - 2);
        if (amount < 1) return null;

        return new TransferPlan(strongest.CountryCode, weakest.CountryCode, amount,
            $"Shoring up {weakest.Name} (only {weakest.Soldiers} soldier) with {amount} from {strongest.Name} " +
            "so it isn't an easy target.");
    }


    public TransferPlan? PlanGarrison(GameState state, string npcId, string fromCode, string toCode)
    {
        var from = state.Board.First(t => t.CountryCode == fromCode);
        var to = state.Board.First(t => t.CountryCode == toCode);
        if (from.OwnerId != npcId || to.OwnerId != npcId || from.Soldiers < 3) return null;

        var amount = _profile == NpcProfile.Aggressive
            ? (from.Soldiers - 1) * 2 / 3
            : (from.Soldiers - to.Soldiers) / 2;
        amount = Math.Min(amount, from.Soldiers - 1);
        if (amount < 1) return null;

        var why = _profile == NpcProfile.Aggressive
            ? $"Pushing {amount} soldiers forward into {to.Name} so the offensive can continue from there."
            : $"Garrisoning {to.Name} with {amount} soldiers from {from.Name} so the new conquest can't be easily retaken.";
        return new TransferPlan(fromCode, toCode, amount, why);
    }


    private static double ScoreTarget(GameState state, string npcId, int committable, Territory target)
    {
        if (committable < 1) return double.MinValue;

        var odds = Math.Min(committable / (double)target.Soldiers, 3.0);
        var score = odds * 6
                    + BattleEngine.ConquestReward(target.SoldierValue) * 1.5
                    + Math.Min(target.Soldiers, 40) * 0.25; 

        if (state.Board.Count(t => t.OwnerId == target.OwnerId) == 1) score += 10;    
        if (target.OwnerId == StrongestRival(state, npcId)) score += 5;              
        return score; 
    }

    private static string? StrongestRival(GameState state, string npcId) =>
        state.Board
            .Where(t => t.OwnerId != npcId)
            .GroupBy(t => t.OwnerId)
            .Select(g => (Owner: g.Key, Power: g.Sum(t => t.Soldiers) + g.Count() * 2))
            .OrderByDescending(x => x.Power)
            .Select(x => x.Owner)
            .FirstOrDefault();
}
