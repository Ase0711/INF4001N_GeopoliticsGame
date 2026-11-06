using GeopoliticsGame.Core.Abstractions;
using GeopoliticsGame.Core.Domain;

namespace GeopoliticsGame.Core.Engine;


public sealed class NpcTurnRunner
{
    private const int MaxRoundsPerBattle = 200;
    private readonly ICommentaryService _commentary;

    public NpcTurnRunner(ICommentaryService commentary)
    {
        _commentary = commentary ?? throw new ArgumentNullException(nameof(commentary));
    }

    public async Task PlayTurnAsync(GameEngine engine, CancellationToken ct = default)
    {
        var npc = engine.CurrentPlayer;
        if (npc.Type != PlayerType.Npc) throw new InvalidOperationException("It is not an NPC's turn.");

        var state = engine.State;
        var strategy = new NpcStrategy(npc.Profile);

        if (npc.ReinforcementsPending > 0)
        {
            var plan = strategy.PlanReinforcements(state, npc.Id, npc.ReinforcementsPending);
            engine.RecordDecision(npc.Id, plan.Justification);
            engine.Deploy(npc.Id, plan.Allocation);
        }

  
        var decision = strategy.PlanAttack(state, npc.Id);
        engine.RecordDecision(npc.Id, decision.Justification);

        if (decision.Attack)
        {
            var from = engine.GetTerritory(decision.FromCode!);
            var to = engine.GetTerritory(decision.ToCode!);
            var defenderName = engine.GetPlayer(to.OwnerId).Name;

            engine.StartAttack(npc.Id, from.CountryCode, to.CountryCode);

            var rounds = new List<BattleRound>();
            var outcome = AttackOutcome.Continue;
            var retreat = false;

            for (var i = 0; i < MaxRoundsPerBattle; i++)
            {
                var result = engine.RollAttack(npc.Id);
                rounds.Add(result.Round);
                outcome = result.Outcome;
                if (outcome != AttackOutcome.Continue) break;

                var keepGoing = strategy.ShouldContinue(from.Soldiers, to.Soldiers);
                if (!keepGoing.Continue)
                {
                    engine.RecordDecision(npc.Id, keepGoing.Justification);
                    retreat = true;
                    break;
                }
            }

            var outcomeText = outcome switch
            {
                AttackOutcome.Conquered => $"{npc.Name} conquered {to.Name}",
                AttackOutcome.Repelled => $"{to.Name} repelled the attack",
                _ => $"{npc.Name} retreated"
            };
            await Narrate(engine, new BattleSummaryContext(
                npc.Name, from.Name, defenderName, to.Name, rounds, outcomeText), ct);

            if (retreat || outcome == AttackOutcome.Continue)
            {
                if (engine.State.PendingAttack is not null) engine.Retreat(npc.Id); // ends the turn
                return;
            }

            if (!engine.IsActive) return;

            if (outcome == AttackOutcome.Conquered)
            {
                var garrison = strategy.PlanGarrison(state, npc.Id, from.CountryCode, to.CountryCode);
                if (garrison is not null)
                {
                    engine.RecordDecision(npc.Id, garrison.Justification);
                    engine.Transfer(npc.Id, garrison.FromCode, garrison.ToCode, garrison.Amount);
                }
            }
        }


        var transfer = strategy.PlanTransfer(state, npc.Id);
        if (transfer is not null)
        {
            engine.RecordDecision(npc.Id, transfer.Justification);
            engine.Transfer(npc.Id, transfer.FromCode, transfer.ToCode, transfer.Amount);
        }

 
        engine.EndTurn(npc.Id);
    }

    private async Task Narrate(GameEngine engine, BattleSummaryContext context, CancellationToken ct)
    {
        string text;
        try
        {
            text = await _commentary.DescribeBattleAsync(context, ct);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            text = $"{context.Outcome} after {context.Rounds.Count} rounds of fighting.";
        }

        if (!string.IsNullOrWhiteSpace(text)) engine.RecordCommentary(text);
    }
}
