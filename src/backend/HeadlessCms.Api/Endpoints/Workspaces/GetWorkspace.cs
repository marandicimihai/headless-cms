using FastEndpoints;
using FastEndpoints.Security;
using HeadlessCms.Api.Auth.Models;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Workspaces.Models;
using HeadlessCms.Api.Workspaces.Services;
using Microsoft.EntityFrameworkCore;

using HeadlessCms.Api.Documentation;

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
        Description(b => b.WithTags("Workspaces"), clearDefaults: true);
        Summary((EndpointSummary s) =>
        {
            s.Summary = "Get a workspace";
            s.Description = "Access: PlatformAdmin or workspace Owner, Editor, or Member.\n\nReturns workspace details. currentRole is null for PlatformAdmin requests, even when the administrator is also a member. Missing or inaccessible workspaces return 404.";
            s.Params["WorkspaceId"] = "Workspace UUID.";
            s.Response<GetWorkspaceResponse>(200, "Success.");
            s.ResponseExamples[200] = ApiExamples.Workspace;
            s.Response<FastEndpoints.ProblemDetails>(400, "Invalid request or validation failure.", "application/problem+json");
            s.Response<ApiProblem>(401, "Authentication is required, or the session has expired.", "application/problem+json");
            s.Response(404, "Resource not found or inaccessible.");
        });
    }

    public override async Task HandleAsync(GetWorkspaceRequest request, CancellationToken ct)
    {
        WorkspaceRole? role = null;
        if (!User.IsInRole(nameof(PlatformRole.PlatformAdmin)))
        {
            var access = await workspaceAccess.ResolveAsync(
                User,
                request.WorkspaceId,
                WorkspaceAccessRoles.Members,
                ct);
            if (access is null)
            {
                await Send.NotFoundAsync(ct);
                return;
            }

            role = access.Role;
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
