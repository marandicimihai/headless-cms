using System.Text.Json;
using FluentValidation;
using HeadlessCms.Api.Content.Models;
using HeadlessCms.Api.Content.Services;
using HeadlessCms.Api.Workspaces.Models;
using HeadlessCms.Api.Workspaces.Services;

namespace HeadlessCms.Api.Endpoints.Content;

public sealed class UpdateContentEntry(
    ContentEntryService entries,
    WorkspaceAccessService workspaceAccess)
    : Endpoint<UpdateContentEntryRequest, UpdateContentEntryResponse>
{
    public override void Configure()
    {
        Put(
            "workspaces/{workspaceId:guid}/projects/{projectId:guid}/" +
            "content-types/{contentTypeKey}/entries/{entryId:guid}");
        Claims("sub");
    }

    public override async Task HandleAsync(UpdateContentEntryRequest request, CancellationToken ct)
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

        try
        {
            var entry = await entries.UpdateAsync(
                request.WorkspaceId,
                request.ProjectId,
                request.ContentTypeKey,
                request.EntryId,
                request.Data,
                request.Status,
                ct);

            if (entry is null)
            {
                await Send.NotFoundAsync(ct);
                return;
            }

            Response = ToResponse(entry);
            entry.Dispose();
        }
        catch (ContentValidationException exception)
        {
            foreach (var error in exception.Errors)
                AddError(error);
            await Send.ErrorsAsync(cancellation: ct);
        }
    }

    private static UpdateContentEntryResponse ToResponse(ContentEntry entry) =>
        new()
        {
            Id = entry.Id,
            Status = entry.Status,
            Data = entry.Data.RootElement.Clone(),
            CreatedAt = entry.CreatedAt,
            UpdatedAt = entry.UpdatedAt
        };
}

public sealed class UpdateContentEntryRequest
{
    public Guid WorkspaceId { get; init; }
    public Guid ProjectId { get; init; }
    public string ContentTypeKey { get; init; } = default!;
    public Guid EntryId { get; init; }
    public JsonElement Data { get; init; }
    public ContentEntryStatus Status { get; init; } = ContentEntryStatus.Draft;
}

public sealed class UpdateContentEntryResponse
{
    public Guid Id { get; init; }
    public required ContentEntryStatus Status { get; init; }
    public JsonElement Data { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public sealed class UpdateContentEntryRequestValidator : Validator<UpdateContentEntryRequest>
{
    public UpdateContentEntryRequestValidator()
    {
        RuleFor(request => request.Data)
            .Must(data => data.ValueKind == JsonValueKind.Object)
            .WithMessage("Data must be a JSON object.");
        RuleFor(request => request.Status)
            .IsInEnum()
            .WithMessage("Status must be draft or published.");
    }
}
