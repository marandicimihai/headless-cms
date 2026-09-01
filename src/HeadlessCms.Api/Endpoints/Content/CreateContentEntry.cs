using System.Text.Json;
using FluentValidation;
using HeadlessCms.Api.Content.Models;
using HeadlessCms.Api.Content.Services;
using HeadlessCms.Api.Workspaces.Models;
using HeadlessCms.Api.Workspaces.Services;

namespace HeadlessCms.Api.Endpoints.Content;

public sealed class CreateContentEntry(
    ContentEntryService entries,
    WorkspaceAccessService workspaceAccess)
    : Endpoint<CreateContentEntryRequest, CreateContentEntryResponse>
{
    public override void Configure()
    {
        Post(
            "workspaces/{workspaceId:guid}/projects/{projectId:guid}/" +
            "content-types/{contentTypeKey}/entries");
        Claims("sub");
    }

    public override async Task HandleAsync(CreateContentEntryRequest request, CancellationToken ct)
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
            var entry = await entries.CreateAsync(
                request.WorkspaceId,
                request.ProjectId,
                request.ContentTypeKey,
                request.Data,
                request.Status,
                ct);

            if (entry is null)
            {
                await Send.NotFoundAsync(ct);
                return;
            }

            var response = ToResponse(entry);
            entry.Dispose();

            await Send.CreatedAtAsync<GetContentEntry>(
                new
                {
                    request.WorkspaceId,
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

    private static CreateContentEntryResponse ToResponse(ContentEntry entry) =>
        new()
        {
            Id = entry.Id,
            Status = entry.Status,
            Data = entry.Data.RootElement.Clone(),
            CreatedAt = entry.CreatedAt,
            UpdatedAt = entry.UpdatedAt
        };
}

public sealed class CreateContentEntryRequest
{
    public Guid WorkspaceId { get; init; }
    public Guid ProjectId { get; init; }
    public string ContentTypeKey { get; init; } = default!;
    public JsonElement Data { get; init; }
    public ContentEntryStatus Status { get; init; } = ContentEntryStatus.Draft;
}

public sealed class CreateContentEntryResponse
{
    public Guid Id { get; init; }
    public required ContentEntryStatus Status { get; init; }
    public JsonElement Data { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public sealed class CreateContentEntryRequestValidator : Validator<CreateContentEntryRequest>
{
    public CreateContentEntryRequestValidator()
    {
        RuleFor(request => request.Data)
            .Must(data => data.ValueKind == JsonValueKind.Object)
            .WithMessage("Data must be a JSON object.");
        RuleFor(request => request.Status)
            .IsInEnum()
            .WithMessage("Status must be draft or published.");
    }
}
