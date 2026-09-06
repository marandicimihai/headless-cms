using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using HeadlessCms.Api.Content.Models;

namespace HeadlessCms.Api.Workspaces.Models;

public class Workspace
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = default!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<WorkspaceMembership> Memberships { get; set; } = [];
    public List<WorkspaceInvitation> Invitations { get; set; } = [];
    public List<Project> Projects { get; set; } = [];
    public List<ContentType> ContentTypes { get; set; } = [];
    public List<ContentEntry> ContentEntries { get; set; } = [];
}
