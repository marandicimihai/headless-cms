using FastEndpoints;
using HeadlessCms.Api.Content.Services;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Tenancy.Services;
using Microsoft.EntityFrameworkCore;

namespace HeadlessCms.Api.Endpoints.Content;

public sealed class CreateContentEntry(
    ContentEntryService entries,
    ApplicationDbContext db,
    TenantAccessService tenantAccess)
    : Endpoint<CreateContentEntryRequest, ContentEntryResponse>
{
    public override void Configure()
    {
        Post(
            "tenants/{tenantId:guid}/projects/{projectId:guid}/" +
            "content-types/{contentTypeKey}/entries");
        Claims("sub");
    }

    public override async Task HandleAsync(CreateContentEntryRequest request, CancellationToken ct)
    {
        if (await tenantAccess.ResolveAsync(User, request.TenantId, ContentContract.Writers, ct)
            is null)
        {
            await Send.ForbiddenAsync(ct);
            return;
        }

        try
        {
            var entry = await entries.CreateAsync(
                request.TenantId,
                request.ProjectId,
                request.ContentTypeKey,
                request.Data,
                ContentContract.ParseStatus(request.Status),
                ct);

            if (entry is null)
            {
                await Send.NotFoundAsync(ct);
                return;
            }

            var schemaVersion = await db.ContentTypeVersions
                .Where(version =>
                    version.TenantId == request.TenantId &&
                    version.ProjectId == request.ProjectId &&
                    version.Id == entry.ContentTypeVersionId)
                .Select(version => version.Version)
                .SingleAsync(ct);

            var response = ContentContract.ToResponse(entry, schemaVersion);
            entry.Dispose();

            await Send.CreatedAtAsync<GetContentEntry>(
                new
                {
                    request.TenantId,
                    request.ProjectId,
                    request.ContentTypeKey,
                    EntryId = entry.Id
                },
                response,
                cancellation: ct);
        }
        catch (ContentValidationException exception)
        {
            foreach (var error in exception.Errors)
                AddError(error);
            await Send.ErrorsAsync(cancellation: ct);
        }
    }
}

public sealed class GetContentEntry(
    ContentEntryService entries,
    ApplicationDbContext db,
    TenantAccessService tenantAccess)
    : Endpoint<ContentEntryIdRequest, ContentEntryResponse>
{
    public override void Configure()
    {
        Get(
            "tenants/{tenantId:guid}/projects/{projectId:guid}/" +
            "content-types/{contentTypeKey}/entries/{entryId:guid}");
        Claims("sub");
    }

    public override async Task HandleAsync(ContentEntryIdRequest request, CancellationToken ct)
    {
        if (await tenantAccess.ResolveAsync(User, request.TenantId, ContentContract.Readers, ct)
            is null)
        {
            await Send.ForbiddenAsync(ct);
            return;
        }

        var entry = await entries.GetAsync(
            request.TenantId,
            request.ProjectId,
            request.ContentTypeKey,
            request.EntryId,
            ct);

        if (entry is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var schemaVersion = await db.ContentTypeVersions
            .Where(version =>
                version.TenantId == request.TenantId &&
                version.ProjectId == request.ProjectId &&
                version.Id == entry.ContentTypeVersionId)
            .Select(version => version.Version)
            .SingleAsync(ct);
        Response = ContentContract.ToResponse(entry, schemaVersion);
        entry.Dispose();
    }
}

public sealed class UpdateContentEntry(
    ContentEntryService entries,
    ApplicationDbContext db,
    TenantAccessService tenantAccess)
    : Endpoint<UpdateContentEntryRequest, ContentEntryResponse>
{
    public override void Configure()
    {
        Put(
            "tenants/{tenantId:guid}/projects/{projectId:guid}/" +
            "content-types/{contentTypeKey}/entries/{entryId:guid}");
        Claims("sub");
    }

    public override async Task HandleAsync(UpdateContentEntryRequest request, CancellationToken ct)
    {
        if (await tenantAccess.ResolveAsync(User, request.TenantId, ContentContract.Writers, ct)
            is null)
        {
            await Send.ForbiddenAsync(ct);
            return;
        }

        try
        {
            var entry = await entries.UpdateAsync(
                request.TenantId,
                request.ProjectId,
                request.ContentTypeKey,
                request.EntryId,
                request.Data,
                ContentContract.ParseStatus(request.Status),
                ct);

            if (entry is null)
            {
                await Send.NotFoundAsync(ct);
                return;
            }

            var schemaVersion = await db.ContentTypeVersions
                .Where(version =>
                    version.TenantId == request.TenantId &&
                    version.ProjectId == request.ProjectId &&
                    version.Id == entry.ContentTypeVersionId)
                .Select(version => version.Version)
                .SingleAsync(ct);
            Response = ContentContract.ToResponse(entry, schemaVersion);
            entry.Dispose();
        }
        catch (ContentValidationException exception)
        {
            foreach (var error in exception.Errors)
                AddError(error);
            await Send.ErrorsAsync(cancellation: ct);
        }
    }
}

public sealed class DeleteContentEntry(
    ContentEntryService entries,
    TenantAccessService tenantAccess)
    : Endpoint<ContentEntryIdRequest>
{
    public override void Configure()
    {
        Delete(
            "tenants/{tenantId:guid}/projects/{projectId:guid}/" +
            "content-types/{contentTypeKey}/entries/{entryId:guid}");
        Claims("sub");
    }

    public override async Task HandleAsync(ContentEntryIdRequest request, CancellationToken ct)
    {
        if (await tenantAccess.ResolveAsync(User, request.TenantId, ContentContract.Writers, ct)
            is null)
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

public sealed class ListContentEntries(
    ContentEntryService entries,
    ApplicationDbContext db,
    TenantAccessService tenantAccess)
    : Endpoint<ListContentEntriesRequest, ContentEntryPageResponse>
{
    public override void Configure()
    {
        Get(
            "tenants/{tenantId:guid}/projects/{projectId:guid}/" +
            "content-types/{contentTypeKey}/entries");
        Claims("sub");
    }

    public override async Task HandleAsync(ListContentEntriesRequest request, CancellationToken ct)
    {
        if (await tenantAccess.ResolveAsync(User, request.TenantId, ContentContract.Readers, ct)
            is null)
        {
            await Send.ForbiddenAsync(ct);
            return;
        }

        try
        {
            var page = await entries.QueryAsync(
                request.TenantId,
                request.ProjectId,
                request.ContentTypeKey,
                new ContentEntryQuery(
                    ContentContract.ParseFilters(HttpContext.Request.Query),
                    request.Sort,
                    request.Status is null
                        ? null
                        : ContentContract.ParseStatus(request.Status),
                    request.Page,
                    request.PageSize),
                ct);

            if (page is null)
            {
                await Send.NotFoundAsync(ct);
                return;
            }

            var versionIds = page.Items
                .Select(entry => entry.ContentTypeVersionId)
                .Distinct()
                .ToList();
            var versions = await db.ContentTypeVersions
                .Where(version =>
                    version.TenantId == request.TenantId &&
                    version.ProjectId == request.ProjectId &&
                    versionIds.Contains(version.Id))
                .ToDictionaryAsync(version => version.Id, version => version.Version, ct);

            Response = new ContentEntryPageResponse
            {
                Items = page.Items
                    .Select(entry =>
                        ContentContract.ToResponse(
                            entry,
                            versions[entry.ContentTypeVersionId]))
                    .ToList(),
                Total = page.Total,
                Page = page.Page,
                PageSize = page.PageSize
            };

            foreach (var entry in page.Items)
                entry.Dispose();
        }
        catch (ContentValidationException exception)
        {
            foreach (var error in exception.Errors)
                AddError(error);
            await Send.ErrorsAsync(cancellation: ct);
        }
    }
}
