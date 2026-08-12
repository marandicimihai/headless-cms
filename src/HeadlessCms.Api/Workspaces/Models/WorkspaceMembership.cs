using HeadlessCms.Api.Auth.Models;

namespace HeadlessCms.Api.Workspaces.Models;

public class WorkspaceMembership
{
    public Guid WorkspaceId { get; set; }
    public Workspace Workspace { get; set; } = default!;

    public string UserId { get; set; } = default!;
    public User User { get; set; } = default!;

    public WorkspaceRole Role { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}
