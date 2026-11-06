using System.Diagnostics;
using GeopoliticsGame.Core.Domain;
using GeopoliticsGame.Web.Models;
using GeopoliticsGame.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace GeopoliticsGame.Web.Controllers;

public sealed class HomeController : Controller
{
    private readonly GameService _games;

    public HomeController(GameService games) => _games = games;

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var countries = await _games.GetCountriesAsync(ct);
        var live = (await _games.GetAllGamesAsync(ct))
            .Where(g => g.Status == GameStatus.Active)
            .Take(6)
            .Select(GameSummary.From)
            .ToList();

        return View(new HomeViewModel
        {
            NewGame = new NewGameViewModel { Countries = countries.OrderBy(c => c.Name).ToList() },
            LiveGames = live
        });
    }

    public IActionResult Rules() => View();

    [Route("/Home/Status/{code:int}")]
    public IActionResult Status(int code)
    {
        Response.StatusCode = code;
        return View("StatusCode", code);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
