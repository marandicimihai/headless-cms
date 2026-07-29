using HeadlessCms.Api.Auth.Data;
using HeadlessCms.Api.Content.Models;
using HeadlessCms.Api.Tenancy.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Text.Json;

namespace HeadlessCms.Api.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : AuthDbContext(options)
{
    public DbSet<Tenant> Tenants { get; set; }
    public DbSet<TenantMembership> TenantMemberships { get; set; }
    public DbSet<TenantInvitation> TenantInvitations { get; set; }
    public DbSet<Project> Projects { get; set; }
    public DbSet<ContentType> ContentTypes { get; set; }
    public DbSet<ContentTypeVersion> ContentTypeVersions { get; set; }
    public DbSet<ContentField> ContentFields { get; set; }
    public DbSet<ContentEntry> ContentEntries { get; set; }

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

        ConfigureContentModel(
            modelBuilder,
            Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory");
    }

    private static void ConfigureContentModel(
        ModelBuilder modelBuilder,
        bool configurePortableJsonConverters)
    {
        modelBuilder.Entity<Project>()
            .HasAlternateKey(project => new { project.TenantId, project.Id });

        modelBuilder.Entity<ContentType>()
            .HasAlternateKey(contentType => new
            {
                contentType.TenantId,
                contentType.ProjectId,
                contentType.Id
            });

        modelBuilder.Entity<ContentType>()
            .HasIndex(contentType => new
            {
                contentType.TenantId,
                contentType.ProjectId,
                contentType.Key
            })
            .IsUnique();

        modelBuilder.Entity<ContentType>()
            .HasOne(contentType => contentType.Tenant)
            .WithMany(tenant => tenant.ContentTypes)
            .HasForeignKey(contentType => contentType.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ContentType>()
            .HasOne(contentType => contentType.Project)
            .WithMany(project => project.ContentTypes)
            .HasForeignKey(contentType => new
            {
                contentType.TenantId,
                contentType.ProjectId
            })
            .HasPrincipalKey(project => new { project.TenantId, project.Id })
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ContentTypeVersion>()
            .HasAlternateKey(version => new
            {
                version.TenantId,
                version.ProjectId,
                version.ContentTypeId,
                version.Id
            });

        modelBuilder.Entity<ContentTypeVersion>()
            .HasAlternateKey(version => new
            {
                version.TenantId,
                version.ProjectId,
                version.Id
            });

        modelBuilder.Entity<ContentTypeVersion>()
            .HasIndex(version => new
            {
                version.TenantId,
                version.ProjectId,
                version.ContentTypeId,
                version.Version
            })
            .IsUnique();

        modelBuilder.Entity<ContentTypeVersion>()
            .HasOne(version => version.ContentType)
            .WithMany(contentType => contentType.Versions)
            .HasForeignKey(version => new
            {
                version.TenantId,
                version.ProjectId,
                version.ContentTypeId
            })
            .HasPrincipalKey(contentType => new
            {
                contentType.TenantId,
                contentType.ProjectId,
                contentType.Id
            })
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ContentType>()
            .HasOne(contentType => contentType.CurrentVersion)
            .WithMany()
            .HasForeignKey(contentType => new
            {
                contentType.TenantId,
                contentType.ProjectId,
                contentType.Id,
                contentType.CurrentVersionId
            })
            .HasPrincipalKey(version => new
            {
                version.TenantId,
                version.ProjectId,
                version.ContentTypeId,
                version.Id
            })
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ContentField>()
            .Property(field => field.Type)
            .HasConversion<string>()
            .HasMaxLength(16);

        modelBuilder.Entity<ContentField>()
            .HasIndex(field => new
            {
                field.TenantId,
                field.ProjectId,
                field.ContentTypeVersionId,
                field.Key
            })
            .IsUnique();

        modelBuilder.Entity<ContentField>()
            .HasOne(field => field.ContentTypeVersion)
            .WithMany(version => version.Fields)
            .HasForeignKey(field => new
            {
                field.TenantId,
                field.ProjectId,
                field.ContentTypeVersionId
            })
            .HasPrincipalKey(version => new
            {
                version.TenantId,
                version.ProjectId,
                version.Id
            })
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ContentEntry>()
            .Property(entry => entry.Status)
            .HasConversion<string>()
            .HasMaxLength(16);

        modelBuilder.Entity<ContentEntry>()
            .HasIndex(entry => new
            {
                entry.TenantId,
                entry.ProjectId,
                entry.ContentTypeId,
                entry.Status,
                entry.CreatedAt
            });

        modelBuilder.Entity<ContentEntry>()
            .HasOne(entry => entry.ContentType)
            .WithMany(contentType => contentType.Entries)
            .HasForeignKey(entry => new
            {
                entry.TenantId,
                entry.ProjectId,
                entry.ContentTypeId
            })
            .HasPrincipalKey(contentType => new
            {
                contentType.TenantId,
                contentType.ProjectId,
                contentType.Id
            })
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ContentEntry>()
            .HasOne(entry => entry.ContentTypeVersion)
            .WithMany(version => version.Entries)
            .HasForeignKey(entry => new
            {
                entry.TenantId,
                entry.ProjectId,
                entry.ContentTypeId,
                entry.ContentTypeVersionId
            })
            .HasPrincipalKey(version => new
            {
                version.TenantId,
                version.ProjectId,
                version.ContentTypeId,
                version.Id
            })
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ContentEntry>()
            .HasOne<Tenant>()
            .WithMany(tenant => tenant.ContentEntries)
            .HasForeignKey(entry => entry.TenantId)
            .OnDelete(DeleteBehavior.NoAction);

        if (configurePortableJsonConverters)
            ConfigurePortableJsonConverters(modelBuilder);
    }

    private static void ConfigurePortableJsonConverters(ModelBuilder modelBuilder)
    {
        var documentConverter = new ValueConverter<JsonDocument, string>(
            document => document.RootElement.GetRawText(),
            json => JsonDocument.Parse(json, new JsonDocumentOptions()));
        var documentComparer = new ValueComparer<JsonDocument>(
            (left, right) =>
                left != null &&
                right != null &&
                left.RootElement.GetRawText() == right.RootElement.GetRawText(),
            document => document.RootElement.GetRawText().GetHashCode(),
            document => JsonDocument.Parse(
                document.RootElement.GetRawText(),
                new JsonDocumentOptions()));

        modelBuilder.Entity<ContentEntry>()
            .Property(entry => entry.Data)
            .HasConversion(documentConverter, documentComparer);

        var elementConverter = new ValueConverter<JsonElement, string>(
            element => element.GetRawText(),
            json => JsonDocument.Parse(json, new JsonDocumentOptions()).RootElement.Clone());
        var elementComparer = new ValueComparer<JsonElement>(
            (left, right) => left.GetRawText() == right.GetRawText(),
            element => element.GetRawText().GetHashCode(),
            element => element.Clone());

        modelBuilder.Entity<ContentField>()
            .Property(field => field.Settings)
            .HasConversion(elementConverter, elementComparer);
    }
}
