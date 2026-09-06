using HeadlessCms.Api.Auth.Models;
using Microsoft.EntityFrameworkCore;

namespace HeadlessCms.Api.Auth.Data;

public abstract class AuthDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<User> Users { get; set; }
    public DbSet<AuthSession> AuthSessions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>()
            .Property(user => user.PlatformRole)
            .HasConversion<string>()
            .HasMaxLength(32);

        modelBuilder.Entity<User>()
            .HasIndex(user => user.PlatformRole)
            .IsUnique()
            .HasFilter("\"PlatformRole\" = 'PlatformAdmin'");

        modelBuilder.Entity<User>()
            .HasIndex(user => user.Email)
            .IsUnique();

        modelBuilder.Entity<AuthSession>()
            .HasIndex(session => session.SecretHash)
            .IsUnique();

        modelBuilder.Entity<AuthSession>()
            .HasIndex(session => new { session.UserId, session.LastSeenAt });

        modelBuilder.Entity<AuthSession>()
            .HasOne(session => session.User)
            .WithMany()
            .HasForeignKey(session => session.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
