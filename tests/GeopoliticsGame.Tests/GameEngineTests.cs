using GeopoliticsGame.Core.Data;
using GeopoliticsGame.Core.Domain;
using GeopoliticsGame.Core.Engine;

namespace GeopoliticsGame.Tests;

public class GameEngineTests
{
    private const string Human = GameEngine.HumanId;

    [Fact]
    public void New_game_has_ten_unique_countries_and_players()
    {
        var s = TestGames.NewGame("ZAF");

        Assert.Equal(10, s.Board.Count);
        Assert.Equal(10, s.Players.Count);
        Assert.Equal(10, s.Board.Select(t => t.CountryCode).Distinct().Count());
        Assert.Equal("ZAF", TestGames.Human(s).CountryCode);
        Assert.Equal(5, TestGames.Human(s).Soldiers);
        Assert.Equal(s.Players.Select(p => p.Id).OrderBy(x => x), s.TurnOrder.OrderBy(x => x));
        Assert.All(s.Board, t => Assert.Equal(t.SoldierValue, t.Soldiers));
    }

    [Fact]
    public void Unknown_country_is_rejected() =>
        Assert.Throws<GameRuleException>(() =>
            GameEngine.CreateGame("u", "tester", "XXX", CountryCatalogue.All, new SecureDice()));

    [Fact]
    public void Only_the_current_player_may_act()
    {
        var s = TestGames.NewGameHumanFirst();
        var engine = new GameEngine(s, new SecureDice());
        var npc = s.TurnOrder[1];

        Assert.Throws<GameRuleException>(() => engine.EndTurn(npc));
    }

    [Fact]
    public void Only_one_attack_per_turn()
    {
        var s = TestGames.NewGameHumanFirst();
        var home = TestGames.Human(s);
        home.Soldiers = 30;
        var (a, b) = (TestGames.Enemy(s, 0), TestGames.Enemy(s, 1));
        a.Soldiers = 1; // one die each, scripted so the defender wins the round
        var engine = new GameEngine(s, new ScriptedDice(new[] { 1, 6 }));

        engine.StartAttack(Human, home.CountryCode, a.CountryCode);
        engine.RollAttack(Human);
        engine.Retreat(Human); // ends the turn

        // Back on our turn later, the flag must be reset; in the same turn it must block.
        s.CurrentTurnIndex = 0;
        s.HasAttackedThisTurn = true;
        Assert.Throws<GameRuleException>(() => engine.StartAttack(Human, home.CountryCode, b.CountryCode));
    }

    [Fact]
    public void Retreat_ends_the_turn()
    {
        var s = TestGames.NewGameHumanFirst();
        TestGames.Human(s).Soldiers = 10;
        TestGames.Enemy(s).Soldiers = 1; // one die each, scripted so the defender wins the round
        var engine = new GameEngine(s, new ScriptedDice(new[] { 1, 6 }));

        engine.StartAttack(Human, TestGames.Human(s).CountryCode, TestGames.Enemy(s).CountryCode);
        engine.RollAttack(Human);
        engine.Retreat(Human);

        Assert.NotEqual(Human, engine.CurrentPlayerId);
        Assert.Null(s.PendingAttack);
    }

    [Fact]
    public void Attacker_needs_two_soldiers()
    {
        var s = TestGames.NewGameHumanFirst();
        TestGames.Human(s).Soldiers = 1;
        var engine = new GameEngine(s, new SecureDice());

        Assert.Throws<GameRuleException>(() =>
            engine.StartAttack(Human, TestGames.Human(s).CountryCode, TestGames.Enemy(s).CountryCode));
    }

    [Fact]
    public void Cannot_attack_own_country()
    {
        var s = TestGames.NewGameHumanFirst();
        var other = TestGames.Enemy(s);
        other.OwnerId = Human;
        TestGames.Human(s).Soldiers = 5;
        var engine = new GameEngine(s, new SecureDice());

        Assert.Throws<GameRuleException>(() =>
            engine.StartAttack(Human, TestGames.Human(s).CountryCode, other.CountryCode));
    }

    [Fact]
    public void Transfers_keep_one_soldier_and_are_blocked_during_attacks()
    {
        var s = TestGames.NewGameHumanFirst();
        var home = s.Board.First(t => t.OwnerId == Human);
        var other = TestGames.Enemy(s, 0);
        other.OwnerId = Human;
        home.Soldiers = 4;
        other.Soldiers = 1;
        var engine = new GameEngine(s, new ScriptedDice(new[] { 1, 6 }));

        Assert.Throws<GameRuleException>(() => engine.Transfer(Human, home.CountryCode, other.CountryCode, 4));
        engine.Transfer(Human, home.CountryCode, other.CountryCode, 3);
        Assert.Equal(1, home.Soldiers);
        Assert.Equal(4, other.Soldiers);

        engine.StartAttack(Human, other.CountryCode, TestGames.Enemy(s, 1).CountryCode);
        Assert.Throws<GameRuleException>(() => engine.Transfer(Human, other.CountryCode, home.CountryCode, 1));
    }

    [Fact]
    public void Conquest_transfers_ownership_and_grants_reward()
    {
        var s = TestGames.NewGameHumanFirst();
        var home = TestGames.Human(s);
        var target = TestGames.Enemy(s);
        home.Soldiers = 6;
        target.Soldiers = 1;
        target.SoldierValue = 9;
        var engine = new GameEngine(s, new ScriptedDice(new[] { 6, 1 }));

        engine.StartAttack(Human, home.CountryCode, target.CountryCode);
        var result = engine.RollAttack(Human);

        Assert.Equal(AttackOutcome.Conquered, result.Outcome);
        Assert.Equal(Human, target.OwnerId);
        Assert.Equal(4, target.Soldiers);   // max(2, 9/2)
        Assert.Equal(6, home.Soldiers);     // no attacker losses this round
        Assert.Null(s.PendingAttack);
    }

    [Fact]
    public void Attack_fails_when_attacker_drops_to_one()
    {
        var s = TestGames.NewGameHumanFirst();
        var home = TestGames.Human(s);
        var target = TestGames.Enemy(s);
        home.Soldiers = 2;
        target.Soldiers = 3;
        var engine = new GameEngine(s, new ScriptedDice(new[] { 1, 6 }));

        engine.StartAttack(Human, home.CountryCode, target.CountryCode);
        var result = engine.RollAttack(Human);

        Assert.Equal(AttackOutcome.Repelled, result.Outcome);
        Assert.Equal(1, home.Soldiers);
        Assert.NotEqual(Human, target.OwnerId);
        Assert.Equal(Human, home.OwnerId); // keeps its own territory
    }

    [Fact]
    public void Reinforcements_are_two_per_country_from_round_two()
    {
        var s = TestGames.NewGameHumanFirst();
        foreach (var t in s.Board.Where(t => t.OwnerId != Human).Take(2)) t.OwnerId = Human; // human holds 3
        var engine = new GameEngine(s, new SecureDice());

        // Round 1: nobody gets reinforcements. Let everyone skip.
        foreach (var _ in s.TurnOrder.ToList()) engine.EndTurn(engine.CurrentPlayerId);

        Assert.Equal(2, s.Round);
        Assert.Equal(Human, engine.CurrentPlayerId);
        Assert.Equal(6, engine.GetPlayer(Human).ReinforcementsPending);
        Assert.Throws<GameRuleException>(() => engine.EndTurn(Human)); // must deploy first

        engine.Deploy(Human, new Dictionary<string, int> { [TestGames.Human(s).CountryCode] = 6 });
        Assert.Equal(0, engine.GetPlayer(Human).ReinforcementsPending);
    }

    [Fact]
    public void Deploying_to_an_enemy_country_is_rejected()
    {
        var s = TestGames.NewGameHumanFirst();
        s.Round = 2;
        s.Players.First(p => p.Id == Human).ReinforcementsPending = 2;
        var engine = new GameEngine(s, new SecureDice());

        Assert.Throws<GameRuleException>(() =>
            engine.Deploy(Human, new Dictionary<string, int> { [TestGames.Enemy(s).CountryCode] = 2 }));
    }

    [Fact]
    public void Eliminated_players_are_skipped_and_conquering_everything_wins()
    {
        var s = TestGames.NewGameHumanFirst();
        var home = TestGames.Human(s);
        home.Soldiers = 500;
        foreach (var t in s.Board.Where(t => t.OwnerId != Human)) t.Soldiers = 1;
        var engine = new GameEngine(s, new ScriptedDice(Enumerable.Repeat(new[] { 6, 1 }, 50).SelectMany(x => x)));

        var victims = s.Board.Where(t => t.OwnerId != Human).ToList();
        foreach (var victim in victims)
        {
            s.HasAttackedThisTurn = false; // test shortcut: many attacks in one turn
            engine.StartAttack(Human, home.CountryCode, victim.CountryCode);
            Assert.Equal(AttackOutcome.Conquered, engine.RollAttack(Human).Outcome);
        }

        Assert.All(s.Players.Where(p => p.Id != Human), p => Assert.True(p.Eliminated));
        Assert.Equal(GameStatus.Won, s.Status);
        Assert.Equal(Human, s.WinnerId);
        Assert.Throws<GameRuleException>(() => engine.EndTurn(Human));
    }

    [Fact]
    public void Losing_the_last_country_ends_the_game()
    {
        var s = TestGames.NewGame();
        var npcId = s.TurnOrder.First(id => id != Human);
        s.CurrentTurnIndex = s.TurnOrder.IndexOf(npcId);
        var npcHome = s.Board.First(t => t.OwnerId == npcId);
        npcHome.Soldiers = 10;
        TestGames.Human(s).Soldiers = 1;
        var engine = new GameEngine(s, new ScriptedDice(new[] { 6, 1 }));

        engine.StartAttack(npcId, npcHome.CountryCode, TestGames.Human(s).CountryCode);
        engine.RollAttack(npcId);

        Assert.True(engine.GetPlayer(Human).Eliminated);
        Assert.Equal(GameStatus.Lost, s.Status);
    }
}
