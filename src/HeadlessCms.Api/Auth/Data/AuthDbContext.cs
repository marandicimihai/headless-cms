using FastEndpoints.Security;
using HeadlessCms.Api.Auth.Models;
using Microsoft.EntityFrameworkCore;

namespace HeadlessCms.Api.Auth.Data;

public abstract class AuthDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<User> Users { get; set; }
    public DbSet<RefreshToken> Tokens { get; set; }
}