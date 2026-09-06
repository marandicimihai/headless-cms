using FastEndpoints;
using FastEndpoints.Security;
using FluentValidation;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Workspaces.Models;
using HeadlessCms.Api.Workspaces.Services;

namespace HeadlessCms.Api.Endpoints.Workspaces;

public sealed class CreateWorkspace(
    ApplicationDbContext db,
    WorkspaceOwnershipLimitService ownershipLimits)
    : Endpoint<CreateWorkspaceRequest, CreateWorkspaceResponse>
{
    public override void Configure()
    {
        Post("workspaces");
        Claims("sub");
    }

    public override async Task HandleAsync(CreateWorkspaceRequest request, CancellationToken ct)
    {
        var userId = User.ClaimValue("sub")!;
        if (!await ownershipLimits.CanOwnAnotherWorkspaceAsync(userId, ct))
        {
            await ApiErrors.SendAsync(
                HttpContext,
                StatusCodes.Status409Conflict,
                "workspace_limit_reached",
                $"A user can own at most {ownershipLimits.MaximumOwnedWorkspaces} workspaces.",
                ct);
            return;
        }

        var workspace = new Workspace
        {
            Name = request.Name.Trim(),
            Memberships =
            [
                new WorkspaceMembership
                {
                    UserId = userId,
                    Role = WorkspaceRole.Owner
                }
            ]
        };

        db.Workspaces.Add(workspace);
        await db.SaveChangesAsync(ct);

        await Send.ResponseAsync(
            new CreateWorkspaceResponse(
                workspace.Id,
                workspace.Name,
                workspace.CreatedAt,
                WorkspaceRole.Owner),
            StatusCodes.Status201Created,
            ct);
    }
}

public sealed class CreateWorkspaceRequest
{
    public required string Name { get; init; }
}

public sealed class CreateWorkspaceRequestValidator : Validator<CreateWorkspaceRequest>
{
    public CreateWorkspaceRequestValidator()
    {
        RuleFor(request => request.Name).NotEmpty().MaximumLength(100);
    }
}

public sealed record CreateWorkspaceResponse(
    Guid Id,
    string Name,
    DateTime CreatedAt,
    WorkspaceRole CurrentRole);
