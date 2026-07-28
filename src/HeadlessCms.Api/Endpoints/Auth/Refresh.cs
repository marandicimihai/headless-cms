using FastEndpoints.Security;
using HeadlessCms.Api.Auth.Models;
using HeadlessCms.Api.Auth.Services;
using HeadlessCms.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace HeadlessCms.Api.Endpoints.Auth;

public class Refresh : RefreshTokenService<TokenRequest, TokenResponse>
{
    private readonly ApplicationDbContext db;
    private readonly ILogger<Refresh> logger;
    
    public Refresh(IConfiguration config, ILogger<Refresh> logger, ApplicationDbContext db)
    {
        this.db = db;
        this.logger = logger;
        
        Setup(o =>
        {
            var accessTokenLifetime =
                config.GetValue<int?>("Auth:AccessTokenExpirationMinutes")
                ?? throw new InvalidOperationException(
                    "Required configuration 'Auth:AccessTokenExpirationMinutes' is missing.");
            
            var refreshTokenLifetime =
                config.GetValue<int?>("Auth:RefreshTokenExpirationDays")
                ?? throw new InvalidOperationException(
                    "Required configuration 'Auth:RefreshTokenExpirationDays' is missing.");
            
            o.TokenSigningKey = config.GetValue<string>("Auth:SigningKey");
            o.AccessTokenValidity = TimeSpan.FromMinutes(accessTokenLifetime);
            o.RefreshTokenValidity = TimeSpan.FromDays(refreshTokenLifetime);
            
            o.Endpoint("auth/refresh", _ => { });
        });
    }

    public override async Task PersistTokenAsync(TokenResponse response)
    {
        var refreshToken = new RefreshToken
        {
            UserId = response.UserId,
            TokenHash = TokenHasher.Hash(response.RefreshToken),
            Expiry = response.RefreshExpiry
        };
        
        var previousTokens = await db.Tokens
            .Where(token => token.UserId == response.UserId)
            .ToListAsync();

        db.Tokens.RemoveRange(previousTokens);
        db.Tokens.Add(refreshToken);
        
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException exception)
        {
            logger.LogError(exception, "An error occurred while persisting a refresh token");
            throw;
        }
    }

    public override async Task RefreshRequestValidationAsync(TokenRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.RefreshToken))
        {
            AddError(r => r.RefreshToken, "Invalid token");
            return;
        }

        var tokenHash = TokenHasher.Hash(req.RefreshToken);
        var token = await db.Tokens
            .SingleOrDefaultAsync(storedToken => storedToken.TokenHash == tokenHash);

        if (token is null || DateTime.UtcNow >= token.Expiry)
        {
            AddError(r => r.RefreshToken, "Invalid token");
            return;
        }

        if (!await db.Users.AnyAsync(user => user.Id == token.UserId))
        {
            AddError(r => r.RefreshToken, "Invalid token");
            return;
        }

        req.UserId = token.UserId;
    }

    public override async Task SetRenewalPrivilegesAsync(
        TokenRequest request,
        UserPrivileges privileges)
    {
        var user = await db.Users
            .AsNoTracking()
            .SingleAsync(storedUser => storedUser.Id == request.UserId);

        privileges["sub"] = user.Id;
        privileges["username"] = user.Username;
    }
}
