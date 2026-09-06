using HeadlessCms.Api.Content.Services;
using HeadlessCms.Api.Workspaces.Models;
using HeadlessCms.Api.Workspaces.Services;

namespace HeadlessCms.Api.Endpoints.Content;

public sealed class DeleteContentEntry(
    ContentEntryService entries,
    WorkspaceAccessService workspaceAccess)
    : Endpoint<DeleteContentEntryRequest>
{
    public override void Configure()
    {
        Delete(
            "workspaces/{workspaceId:guid}/projects/{projectId:guid}/" +
            "content-types/{contentTypeKey}/entries/{entryId:guid}");
        Claims("sub");
    }

    public override async Task HandleAsync(DeleteContentEntryRequest request, CancellationToken ct)
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

        if (!await entries.DeleteAsync(
                request.WorkspaceId,
                request.ProjectId,
                request.ContentTypeKey,
                request.EntryId,
                ct))
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}

public sealed class DeleteContentEntryRequest
{
    public Guid WorkspaceId { get; init; }
    public Guid ProjectId { get; init; }
    public string ContentTypeKey { get; init; } = default!;
    public Guid EntryId { get; init; }
}
