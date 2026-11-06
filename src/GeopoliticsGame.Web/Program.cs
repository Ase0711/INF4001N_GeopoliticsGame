using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using GeopoliticsGame.Core.Abstractions;
using GeopoliticsGame.Core.Engine;
using GeopoliticsGame.Web.Filters;
using GeopoliticsGame.Web.Infrastructure;
using GeopoliticsGame.Web.Services;
using GeopoliticsGame.Web.Services.Commentary;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

using var startupLoggerFactory = LoggerFactory.Create(b => b.AddConsole().AddDebug()); // Debug = Visual Studio's Output pane
var startupLogger = startupLoggerFactory.CreateLogger("Startup");

// ── Data layer (MongoDB, or the in-memory fallback) ─────────────────────────
builder.Services.AddGameDatabase(builder.Configuration, startupLogger);

// ── Domain + application services ───────────────────────────────────────────
builder.Services.AddSingleton<IDice, SecureDice>();
builder.Services.AddSingleton<GameLocks>();
builder.Services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
builder.Services.AddScoped<AccountService>();
builder.Services.AddScoped<GameService>();
builder.Services.AddScoped<AnalyticsService>();
builder.Services.AddScoped<ApiExceptionFilter>();

// ── AI commentary (LLM with offline fallback) ───────────────────────────────
builder.Services.Configure<AiOptions>(builder.Configuration.GetSection(AiOptions.SectionName));
builder.Services.AddSingleton<TemplateCommentaryService>();
builder.Services.AddHttpClient<ICommentaryService, LlmCommentaryService>(client =>
    client.Timeout = TimeSpan.FromSeconds(30));

// ── Authentication & authorisation ──────────────────────────────────────────
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/Login";
        options.Cookie.Name = "geo.auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;

        // API calls get a JSON 401/403 instead of a redirect to the login page.
        options.Events.OnRedirectToLogin = ctx => ApiAwareRedirect(ctx.HttpContext, ctx.RedirectUri, 401,
            "Please sign in to play.");
        options.Events.OnRedirectToAccessDenied = ctx => ApiAwareRedirect(ctx.HttpContext, ctx.RedirectUri, 403,
            "You are not allowed to do that.");
    });
builder.Services.AddAuthorization();

// ── MVC, JSON and validation ────────────────────────────────────────────────
builder.Services.AddAntiforgery(o => o.HeaderName = "X-CSRF-TOKEN");
builder.Services
    .AddControllersWithViews(o => o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()))
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    })
    .ConfigureApiBehaviorOptions(o => o.InvalidModelStateResponseFactory = ctx =>
    {
        var first = ctx.ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage;
        return new BadRequestObjectResult(new { error = string.IsNullOrWhiteSpace(first) ? "Invalid request." : first });
    });

// ── Brute-force protection for sign-in / sign-up ────────────────────────────
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("auth", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

var app = builder.Build();

// ── HTTP pipeline ───────────────────────────────────────────────────────────
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/Home/Status/{0}");

app.Use(async (ctx, next) =>
{
    var headers = ctx.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    headers["Content-Security-Policy"] =
        "default-src 'self'; img-src 'self' data:; style-src 'self' 'unsafe-inline'; script-src 'self'; " +
        "connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
    await next();
});

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

static Task ApiAwareRedirect(HttpContext http, string redirectUri, int status, string message)
{
    if (http.Request.Path.StartsWithSegments("/api"))
    {
        http.Response.StatusCode = status;
        return http.Response.WriteAsJsonAsync(new { error = message });
    }
    http.Response.Redirect(redirectUri);
    return Task.CompletedTask;
}

/// <summary>Exposed for integration tests (WebApplicationFactory).</summary>
public partial class Program;
