using HeadlessCms.Api.Content.Services;
using HeadlessCms.Api.Tenancy.Models;
using HeadlessCms.Api.Tenancy.Services;

namespace HeadlessCms.Api.Endpoints.Content;

public sealed class DeleteContentEntry(
    ContentEntryService entries,
    TenantAccessService tenantAccess)
    : Endpoint<DeleteContentEntryRequest>
{
    private static readonly IReadOnlySet<TenantRole> Writers =
        new HashSet<TenantRole>([TenantRole.Owner, TenantRole.Editor]);

    public override void Configure()
    {
        Delete(
            "tenants/{tenantId:guid}/projects/{projectId:guid}/" +
            "content-types/{contentTypeKey}/entries/{entryId:guid}");
        Claims("sub");
    }

    public override async Task HandleAsync(DeleteContentEntryRequest request, CancellationToken ct)
    {
        if (await tenantAccess.ResolveAsync(User, request.TenantId, Writers, ct) is null)
        {
            await Send.ForbiddenAsync(ct);
            return;
        }

        if (!await entries.DeleteAsync(
                request.TenantId,
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
    public Guid TenantId { get; init; }
    public Guid ProjectId { get; init; }
    public string ContentTypeKey { get; init; } = default!;
    public Guid EntryId { get; init; }
}
