using HeadlessCms.Api.Auth.Data;
using HeadlessCms.Api.Content.Models;
using HeadlessCms.Api.Workspaces.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Text.Json;

namespace HeadlessCms.Api.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : AuthDbContext(options)
{
    public DbSet<Workspace> Workspaces { get; set; }
    public DbSet<WorkspaceMembership> WorkspaceMemberships { get; set; }
    public DbSet<WorkspaceInvitation> WorkspaceInvitations { get; set; }
    public DbSet<Project> Projects { get; set; }
    public DbSet<ContentType> ContentTypes { get; set; }
    public DbSet<ContentField> ContentFields { get; set; }
    public DbSet<ContentEntry> ContentEntries { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<WorkspaceMembership>()
            .HasKey(membership => new { membership.WorkspaceId, membership.UserId });

        modelBuilder.Entity<WorkspaceMembership>()
            .Property(membership => membership.Role)
            .HasConversion<string>()
            .HasMaxLength(16);

        modelBuilder.Entity<WorkspaceMembership>()
            .HasIndex(membership => new { membership.WorkspaceId, membership.Role })
            .IsUnique()
            .HasFilter("\"Role\" = 'Owner'");

        modelBuilder.Entity<WorkspaceInvitation>()
            .Property(invitation => invitation.Role)
            .HasConversion<string>()
            .HasMaxLength(16);

        modelBuilder.Entity<WorkspaceInvitation>()
            .HasIndex(invitation => invitation.TokenHash)
            .IsUnique();

        modelBuilder.Entity<WorkspaceInvitation>()
            .Property(invitation => invitation.AcceptedAt)
            .IsConcurrencyToken();

        modelBuilder.Entity<WorkspaceInvitation>()
            .HasOne(invitation => invitation.InvitedByUser)
            .WithMany()
            .HasForeignKey(invitation => invitation.InvitedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<WorkspaceInvitation>()
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
            .HasAlternateKey(project => new { project.WorkspaceId, project.Id });

        modelBuilder.Entity<ContentType>()
            .HasAlternateKey(contentType => new
            {
                contentType.WorkspaceId,
                contentType.ProjectId,
                contentType.Id
            });

        modelBuilder.Entity<ContentType>()
            .HasIndex(contentType => new
            {
                contentType.WorkspaceId,
                contentType.ProjectId,
                contentType.Key
            })
            .IsUnique();

        modelBuilder.Entity<ContentType>()
            .HasOne(contentType => contentType.Workspace)
            .WithMany(workspace => workspace.ContentTypes)
            .HasForeignKey(contentType => contentType.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ContentType>()
            .HasOne(contentType => contentType.Project)
            .WithMany(project => project.ContentTypes)
            .HasForeignKey(contentType => new
            {
                contentType.WorkspaceId,
                contentType.ProjectId
            })
            .HasPrincipalKey(project => new { project.WorkspaceId, project.Id })
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ContentField>()
            .Property(field => field.Type)
            .HasConversion<string>()
            .HasMaxLength(16);

        modelBuilder.Entity<ContentField>()
            .HasIndex(field => new
            {
                field.WorkspaceId,
                field.ProjectId,
                field.ContentTypeId,
                field.Key
            })
            .IsUnique();

        modelBuilder.Entity<ContentField>()
            .HasOne(field => field.ContentType)
            .WithMany(contentType => contentType.Fields)
            .HasForeignKey(field => new
            {
                field.WorkspaceId,
                field.ProjectId,
                field.ContentTypeId
            })
            .HasPrincipalKey(contentType => new
            {
                contentType.WorkspaceId,
                contentType.ProjectId,
                contentType.Id
            })
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ContentEntry>()
            .Property(entry => entry.Status)
            .HasConversion<string>()
            .HasMaxLength(16);

        modelBuilder.Entity<ContentEntry>()
            .HasIndex(entry => new
            {
                entry.WorkspaceId,
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
                entry.WorkspaceId,
                entry.ProjectId,
                entry.ContentTypeId
            })
            .HasPrincipalKey(contentType => new
            {
                contentType.WorkspaceId,
                contentType.ProjectId,
                contentType.Id
            })
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ContentEntry>()
            .HasOne<Workspace>()
            .WithMany(workspace => workspace.ContentEntries)
            .HasForeignKey(entry => entry.WorkspaceId)
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
