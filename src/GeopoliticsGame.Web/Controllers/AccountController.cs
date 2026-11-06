using GeopoliticsGame.Web.Models;
using GeopoliticsGame.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GeopoliticsGame.Web.Controllers;

public sealed class AccountController : Controller
{
    private readonly AccountService _accounts;

    public AccountController(AccountService accounts) => _accounts = accounts;

    [HttpGet, AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToLocal(returnUrl);
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost, AllowAnonymous, EnableRateLimiting("auth")]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(model);

        var result = await _accounts.ValidateCredentialsAsync(model.UsernameOrEmail, model.Password, ct);
        if (!result.Succeeded)
        {
            AddErrors(result);
            return View(model);
        }

        await SignInAsync(result, model.RememberMe);
        return RedirectToLocal(model.ReturnUrl);
    }

    [HttpGet, AllowAnonymous]
    public IActionResult Register(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToLocal(returnUrl);
        return View(new RegisterViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost, AllowAnonymous, EnableRateLimiting("auth")]
    public async Task<IActionResult> Register(RegisterViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(model);

        var result = await _accounts.RegisterAsync(model.Username, model.Email, model.Password, ct);
        if (!result.Succeeded)
        {
            AddErrors(result);
            return View(model);
        }

        await SignInAsync(result, persistent: true);
        TempData["Toast"] = $"Welcome, {result.User!.Username}! Pick a country to start your first campaign.";
        return RedirectToLocal(model.ReturnUrl);
    }

    [HttpPost, Authorize]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }

    private Task SignInAsync(AccountResult result, bool persistent) =>
        HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            AccountService.CreatePrincipal(result.User!, CookieAuthenticationDefaults.AuthenticationScheme),
            new AuthenticationProperties { IsPersistent = persistent });

    private void AddErrors(AccountResult result)
    {
        foreach (var (field, message) in result.Errors) ModelState.AddModelError(field, message);
    }

    /// <summary>Only redirects to local URLs, to prevent open-redirect attacks.</summary>
    private IActionResult RedirectToLocal(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : RedirectToAction("Index", "Games");
}
