using GeopoliticsGame.Core.Domain;

namespace GeopoliticsGame.Core.Engine;

public enum AttackOutcome
{
    /// <summary>Both sides still standing; the attacker may roll again or retreat.</summary>
    Continue,

    /// <summary>The defender lost all soldiers and the country changed hands.</summary>
    Conquered,

    /// <summary>The attacker is down to 1 soldier and the attack failed.</summary>
    Repelled
}

public sealed record AttackRoundResult(BattleRound Round, AttackOutcome Outcome, int Reward, int RoundNumber);

/// <summary>
/// Applies every game rule to a <see cref="GameState"/>. All changes to a game go through
/// this class's methods, and each one checks the rules before it changes anything. That
/// keeps the state valid whether the move comes from the human (via the API) or from an NPC.
/// </summary>
public sealed class GameEngine
{
    public const string HumanId = "human";
    public const int CountriesPerGame = 10;
    public const int ReinforcementsPerCountry = 2;
    private const int MaxLogEntries = 1500;

    private static readonly string[] PlayerColors =
    {
        "#4f8cff", "#e63946", "#2a9d8f", "#e9c46a", "#f4a261",
        "#9b5de5", "#00bbf9", "#8ac926", "#ff6392", "#b08968"
    };

    private readonly GameState _state;
    private readonly BattleEngine _battle;

    /// <summary>Raised after every log entry, so callers can capture replay frames.</summary>
    public event Action<LogEntry>? Logged;

    public GameEngine(GameState state, IDice dice)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _battle = new BattleEngine(dice);
    }

    // ───────────────────────────── Queries ─────────────────────────────

    public GameState State => _state;

    public bool IsActive => _state.Status == GameStatus.Active;

    public string CurrentPlayerId => _state.TurnOrder[_state.CurrentTurnIndex];

    public Player CurrentPlayer => GetPlayer(CurrentPlayerId);

    public bool IsHumanTurn => IsActive && CurrentPlayerId == HumanId;

    public Player GetPlayer(string id) =>
        _state.Players.FirstOrDefault(p => p.Id == id)
        ?? throw new GameRuleException("Unknown player.");

    public Territory GetTerritory(string code) =>
        _state.Board.FirstOrDefault(t => string.Equals(t.CountryCode, code, StringComparison.OrdinalIgnoreCase))
        ?? throw new GameRuleException("That country is not part of this game.");

    public IReadOnlyList<Territory> TerritoriesOf(string playerId) =>
        _state.Board.Where(t => t.OwnerId == playerId).ToList();

    // ────────────────────────────── Setup ──────────────────────────────

    /// <summary>
    /// Creates a new game: the chosen country plus 9 random others, one human player and 9 NPCs,
    /// soldiers from population, and a random turn order.
    /// </summary>
    public static GameState CreateGame(
        string ownerUserId,
        string ownerUsername,
        string chosenCountryCode,
        IReadOnlyList<CountryInfo> catalogue,
        IDice dice)
    {
        if (catalogue.Count < CountriesPerGame)
            throw new InvalidOperationException($"The country catalogue needs at least {CountriesPerGame} countries.");

        var chosen = catalogue.FirstOrDefault(c =>
                         string.Equals(c.Code, chosenCountryCode, StringComparison.OrdinalIgnoreCase))
                     ?? throw new GameRuleException("Please choose a country from the list.");

        var rivals = Shuffle(catalogue.Where(c => c.Code != chosen.Code).ToList(), dice)
            .Take(CountriesPerGame - 1)
            .ToList();

        var now = DateTime.UtcNow;
        var state = new GameState
        {
            Id = Guid.NewGuid().ToString("N"),
            OwnerUserId = ownerUserId,
            OwnerUsername = ownerUsername,
            HumanCountryCode = chosen.Code,
            CreatedAt = now,
            UpdatedAt = now
        };

        state.Players.Add(new Player
        {
            Id = HumanId,
            Name = ownerUsername,
            Type = PlayerType.Human,
            UserId = ownerUserId,
            Color = PlayerColors[0],
            HomeCountryCode = chosen.Code
        });
        state.Board.Add(MakeTerritory(chosen, HumanId));

        var profiles = Enum.GetValues<NpcProfile>();
        for (var i = 0; i < rivals.Count; i++)
        {
            var country = rivals[i];
            var npcId = $"npc-{i + 1}";
            state.Players.Add(new Player
            {
                Id = npcId,
                Name = $"{country.Name} AI",
                Type = PlayerType.Npc,
                Profile = profiles[dice.Next(profiles.Length)],
                Color = PlayerColors[(i + 1) % PlayerColors.Length],
                HomeCountryCode = country.Code
            });
            state.Board.Add(MakeTerritory(country, npcId));
        }

        state.TurnOrder = Shuffle(state.Players.Select(p => p.Id).ToList(), dice);
        state.CurrentTurnIndex = 0;

        var first = state.Players.First(p => p.Id == state.TurnOrder[0]);
        var order = string.Join(" → ", state.TurnOrder.Select(id => state.Players.First(p => p.Id == id).Name));
        state.Log.Add(new LogEntry
        {
            Type = LogType.System,
            Actor = "system",
            Message = $"New game! {ownerUsername} commands {chosen.Flag} {chosen.Name} " +
                      $"({state.Board[0].Soldiers} soldiers). Turn order (decided at random): {order}. " +
                      $"{(first.Id == HumanId ? "You move" : first.Name + " moves")} first."
        });

        return state;
    }

    // ───────────────────────────── Commands ────────────────────────────

    /// <summary>Places start-of-turn reinforcements on countries the player owns.</summary>
    public void Deploy(string playerId, IReadOnlyDictionary<string, int> allocation)
    {
        EnsureCanAct(playerId);
        if (_state.PendingAttack is not null)
            throw new GameRuleException("Finish your attack first.");

        var player = GetPlayer(playerId);
        if (player.ReinforcementsPending <= 0)
            throw new GameRuleException("You have no reinforcements to deploy.");

        var placements = new List<(Territory Territory, int Amount)>();
        foreach (var (code, amount) in allocation)
        {
            if (amount < 0) throw new GameRuleException("Reinforcement amounts cannot be negative.");
            if (amount == 0) continue;
            var territory = GetTerritory(code);
            if (territory.OwnerId != playerId)
                throw new GameRuleException($"You can only reinforce your own countries ({territory.Name} isn't yours).");
            placements.Add((territory, amount));
        }

        var total = placements.Sum(p => p.Amount);
        if (total == 0) throw new GameRuleException("Choose at least one soldier to deploy.");
        if (total > player.ReinforcementsPending)
            throw new GameRuleException($"You only have {player.ReinforcementsPending} reinforcements to deploy.");

        foreach (var (territory, amount) in placements) territory.Soldiers += amount;
        player.ReinforcementsPending -= total;

        var detail = string.Join(", ", placements.Select(p => $"{p.Amount} → {p.Territory.Name}"));
        Log(LogType.Deploy, playerId, $"{player.Name} deploys {Plural(total, "reinforcement")}: {detail}.");
    }

    /// <summary>Moves soldiers between two of the player's countries; the source keeps at least 1.</summary>
    public void Transfer(string playerId, string fromCode, string toCode, int amount)
    {
        EnsureCanAct(playerId);
        if (_state.PendingAttack is not null)
            throw new GameRuleException("You cannot move soldiers while an attack is in progress.");

        var from = GetTerritory(fromCode);
        var to = GetTerritory(toCode);
        if (from.CountryCode == to.CountryCode)
            throw new GameRuleException("Choose two different countries.");
        if (from.OwnerId != playerId || to.OwnerId != playerId)
            throw new GameRuleException("You can only move soldiers between countries you control.");
        if (amount < 1)
            throw new GameRuleException("Move at least one soldier.");
        if (from.Soldiers - amount < 1)
            throw new GameRuleException(
                $"{from.Name} must keep at least 1 soldier (you can move up to {from.Soldiers - 1}).");

        from.Soldiers -= amount;
        to.Soldiers += amount;
        Log(LogType.Transfer, playerId,
            $"{GetPlayer(playerId).Name} moves {Plural(amount, "soldier")} from {from.Flag} {from.Name} to {to.Flag} {to.Name}.",
            from.CountryCode, to.CountryCode);
    }

    /// <summary>Starts the player's single attack for this turn.</summary>
    public void StartAttack(string playerId, string fromCode, string toCode)
    {
        EnsureCanAct(playerId);
        var player = GetPlayer(playerId);

        if (player.ReinforcementsPending > 0)
            throw new GameRuleException($"Deploy your {player.ReinforcementsPending} reinforcements before attacking.");
        if (_state.PendingAttack is not null)
            throw new GameRuleException("An attack is already in progress.");
        if (_state.HasAttackedThisTurn)
            throw new GameRuleException("You can only launch one attack per turn.");

        var from = GetTerritory(fromCode);
        var to = GetTerritory(toCode);
        if (from.OwnerId != playerId)
            throw new GameRuleException("You can only attack from a country you control.");
        if (to.OwnerId == playerId)
            throw new GameRuleException("You cannot attack your own country.");
        if (!BattleEngine.CanAttack(from.Soldiers))
            throw new GameRuleException($"{from.Name} needs at least 2 soldiers to attack (it must leave 1 behind).");

        _state.PendingAttack = new PendingAttack
        {
            AttackerId = playerId,
            FromCode = from.CountryCode,
            ToCode = to.CountryCode
        };
        _state.HasAttackedThisTurn = true;

        var defender = GetPlayer(to.OwnerId);
        Log(LogType.Attack, playerId,
            $"⚔️ {player.Name} attacks {to.Flag} {to.Name} ({defender.Name}, {to.Soldiers}) " +
            $"from {from.Flag} {from.Name} ({from.Soldiers}).",
            from.CountryCode, to.CountryCode);
    }

    /// <summary>Rolls one round of the attack that is in progress.</summary>
    public AttackRoundResult RollAttack(string playerId)
    {
        EnsureCanAct(playerId);
        var pending = _state.PendingAttack ?? throw new GameRuleException("There is no attack in progress.");
        if (pending.AttackerId != playerId) throw new GameRuleException("This is not your attack.");

        var attacker = GetPlayer(playerId);
        var from = GetTerritory(pending.FromCode);
        var to = GetTerritory(pending.ToCode);
        var defender = GetPlayer(to.OwnerId);

        var round = _battle.ResolveRound(from.Soldiers, to.Soldiers);
        from.Soldiers = round.AttackerRemaining;
        to.Soldiers = round.DefenderRemaining;
        pending.Rounds++;

        if (playerId == HumanId) _state.Stats.DiceRolled += round.AttackerDice.Count;

        var ties = round.Ties > 0 ? $", {Plural(round.Ties, "tie")}" : string.Empty;
        Log(LogType.Round, playerId,
            $"🎲 Round {pending.Rounds}: {from.Name} [{string.Join(", ", round.AttackerDice)}] vs " +
            $"{to.Name} [{string.Join(", ", round.DefenderDice)}] — {from.Name} −{round.AttackerLosses}, " +
            $"{to.Name} −{round.DefenderLosses}{ties}. ({from.Soldiers} vs {to.Soldiers})",
            from.CountryCode, to.CountryCode, round);

        if (to.Soldiers <= 0)
            return Conquer(attacker, defender, from, to, round, pending.Rounds);

        if (!BattleEngine.CanAttack(from.Soldiers))
        {
            _state.PendingAttack = null;
            if (playerId == HumanId) _state.Stats.BattlesLost++;
            Log(LogType.Repelled, playerId,
                $"🛡️ {to.Name} holds! {attacker.Name} is down to 1 soldier in {from.Name} and the attack fails.",
                from.CountryCode, to.CountryCode);
            return new AttackRoundResult(round, AttackOutcome.Repelled, 0, pending.Rounds);
        }

        return new AttackRoundResult(round, AttackOutcome.Continue, 0, pending.Rounds);
    }

    /// <summary>Calls off the attack. As the rules require, this also ends the player's turn.</summary>
    public void Retreat(string playerId)
    {
        EnsureCanAct(playerId);
        var pending = _state.PendingAttack ?? throw new GameRuleException("There is no attack in progress.");
        if (pending.AttackerId != playerId) throw new GameRuleException("This is not your attack.");

        var from = GetTerritory(pending.FromCode);
        var to = GetTerritory(pending.ToCode);
        _state.PendingAttack = null;
        Log(LogType.Retreat, playerId,
            $"{GetPlayer(playerId).Name} calls off the attack on {to.Name}; {from.Name} keeps {Plural(from.Soldiers, "soldier")}. Turn over.",
            from.CountryCode, to.CountryCode);

        AdvanceTurn();
    }

    /// <summary>Ends (or skips) the player's turn.</summary>
    public void EndTurn(string playerId)
    {
        EnsureCanAct(playerId);
        if (_state.PendingAttack is not null)
            throw new GameRuleException("Keep rolling or call off your attack before ending your turn.");

        var player = GetPlayer(playerId);
        if (player.ReinforcementsPending > 0)
            throw new GameRuleException($"Deploy your {player.ReinforcementsPending} reinforcements before ending your turn.");

        Log(LogType.EndTurn, playerId,
            _state.HasAttackedThisTurn ? $"{player.Name} ends their turn." : $"{player.Name} skips their turn.");
        AdvanceTurn();
    }

    /// <summary>Adds an explanation of an NPC decision to the battle feed.</summary>
    public void RecordDecision(string playerId, string justification) =>
        Log(LogType.Decision, playerId, $"🤖 {GetPlayer(playerId).Name}: {justification}");

    /// <summary>Adds AI commentary to the battle feed.</summary>
    public void RecordCommentary(string text) =>
        Log(LogType.Commentary, "ai", $"🎙️ {text}");

    // ───────────────────────────── Internals ───────────────────────────

    private AttackRoundResult Conquer(
        Player attacker, Player defender, Territory from, Territory to, BattleRound round, int rounds)
    {
        var reward = BattleEngine.ConquestReward(to.SoldierValue);
        to.OwnerId = attacker.Id;
        to.Soldiers = reward;
        _state.PendingAttack = null;

        if (attacker.Id == HumanId)
        {
            _state.Stats.BattlesWon++;
            _state.Stats.CountriesConquered++;
        }

        Log(LogType.Conquer, attacker.Id,
            $"🏴 {attacker.Name} conquers {to.Flag} {to.Name}! It gains {Plural(reward, "soldier")} " +
            $"(max(2, {to.SoldierValue} ÷ 2)).",
            from.CountryCode, to.CountryCode);

        if (_state.Board.All(t => t.OwnerId != defender.Id))
        {
            defender.Eliminated = true;
            defender.ReinforcementsPending = 0;
            Log(LogType.Eliminate, attacker.Id, $"☠️ {defender.Name} has lost every country and is eliminated.");

            if (defender.Id == HumanId)
            {
                Finish(GameStatus.Lost, null);
                Log(LogType.Win, "system", $"Game over — {attacker.Name} wiped out your last country.");
                return new AttackRoundResult(round, AttackOutcome.Conquered, reward, rounds);
            }
        }

        if (_state.Board.All(t => t.OwnerId == attacker.Id))
        {
            Finish(attacker.Id == HumanId ? GameStatus.Won : GameStatus.Lost, attacker.Id);
            Log(LogType.Win, attacker.Id, $"🏆 {attacker.Name} has conquered all {CountriesPerGame} nations and wins the game!");
        }

        return new AttackRoundResult(round, AttackOutcome.Conquered, reward, rounds);
    }

    private void AdvanceTurn()
    {
        _state.PendingAttack = null;
        _state.HasAttackedThisTurn = false;
        if (!IsActive) return;

        var count = _state.TurnOrder.Count;
        for (var step = 0; step < count; step++)
        {
            _state.CurrentTurnIndex = (_state.CurrentTurnIndex + 1) % count;
            if (_state.CurrentTurnIndex == 0)
            {
                _state.Round++;
                Log(LogType.System, "system", $"━━ Round {_state.Round} begins ━━");
            }

            var next = CurrentPlayer;
            if (next.Eliminated) continue;

            GrantReinforcements(next);
            return;
        }
    }

    /// <summary>From round 2 onwards: 2 soldiers for every country controlled.</summary>
    private void GrantReinforcements(Player player)
    {
        if (_state.Round < 2) return;
        var countries = _state.Board.Count(t => t.OwnerId == player.Id);
        var amount = countries * ReinforcementsPerCountry;
        player.ReinforcementsPending = amount;
        Log(LogType.Reinforce, player.Id,
            $"{player.Name} receives {Plural(amount, "reinforcement")} ({ReinforcementsPerCountry} × {Plural(countries, "country", "countries")}).");
    }

    private void Finish(GameStatus status, string? winnerId)
    {
        _state.Status = status;
        _state.WinnerId = winnerId;
        _state.PendingAttack = null;
        _state.FinishedAt = DateTime.UtcNow;
    }

    private void EnsureCanAct(string playerId)
    {
        if (!IsActive) throw new GameRuleException("This game is over.");
        if (CurrentPlayerId != playerId) throw new GameRuleException("It is not your turn.");
    }

    private void Log(
        LogType type, string actor, string message,
        string? fromCode = null, string? toCode = null, BattleRound? round = null)
    {
        var entry = new LogEntry
        {
            At = DateTime.UtcNow,
            Type = type,
            Actor = actor,
            Message = message,
            FromCode = fromCode,
            ToCode = toCode,
            Round = round
        };
        _state.Log.Add(entry);
        if (_state.Log.Count > MaxLogEntries)
            _state.Log.RemoveRange(0, _state.Log.Count - MaxLogEntries);
        _state.UpdatedAt = entry.At;
        Logged?.Invoke(entry);
    }

    private static Territory MakeTerritory(CountryInfo country, string ownerId)
    {
        var value = SoldierAllocator.SoldiersFor(country.Population);
        return new Territory
        {
            CountryCode = country.Code,
            Name = country.Name,
            Flag = country.Flag,
            Population = country.Population,
            SoldierValue = value,
            Soldiers = value,
            OwnerId = ownerId
        };
    }

    private static List<T> Shuffle<T>(List<T> items, IDice dice)
    {
        var list = new List<T>(items);
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = dice.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
        return list;
    }

    private static string Plural(int n, string singular, string? plural = null) =>
        $"{n} {(n == 1 ? singular : plural ?? singular + "s")}";
}
