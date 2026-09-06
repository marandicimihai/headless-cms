using FastEndpoints;
using FluentValidation;
using HeadlessCms.Api.Auth.Models;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Workspaces.Models;
using HeadlessCms.Api.Workspaces.Services;
using Microsoft.EntityFrameworkCore;

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
