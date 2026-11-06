using GeopoliticsGame.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace GeopoliticsGame.Web.Controllers;

/// <summary>Platform analytics are public; personal analytics show when signed in.</summary>
public sealed class AnalyticsController : Controller
{
    private readonly AnalyticsService _analytics;

    public AnalyticsController(AnalyticsService analytics) => _analytics = analytics;

    public async Task<IActionResult> Index(CancellationToken ct) =>
        View(await _analytics.BuildAsync(User.GetUserId(), ct));
}
