using System.Text.Json;
using FluentValidation;
using HeadlessCms.Api.Content.Models;
using HeadlessCms.Api.Content.Services;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Workspaces.Models;
using HeadlessCms.Api.Workspaces.Services;
using Microsoft.EntityFrameworkCore;

namespace HeadlessCms.Api.Endpoints.Content;

public sealed class CreateContentEntry(
    ContentEntryService entries,
    ApplicationDbContext db,
    WorkspaceAccessService workspaceAccess)
    : Endpoint<CreateContentEntryRequest, CreateContentEntryResponse>
{
    private static readonly IReadOnlySet<WorkspaceRole> Writers =
        new HashSet<WorkspaceRole>([WorkspaceRole.Owner, WorkspaceRole.Editor]);

    public override void Configure()
    {
        Post(
            "workspaces/{workspaceId:guid}/projects/{projectId:guid}/" +
            "content-types/{contentTypeKey}/entries");
        Claims("sub");
    }

    public override async Task HandleAsync(CreateContentEntryRequest request, CancellationToken ct)
    {
        if (await workspaceAccess.ResolveAsync(User, request.WorkspaceId, Writers, ct) is null)
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
                ParseStatus(request.Status),
                ct);

            if (entry is null)
            {
                await Send.NotFoundAsync(ct);
                return;
            }

            var schemaVersion = await db.ContentTypeVersions
                .Where(version =>
                    version.WorkspaceId == request.WorkspaceId &&
                    version.ProjectId == request.ProjectId &&
                    version.Id == entry.ContentTypeVersionId)
                .Select(version => version.Version)
                .SingleAsync(ct);

            var response = ToResponse(entry, schemaVersion);
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

    internal static bool TryParseStatus(string value) =>
        Enum.TryParse<ContentEntryStatus>(value, true, out _);

    private static ContentEntryStatus ParseStatus(string value) =>
        Enum.Parse<ContentEntryStatus>(value, true);

    private static CreateContentEntryResponse ToResponse(
        ContentEntry entry,
        int schemaVersion) =>
        new()
        {
            Id = entry.Id,
            SchemaVersion = schemaVersion,
            Status = entry.Status.ToString().ToLowerInvariant(),
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
    public string Status { get; init; } = "draft";
}

public sealed class CreateContentEntryResponse
{
    public Guid Id { get; init; }
    public int SchemaVersion { get; init; }
    public required string Status { get; init; }
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
            .Must(CreateContentEntry.TryParseStatus)
            .WithMessage("Status must be draft or published.");
    }
}
