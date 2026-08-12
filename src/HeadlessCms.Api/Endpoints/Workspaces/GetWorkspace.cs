using FastEndpoints;
using FastEndpoints.Security;
using HeadlessCms.Api.Auth.Models;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Workspaces.Models;
using HeadlessCms.Api.Workspaces.Services;
using Microsoft.EntityFrameworkCore;

namespace HeadlessCms.Api.Endpoints.Workspaces;

public sealed class GetWorkspace(
    ApplicationDbContext db,
    WorkspaceAccessService workspaceAccess)
    : Endpoint<GetWorkspaceRequest, GetWorkspaceResponse>
{
    public override void Configure()
    {
        Get("workspaces/{workspaceId:guid}");
        Claims("sub");
    }

    public override async Task HandleAsync(GetWorkspaceRequest request, CancellationToken ct)
    {
        WorkspaceRole? role = null;
        if (!User.IsInRole(nameof(PlatformRole.PlatformAdmin)))
        {
            var membership = await workspaceAccess.FindMembershipAsync(User, request.WorkspaceId, ct);
            if (membership is null)
            {
                await Send.NotFoundAsync(ct);
                return;
            }

            role = membership.Role;
        }

        var response = await db.Workspaces
            .AsNoTracking()
            .Where(workspace => workspace.Id == request.WorkspaceId)
            .Select(workspace => new GetWorkspaceResponse(
                workspace.Id,
                workspace.Name,
                workspace.CreatedAt,
                role))
            .SingleOrDefaultAsync(ct);

        if (response is null)
            await Send.NotFoundAsync(ct);
        else
            Response = response;
    }
}

public sealed class GetWorkspaceRequest
{
    public Guid WorkspaceId { get; init; }
}

public sealed record GetWorkspaceResponse(
    Guid Id,
    string Name,
    DateTime CreatedAt,
    WorkspaceRole? CurrentRole);
