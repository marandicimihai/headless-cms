using System.Data.Common;
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
        
        await db.Tokens.AddAsync(refreshToken);
        db.Tokens.RemoveRange(db.Tokens.Where(t => t.UserId == response.UserId));
        
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbException de)
        {
            logger.LogError(de, "An error occurred while persisting token");
            throw;
        }
    }

    public override async Task RefreshRequestValidationAsync(TokenRequest req)
    {
        var token = await db.Tokens.FirstOrDefaultAsync(t => t.TokenHash == TokenHasher.Hash(req.RefreshToken));

        if (token is null || DateTime.UtcNow > token.Expiry)
        {
            AddError(r => r.RefreshToken, "Invalid Token");
        }
        
        req.UserId = token!.UserId;
    }

    public override Task SetRenewalPrivilegesAsync(TokenRequest request, UserPrivileges privileges)
    {
        return Task.CompletedTask;
    }
}