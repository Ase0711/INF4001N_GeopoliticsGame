using System.Security.Claims;
using GeopoliticsGame.Core.Abstractions;
using GeopoliticsGame.Core.Domain;

namespace GeopoliticsGame.Web.Services;

public sealed class AccountResult
{
    public User? User { get; private init; }
    public IReadOnlyDictionary<string, string> Errors { get; private init; } = new Dictionary<string, string>();
    public bool Succeeded => User is not null;

    public static AccountResult Success(User user) => new() { User = user };

    public static AccountResult Fail(string field, string message) =>
        new() { Errors = new Dictionary<string, string> { [field] = message } };
}

/// <summary>Registration and credential checks. Sign-in itself (the auth cookie) is done by the controller.</summary>
public sealed class AccountService
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;

    public AccountService(IUserRepository users, IPasswordHasher hasher)
    {
        _users = users;
        _hasher = hasher;
    }

    public async Task<AccountResult> RegisterAsync(string username, string email, string password, CancellationToken ct = default)
    {
        var normalisedUser = username.Trim().ToLowerInvariant();
        var normalisedEmail = email.Trim().ToLowerInvariant();

        if (await _users.UsernameExistsAsync(normalisedUser, ct))
            return AccountResult.Fail("Username", "That username is already taken.");
        if (await _users.EmailExistsAsync(normalisedEmail, ct))
            return AccountResult.Fail("Email", "An account with that email already exists.");

        var user = new User
        {
            Id = Guid.NewGuid().ToString("N"),
            Username = normalisedUser,
            Email = normalisedEmail,
            PasswordHash = _hasher.Hash(password),
            CreatedAt = DateTime.UtcNow
        };
        await _users.CreateAsync(user, ct);
        return AccountResult.Success(user);
    }

    public async Task<AccountResult> ValidateCredentialsAsync(string usernameOrEmail, string password, CancellationToken ct = default)
    {
        var user = await _users.FindByUsernameOrEmailAsync(usernameOrEmail, ct);

        // The same message either way, so the login form can't be used to find out which usernames exist.
        if (user is null || !_hasher.Verify(password, user.PasswordHash))
            return AccountResult.Fail(string.Empty, "Invalid username/email or password.");

        return AccountResult.Success(user);
    }

    public static ClaimsPrincipal CreatePrincipal(User user, string authenticationScheme)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Email, user.Email)
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationScheme));
    }
}

public static class ClaimsPrincipalExtensions
{
    public static string? GetUserId(this ClaimsPrincipal principal) =>
        principal.FindFirstValue(ClaimTypes.NameIdentifier);

    public static string GetUsername(this ClaimsPrincipal principal) =>
        principal.Identity?.Name ?? string.Empty;
}
