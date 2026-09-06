using HeadlessCms.Api.Content.Services;
using HeadlessCms.Api.Workspaces.Models;
using HeadlessCms.Api.Workspaces.Services;

namespace HeadlessCms.Api.Endpoints.Content;

public sealed class DeleteContentType(
    ContentDefinitionService definitions,
    WorkspaceAccessService workspaceAccess)
    : Endpoint<DeleteContentTypeRequest>
{
    public override void Configure()
    {
        Delete(
            "workspaces/{workspaceId:guid}/projects/{projectId:guid}/" +
            "content-types/{contentTypeKey}");
        Claims("sub");
    }

    public override async Task HandleAsync(DeleteContentTypeRequest request, CancellationToken ct)
    {
        if (await workspaceAccess.ResolveAsync(
                User,
                request.WorkspaceId,
                WorkspaceAccessRoles.Writers,
                ct) is null)
        {
            await Send.ForbiddenAsync(ct);
            return;
        }

        if (!await definitions.DeleteAsync(
                request.WorkspaceId,
                request.ProjectId,
                request.ContentTypeKey,
                ct))
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}

public sealed class DeleteContentTypeRequest
{
    public Guid WorkspaceId { get; init; }
    public Guid ProjectId { get; init; }
    public string ContentTypeKey { get; init; } = default!;
}
