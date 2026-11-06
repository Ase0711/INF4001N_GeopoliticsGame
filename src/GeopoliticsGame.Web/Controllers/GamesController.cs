using GeopoliticsGame.Core.Domain;
using GeopoliticsGame.Core.Engine;
using GeopoliticsGame.Web.Models;
using GeopoliticsGame.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GeopoliticsGame.Web.Controllers;


public sealed class GamesController : Controller
{
    private readonly GameService _games;

    public GamesController(GameService games) => _games = games;

    [Authorize]
    public async Task<IActionResult> Index(string? status, string? q, string sort = "recent", CancellationToken ct = default)
    {
        var userId = User.GetUserId()!;
        var all = (await _games.GetUserGamesAsync(userId, ct)).Select(GameSummary.From).ToList();

        IEnumerable<GameSummary> query = all;
        if (Enum.TryParse<GameStatus>(status, ignoreCase: true, out var wanted))
            query = query.Where(g => g.Status == wanted);
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(g => g.CountryName.Contains(q.Trim(), StringComparison.OrdinalIgnoreCase));

        query = sort switch
        {
            "oldest" => query.OrderBy(g => g.CreatedAt),
            "rounds" => query.OrderByDescending(g => g.Round),
            "countries" => query.OrderByDescending(g => g.CountriesHeld).ThenByDescending(g => g.SoldiersHeld),
            _ => query.OrderByDescending(g => g.UpdatedAt)
        };

        var countries = await _games.GetCountriesAsync(ct);
        return View(new DashboardViewModel
        {
            Games = query.ToList(),
            TotalGames = all.Count,
            Status = status,
            Search = q,
            Sort = sort,
            NewGame = new NewGameViewModel { Countries = countries.OrderBy(c => c.Name).ToList() }
        });
    }

    [HttpPost, Authorize]
    public async Task<IActionResult> Create(NewGameViewModel model, CancellationToken ct)
    {
        var countries = await _games.GetCountriesAsync(ct);
        if (ModelState.IsValid && countries.All(c => !string.Equals(c.Code, model.CountryCode, StringComparison.OrdinalIgnoreCase)))
            ModelState.AddModelError(nameof(model.CountryCode), "Please choose a country from the list.");

        if (!ModelState.IsValid)
        {
            TempData["Error"] = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage
                                ?? "Please choose a country.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var game = await _games.CreateAsync(User.GetUserId()!, User.GetUsername(), model.CountryCode!, ct);
            return RedirectToAction(nameof(Play), new { id = game.Id });
        }
        catch (GameRuleException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Index));
        }
    }


    [AllowAnonymous]
    public async Task<IActionResult> Play(string id, CancellationToken ct)
    {
        var game = await _games.GetAsync(id, ct);
        if (game is null) return NotFound();

        var userId = User.GetUserId();
        return View(new PlayViewModel
        {
            GameId = game.Id,
            OwnerUsername = game.OwnerUsername,
            CanPlay = userId is not null && userId == game.OwnerUserId,
            IsSignedIn = userId is not null,
            AiIsLive = _games.CommentaryIsLive
        });
    }
}
