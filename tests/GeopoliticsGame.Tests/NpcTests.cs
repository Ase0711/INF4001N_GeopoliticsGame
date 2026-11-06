using GeopoliticsGame.Core.Domain;
using GeopoliticsGame.Core.Engine;

namespace GeopoliticsGame.Tests;

public class NpcStrategyTests
{
    [Fact]
    public void Defensive_npc_holds_without_overwhelming_odds()
    {
        var s = TestGames.NewGame();
        var npc = s.Players.First(p => p.Type == PlayerType.Npc);
        var home = s.Board.First(t => t.OwnerId == npc.Id);
        home.Soldiers = 4; // 3 committable
        foreach (var t in s.Board.Where(t => t.OwnerId != npc.Id)) t.Soldiers = 3;

        var decision = new NpcStrategy(NpcProfile.Defensive).PlanAttack(s, npc.Id);

        Assert.False(decision.Attack);
        Assert.Contains("threshold", decision.Justification);
    }

    [Fact]
    public void Npc_prefers_the_target_it_can_eliminate()
    {
        // Board: me (1 country, 12 soldiers), two big rivals with 4 countries each, and one
        // rival down to its last country. Every enemy country has 4 soldiers.
        var s = TestGames.NewGame();
        var npcs = s.Players.Where(p => p.Type == PlayerType.Npc).Select(p => p.Id).ToList();
        var (me, big1, big2, lone) = (npcs[0], npcs[1], npcs[2], npcs[3]);

        s.Board[0].OwnerId = me;
        s.Board[0].Soldiers = 12;
        s.Board[1].OwnerId = lone;
        for (var i = 2; i <= 5; i++) s.Board[i].OwnerId = big1;
        for (var i = 6; i <= 9; i++) s.Board[i].OwnerId = big2;
        foreach (var t in s.Board.Skip(1)) t.Soldiers = 4;
        foreach (var p in s.Players.Where(p => p.Id != me && p.Id != big1 && p.Id != big2 && p.Id != lone))
            p.Eliminated = true;

        var decision = new NpcStrategy(NpcProfile.Balanced).PlanAttack(s, me);

        Assert.True(decision.Attack);
        Assert.Equal(s.Board[1].CountryCode, decision.ToCode);
        Assert.Contains("eliminates", decision.Justification);
    }

    [Fact]
    public void Npc_garrisons_a_new_conquest()
    {
        var s = TestGames.NewGame();
        var npc = s.Players.First(p => p.Type == PlayerType.Npc).Id;
        var from = s.Board.First(t => t.OwnerId == npc);
        var won = s.Board.First(t => t.OwnerId != npc);
        from.Soldiers = 20;
        won.OwnerId = npc;
        won.Soldiers = 2;

        var plan = new NpcStrategy(NpcProfile.Balanced).PlanGarrison(s, npc, from.CountryCode, won.CountryCode);

        Assert.True(plan is not null);
        Assert.Equal(9, plan!.Amount); // (20 - 2) / 2 → both sides end level-ish
        Assert.True(from.Soldiers - plan.Amount >= 1);
    }

    [Theory]
    [InlineData(NpcProfile.Aggressive)]
    [InlineData(NpcProfile.Balanced)]
    [InlineData(NpcProfile.Defensive)]
    public void Reinforcement_plan_uses_exactly_the_amount_on_own_countries(NpcProfile profile)
    {
        var s = TestGames.NewGame();
        var npc = s.Players.First(p => p.Type == PlayerType.Npc);
        foreach (var t in s.Board.Where(t => t.OwnerId != npc.Id).Take(3)) t.OwnerId = npc.Id;

        var plan = new NpcStrategy(profile).PlanReinforcements(s, npc.Id, 8);

        Assert.Equal(8, plan.Allocation.Values.Sum());
        Assert.All(plan.Allocation.Keys, code => Assert.Equal(npc.Id, s.Board.First(t => t.CountryCode == code).OwnerId));
        Assert.False(string.IsNullOrWhiteSpace(plan.Justification));
    }

    [Fact]
    public void Npc_retreats_when_odds_collapse()
    {
        var decision = new NpcStrategy(NpcProfile.Defensive).ShouldContinue(attackerSoldiers: 3, defenderSoldiers: 6);
        Assert.False(decision.Continue);

        var finish = new NpcStrategy(NpcProfile.Defensive).ShouldContinue(attackerSoldiers: 2, defenderSoldiers: 1);
        Assert.True(finish.Continue);
    }
}

public class SimulationTests
{
    /// <summary>
    /// Plays complete games where all ten players (the "human" included) are driven by the NPC
    /// strategy, checking the board invariants after every turn. Each game must produce a
    /// single winner who owns all ten countries.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public async Task Full_npc_games_finish_with_a_world_conqueror(int seed)
    {
        var s = TestGames.NewGame("BRA", seed);
        var human = s.Players.First(p => p.Id == GameEngine.HumanId);
        human.Type = PlayerType.Npc; // let the AI play our seat too
        human.Profile = NpcProfile.Balanced;

        var engine = new GameEngine(s, new ScriptedDice(Array.Empty<int>(), seed));
        var runner = new NpcTurnRunner(new SilentCommentary());

        var turns = 0;
        while (s.Board.Select(t => t.OwnerId).Distinct().Count() > 1 && turns < 5000)
        {
            if (s.Status != GameStatus.Active)
            {
                // The "human" seat was knocked out, which normally ends the game. Keep the AIs playing
                // to prove someone conquers the world: finish the interrupted turn and carry on.
                s.Status = GameStatus.Active;
                s.WinnerId = null;
                if (s.HasAttackedThisTurn) engine.EndTurn(engine.CurrentPlayerId);
                continue;
            }
            await runner.PlayTurnAsync(engine);
            turns++;

            Assert.Equal(10, s.Board.Count);
            Assert.All(s.Board, t => Assert.True(t.Soldiers >= 1, $"{t.Name} has {t.Soldiers} soldiers"));
            Assert.Null(s.PendingAttack);
            Assert.All(s.Players.Where(p => p.Eliminated), p => Assert.DoesNotContain(s.Board, t => t.OwnerId == p.Id));
        }

        Assert.Single(s.Board.Select(t => t.OwnerId).Distinct());
        Assert.Equal(9, s.Players.Count(p => p.Eliminated));
    }
}
