using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using HeadlessCms.Api.Auth.Models;

namespace HeadlessCms.Api.Workspaces.Models;

public class WorkspaceInvitation
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }

    public Guid WorkspaceId { get; set; }
    public Workspace Workspace { get; set; } = default!;

    [Required]
    [StringLength(320)]
    public string Email { get; set; } = default!;

    public WorkspaceRole Role { get; set; }

    [Required]
    [StringLength(64)]
    public string TokenHash { get; set; } = default!;

    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastSentAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public DateTime? AcceptedAt { get; set; }

    public string InvitedByUserId { get; set; } = default!;
    public User InvitedByUser { get; set; } = default!;

    public string? AcceptedByUserId { get; set; }
    public User? AcceptedByUser { get; set; }
}
