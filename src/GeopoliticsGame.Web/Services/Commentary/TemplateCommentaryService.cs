using GeopoliticsGame.Core.Abstractions;

namespace GeopoliticsGame.Web.Services.Commentary;

public sealed class TemplateCommentaryService : ICommentaryService
{
    public bool IsLive => false;

    public Task<string> DescribeRoundAsync(RoundContext c, CancellationToken ct = default)
    {
        var r = c.Round;
        var a = c.AttackerCountry;
        var d = c.DefenderCountry;

        string line;
        if (c.Outcome == "conquered")
        {
            line = Pick(
                $"{d} falls! {c.AttackerPlayer}'s banners rise over the capital after {c.RoundNumber} rounds.",
                $"The last defenders of {d} lay down their arms — {a}'s forces march in.",
                $"Total victory at {d}: {c.AttackerPlayer} claims the country.");
        }
        else if (c.Outcome == "repelled")
        {
            line = Pick(
                $"{d} stands firm! {a}'s exhausted army retreats with a single soldier left.",
                $"A heroic defence — {c.DefenderPlayer} throws back the invaders from {a}.",
                $"The assault breaks on {d}'s walls; {a} has nothing left to throw.");
        }
        else if (r.DefenderLosses > r.AttackerLosses)
        {
            line = Pick(
                $"{a} punches through! {d} loses {r.DefenderLosses} and is down to {r.DefenderRemaining}.",
                $"A brutal volley from {a} — {d}'s line is cracking ({r.DefenderRemaining} left).",
                $"The dice favour the invader: {d} reels as {a} presses on.");
        }
        else if (r.AttackerLosses > r.DefenderLosses)
        {
            line = Pick(
                $"{d} bloodies the attackers — {a} loses {r.AttackerLosses} and has {r.AttackerRemaining} left.",
                $"Fierce resistance! {d}'s defenders out-roll {a} this round.",
                $"{a}'s charge falters against {d}'s stubborn garrison.");
        }
        else if (r.Ties == r.AttackerDice.Count)
        {
            line = Pick(
                $"Dead even! Every die ties and not a single soldier falls between {a} and {d}.",
                $"Stalemate — {a} and {d} stare each other down with no losses.");
        }
        else
        {
            line = Pick(
                $"Blow for blow — {a} and {d} trade losses ({r.AttackerRemaining} vs {r.DefenderRemaining}).",
                $"Neither side gives an inch: {a} {r.AttackerRemaining}, {d} {r.DefenderRemaining}.");
        }

        return Task.FromResult(line);
    }

    public Task<string> DescribeBattleAsync(BattleSummaryContext c, CancellationToken ct = default)
    {
        var attackerLosses = c.Rounds.Sum(r => r.AttackerLosses);
        var defenderLosses = c.Rounds.Sum(r => r.DefenderLosses);
        var rounds = c.Rounds.Count;
        var span = rounds == 1 ? "a single round" : $"{rounds} rounds";

        var line = c.Outcome switch
        {
            var o when o.Contains("conquered") => Pick(
                $"{c.AttackerPlayer} storms {c.DefenderCountry} in {span}, losing {attackerLosses} soldiers to {defenderLosses}.",
                $"After {span} of fighting, {c.DefenderCountry} falls to {c.AttackerCountry}'s army."),
            var o when o.Contains("repelled") => Pick(
                $"{c.DefenderCountry} survives {span} of assault — {c.AttackerPlayer} loses {attackerLosses} soldiers for nothing.",
                $"{c.DefenderPlayer} holds {c.DefenderCountry}! The invasion from {c.AttackerCountry} collapses."),
            _ => Pick(
                $"{c.AttackerPlayer} pulls back from {c.DefenderCountry} after {span} ({attackerLosses} lost, {defenderLosses} inflicted).",
                $"A cautious retreat: {c.AttackerCountry} breaks off the attack on {c.DefenderCountry}.")
        };

        return Task.FromResult(line);
    }

    private static string Pick(params string[] options) => options[Random.Shared.Next(options.Length)];
}
