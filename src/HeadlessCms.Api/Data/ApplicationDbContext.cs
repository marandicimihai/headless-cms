using HeadlessCms.Api.Auth.Data;
using HeadlessCms.Api.Models;
using HeadlessCms.Api.Tenancy.Models;
using Microsoft.EntityFrameworkCore;

namespace HeadlessCms.Api.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : AuthDbContext(options)
{
    public DbSet<Tenant> Tenants { get; set; }
    public DbSet<TenantMembership> TenantMemberships { get; set; }
    public DbSet<TenantInvitation> TenantInvitations { get; set; }
    public DbSet<Project> Projects { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TenantMembership>()
            .HasKey(membership => new { membership.TenantId, membership.UserId });

        modelBuilder.Entity<TenantMembership>()
            .Property(membership => membership.Role)
            .HasConversion<string>()
            .HasMaxLength(16);

        modelBuilder.Entity<TenantMembership>()
            .HasIndex(membership => new { membership.TenantId, membership.Role })
            .IsUnique()
            .HasFilter("\"Role\" = 'Owner'");

        modelBuilder.Entity<TenantInvitation>()
            .Property(invitation => invitation.Role)
            .HasConversion<string>()
            .HasMaxLength(16);

        modelBuilder.Entity<TenantInvitation>()
            .HasIndex(invitation => invitation.TokenHash)
            .IsUnique();

        modelBuilder.Entity<TenantInvitation>()
            .Property(invitation => invitation.AcceptedAt)
            .IsConcurrencyToken();

        modelBuilder.Entity<TenantInvitation>()
            .HasOne(invitation => invitation.InvitedByUser)
            .WithMany()
            .HasForeignKey(invitation => invitation.InvitedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<TenantInvitation>()
            .HasOne(invitation => invitation.AcceptedByUser)
            .WithMany()
            .HasForeignKey(invitation => invitation.AcceptedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
