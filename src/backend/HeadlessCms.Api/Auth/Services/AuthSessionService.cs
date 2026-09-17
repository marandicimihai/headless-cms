using System.Security.Cryptography;
using HeadlessCms.Api.Auth.Models;
using HeadlessCms.Api.Data;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace HeadlessCms.Api.Auth.Services;

public sealed record CreatedAuthSession(
    string Secret,
    AuthSession Session);

public sealed record ResolvedAuthSession(
    Guid Id,
    string UserId,
    string Email,
    PlatformRole PlatformRole,
    DateTime IdleExpiresAt,
    DateTime AbsoluteExpiresAt,
    DateTime LastSeenAt);

public sealed class AuthSessionService(
    ApplicationDbContext db,
    TimeProvider timeProvider,
    IWebHostEnvironment environment)
{
    public const string CookieName = "cms_session";
    public const string HttpContextItemName = "HeadlessCms.AuthSession";
    public static readonly TimeSpan IdleLifetime = TimeSpan.FromDays(30);
    public static readonly TimeSpan AbsoluteLifetime = TimeSpan.FromDays(365);
    public static readonly TimeSpan RenewalInterval = TimeSpan.FromHours(24);
    public const int MaximumActiveSessions = 10;

    public async Task<CreatedAuthSession> CreateAsync(
        string userId,
        CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var absoluteExpiry = now.Add(AbsoluteLifetime);
        var secret = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var session = new AuthSession
        {
            UserId = userId,
            SecretHash = TokenHasher.Hash(secret),
            CreatedAt = now,
            LastSeenAt = now,
            IdleExpiresAt = now.Add(IdleLifetime),
            AbsoluteExpiresAt = absoluteExpiry
        };

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        // Serialize session issuance for one user so concurrent logins cannot
        // exceed the active-session limit.
        _ = await db.Users
            .FromSqlInterpolated(
                $"SELECT * FROM \"Users\" WHERE \"Id\" = {userId} FOR UPDATE")
            .AsNoTracking()
            .SingleAsync(ct);

        await db.AuthSessions
            .Where(candidate =>
                candidate.UserId == userId &&
                (candidate.IdleExpiresAt <= now ||
                 candidate.AbsoluteExpiresAt <= now))
            .ExecuteDeleteAsync(ct);

        var activeCount = await db.AuthSessions.CountAsync(
            candidate => candidate.UserId == userId,
            ct);
        var evictionCount = Math.Max(0, activeCount - MaximumActiveSessions + 1);

        if (evictionCount > 0)
        {
            var sessionsToEvict = await db.AuthSessions
                .Where(candidate => candidate.UserId == userId)
                .OrderBy(candidate => candidate.LastSeenAt)
                .ThenBy(candidate => candidate.CreatedAt)
                .ThenBy(candidate => candidate.Id)
                .Take(evictionCount)
                .ToListAsync(ct);
            db.AuthSessions.RemoveRange(sessionsToEvict);
        }

        db.AuthSessions.Add(session);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return new CreatedAuthSession(secret, session);
    }

    public async Task<ResolvedAuthSession?> ResolveAsync(
        string? secret,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(secret))
            return null;

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var secretHash = TokenHasher.Hash(secret);
        var session = await db.AuthSessions
            .AsNoTracking()
            .Where(candidate =>
                candidate.SecretHash == secretHash &&
                candidate.IdleExpiresAt > now &&
                candidate.AbsoluteExpiresAt > now)
            .Select(candidate => new ResolvedAuthSession(
                candidate.Id,
                candidate.UserId,
                candidate.User.Email,
                candidate.User.PlatformRole,
                candidate.IdleExpiresAt,
                candidate.AbsoluteExpiresAt,
                candidate.LastSeenAt))
            .SingleOrDefaultAsync(ct);

        if (session is null)
            return null;

        var renewalCutoff = now.Subtract(RenewalInterval);
        if (session.LastSeenAt > renewalCutoff)
            return session;

        var renewedIdleExpiry = Min(now.Add(IdleLifetime), session.AbsoluteExpiresAt);
        var renewed = await db.AuthSessions
            .Where(candidate =>
                candidate.Id == session.Id &&
                candidate.LastSeenAt <= renewalCutoff &&
                candidate.IdleExpiresAt > now &&
                candidate.AbsoluteExpiresAt > now)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(candidate => candidate.LastSeenAt, now)
                    .SetProperty(candidate => candidate.IdleExpiresAt, renewedIdleExpiry),
                ct);

        return renewed == 0
            ? session
            : session with { IdleExpiresAt = renewedIdleExpiry, LastSeenAt = now };
    }

    public async Task RevokeAsync(string? secret, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(secret))
            return;

        var hash = TokenHasher.Hash(secret);
        await db.AuthSessions
            .Where(session => session.SecretHash == hash)
            .ExecuteDeleteAsync(ct);
    }

    public void AppendCookie(HttpResponse response, CreatedAuthSession created) =>
        response.Cookies.Append(
            CookieName,
            created.Secret,
            CookieOptions(created.Session.AbsoluteExpiresAt));

    public void DeleteCookie(HttpResponse response) =>
        response.Cookies.Delete(
            CookieName,
            CookieOptions(DateTime.UnixEpoch));

    private CookieOptions CookieOptions(DateTime expiresAt) => new()
    {
        HttpOnly = true,
        SameSite = SameSiteMode.Lax,
        Path = "/",
        Secure = !environment.IsDevelopment(),
        Expires = new DateTimeOffset(expiresAt, TimeSpan.Zero)
    };

    private static DateTime Min(DateTime first, DateTime second) =>
        first <= second ? first : second;
}
