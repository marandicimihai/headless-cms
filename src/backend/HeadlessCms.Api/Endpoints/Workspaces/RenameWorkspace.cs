using FastEndpoints;
using FluentValidation;
using HeadlessCms.Api.Auth.Models;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Workspaces.Models;
using HeadlessCms.Api.Workspaces.Services;
using Microsoft.EntityFrameworkCore;

using HeadlessCms.Api.Documentation;

namespace HeadlessCms.Api.Endpoints.Workspaces;

public sealed class RenameWorkspace(
    ApplicationDbContext db,
    WorkspaceAccessService workspaceAccess)
    : Endpoint<RenameWorkspaceRequest, RenameWorkspaceResponse>
{
    public override void Configure()
    {
        Patch("workspaces/{workspaceId:guid}");
        Claims("sub");
        Description(b => b.WithTags("Workspaces"), clearDefaults: true);
        Summary((EndpointSummary s) =>
        {
            s.Summary = "Rename a workspace";
            s.Description = "Access: PlatformAdmin or workspace Owner.\n\nTrims the name; maximum 100 characters. Missing or inaccessible workspaces return 404. currentRole is null for PlatformAdmin requests.";
            s.Params["WorkspaceId"] = "Workspace UUID.";
            s.Params["Name"] = "Display name; whitespace is trimmed. Maximum 100 characters.";
            s.ExampleRequest = new { Name = "Editorial" };
            s.Response<RenameWorkspaceResponse>(200, "Success.");
            s.ResponseExamples[200] = ApiExamples.Workspace;
            s.Response<FastEndpoints.ProblemDetails>(400, "Invalid request or validation failure.", "application/problem+json");
            s.Response<ApiProblem>(401, "Authentication is required, or the session has expired.", "application/problem+json");
            s.Response(404, "Resource not found or inaccessible.");
        });
    }

    public override async Task HandleAsync(RenameWorkspaceRequest request, CancellationToken ct)
    {
        WorkspaceRole? currentRole = null;
        if (!User.IsInRole(nameof(PlatformRole.PlatformAdmin)))
        {
            var access = await workspaceAccess.ResolveAsync(
                User,
                request.WorkspaceId,
                WorkspaceAccessRoles.Owners,
                ct);
            if (access is null)
            {
                await Send.NotFoundAsync(ct);
                return;
            }

            currentRole = access.Role;
        }

        var workspace = await db.Workspaces.SingleOrDefaultAsync(
            candidate => candidate.Id == request.WorkspaceId,
            ct);
        if (workspace is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        workspace.Name = request.Name.Trim();
        await db.SaveChangesAsync(ct);
        Response = new RenameWorkspaceResponse(
            workspace.Id,
            workspace.Name,
            workspace.CreatedAt,
            currentRole);
    }
}

public sealed class RenameWorkspaceRequest
{
    public Guid WorkspaceId { get; init; }
    public required string Name { get; init; }
}

public sealed class RenameWorkspaceRequestValidator : Validator<RenameWorkspaceRequest>
{
    public RenameWorkspaceRequestValidator() =>
        RuleFor(request => request.Name).NotEmpty().MaximumLength(100);
}

public sealed record RenameWorkspaceResponse(
    Guid Id,
    string Name,
    DateTime CreatedAt,
    WorkspaceRole? CurrentRole);
