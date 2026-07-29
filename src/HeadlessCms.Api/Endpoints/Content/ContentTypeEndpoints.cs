using FastEndpoints;
using HeadlessCms.Api.Content.Services;
using HeadlessCms.Api.Tenancy.Services;

namespace HeadlessCms.Api.Endpoints.Content;

public sealed class CreateContentType(
    ContentDefinitionService definitions,
    TenantAccessService tenantAccess)
    : Endpoint<CreateContentTypeRequest, ContentTypeResponse>
{
    public override void Configure()
    {
        Post("tenants/{tenantId:guid}/projects/{projectId:guid}/content-types");
        Claims("sub");
    }

    public override async Task HandleAsync(CreateContentTypeRequest request, CancellationToken ct)
    {
        if (await tenantAccess.ResolveAsync(User, request.TenantId, ContentContract.Writers, ct)
            is null)
        {
            await Send.ForbiddenAsync(ct);
            return;
        }

        try
        {
            var definition = await definitions.CreateAsync(
                request.TenantId,
                request.ProjectId,
                request.Key,
                request.Name.Trim(),
                ContentContract.ToInputs(request.Fields),
                ct);

            await Send.CreatedAtAsync<GetContentType>(
                new
                {
                    request.TenantId,
                    request.ProjectId,
                    ContentTypeKey = request.Key
                },
                ContentContract.ToResponse(definition),
                cancellation: ct);
        }
        catch (ContentNotFoundException)
        {
            await Send.NotFoundAsync(ct);
        }
        catch (ContentConflictException exception)
        {
            await Send.StringAsync(exception.Message, 409, cancellation: ct);
        }
        catch (ContentValidationException exception)
        {
            foreach (var error in exception.Errors)
                AddError(error);
            await Send.ErrorsAsync(cancellation: ct);
        }
    }
}

public sealed class UpdateContentType(
    ContentDefinitionService definitions,
    TenantAccessService tenantAccess)
    : Endpoint<UpdateContentTypeRequest, ContentTypeResponse>
{
    public override void Configure()
    {
        Put(
            "tenants/{tenantId:guid}/projects/{projectId:guid}/" +
            "content-types/{contentTypeKey}");
        Claims("sub");
    }

    public override async Task HandleAsync(UpdateContentTypeRequest request, CancellationToken ct)
    {
        if (await tenantAccess.ResolveAsync(User, request.TenantId, ContentContract.Writers, ct)
            is null)
        {
            await Send.ForbiddenAsync(ct);
            return;
        }

        try
        {
            var definition = await definitions.UpdateAsync(
                request.TenantId,
                request.ProjectId,
                request.ContentTypeKey,
                request.Name.Trim(),
                ContentContract.ToInputs(request.Fields),
                ct);

            if (definition is null)
            {
                await Send.NotFoundAsync(ct);
                return;
            }

            Response = ContentContract.ToResponse(definition);
        }
        catch (ContentValidationException exception)
        {
            foreach (var error in exception.Errors)
                AddError(error);
            await Send.ErrorsAsync(cancellation: ct);
        }
    }
}

public sealed class GetContentType(
    ContentDefinitionService definitions,
    TenantAccessService tenantAccess)
    : Endpoint<ContentTypeKeyRequest, ContentTypeResponse>
{
    public override void Configure()
    {
        Get(
            "tenants/{tenantId:guid}/projects/{projectId:guid}/" +
            "content-types/{contentTypeKey}");
        Claims("sub");
    }

    public override async Task HandleAsync(ContentTypeKeyRequest request, CancellationToken ct)
    {
        if (await tenantAccess.ResolveAsync(User, request.TenantId, ContentContract.Readers, ct)
            is null)
        {
            await Send.ForbiddenAsync(ct);
            return;
        }

        var definition = await definitions.GetCurrentAsync(
            request.TenantId,
            request.ProjectId,
            request.ContentTypeKey,
            ct);

        if (definition is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        Response = ContentContract.ToResponse(definition);
    }
}

public sealed class ListContentTypes(
    ContentDefinitionService definitions,
    TenantAccessService tenantAccess)
    : Endpoint<ProjectContentRequest, IReadOnlyList<ContentTypeResponse>>
{
    public override void Configure()
    {
        Get("tenants/{tenantId:guid}/projects/{projectId:guid}/content-types");
        Claims("sub");
    }

    public override async Task HandleAsync(ProjectContentRequest request, CancellationToken ct)
    {
        if (await tenantAccess.ResolveAsync(User, request.TenantId, ContentContract.Readers, ct)
            is null)
        {
            await Send.ForbiddenAsync(ct);
            return;
        }

        try
        {
            Response = (await definitions.ListCurrentAsync(
                    request.TenantId,
                    request.ProjectId,
                    ct))
                .Select(ContentContract.ToResponse)
                .ToList();
        }
        catch (ContentNotFoundException)
        {
            await Send.NotFoundAsync(ct);
        }
    }
}
