using GeopoliticsGame.Web.Filters;
using GeopoliticsGame.Web.Models;
using GeopoliticsGame.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GeopoliticsGame.Web.Controllers;


[ApiController]
[Route("api/games/{id}")]
[ServiceFilter(typeof(ApiExceptionFilter))]
public sealed class GameApiController : ControllerBase
{
    private readonly GameService _games;

    public GameApiController(GameService games) => _games = games;

    [HttpGet, AllowAnonymous]
    public async Task<IActionResult> Get(string id, CancellationToken ct)
    {
        var game = await _games.GetAsync(id, ct) ?? throw new GameNotFoundException();
        var userId = User.GetUserId();
        return Ok(new
        {
            game,
            canPlay = userId is not null && userId == game.OwnerUserId,
            aiIsLive = _games.CommentaryIsLive
        });
    }

    [HttpPost("deploy"), Authorize]
    public async Task<IActionResult> Deploy(string id, DeployRequest request, CancellationToken ct) =>
        Ok(await _games.DeployAsync(id, UserId, request.Allocation, ct));

    [HttpPost("transfer"), Authorize]
    public async Task<IActionResult> Transfer(string id, TransferRequest request, CancellationToken ct) =>
        Ok(await _games.TransferAsync(id, UserId, request.FromCode, request.ToCode, request.Amount, ct));

    [HttpPost("attack"), Authorize]
    public async Task<IActionResult> Attack(string id, AttackRequest request, CancellationToken ct) =>
        Ok(await _games.StartAttackAsync(id, UserId, request.FromCode, request.ToCode, ct));

    [HttpPost("roll"), Authorize]
    public async Task<IActionResult> Roll(string id, CancellationToken ct) =>
        Ok(await _games.RollAsync(id, UserId, ct));

    [HttpPost("retreat"), Authorize]
    public async Task<IActionResult> Retreat(string id, CancellationToken ct) =>
        Ok(await _games.RetreatAsync(id, UserId, ct));

    [HttpPost("end-turn"), Authorize]
    public async Task<IActionResult> EndTurn(string id, CancellationToken ct) =>
        Ok(await _games.EndTurnAsync(id, UserId, ct));

    [HttpPost("npc-turns"), Authorize]
    public async Task<IActionResult> NpcTurns(string id, CancellationToken ct) =>
        Ok(await _games.RunNpcTurnsAsync(id, UserId, ct));

    private string UserId => User.GetUserId() ?? throw new GameAccessDeniedException();
}
