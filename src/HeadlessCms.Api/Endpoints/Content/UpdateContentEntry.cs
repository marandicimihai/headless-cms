using System.Text.Json;
using FluentValidation;
using HeadlessCms.Api.Content.Models;
using HeadlessCms.Api.Content.Services;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Tenancy.Models;
using HeadlessCms.Api.Tenancy.Services;
using Microsoft.EntityFrameworkCore;

namespace HeadlessCms.Api.Endpoints.Content;

public sealed class UpdateContentEntry(
    ContentEntryService entries,
    ApplicationDbContext db,
    TenantAccessService tenantAccess)
    : Endpoint<UpdateContentEntryRequest, UpdateContentEntryResponse>
{
    private static readonly IReadOnlySet<TenantRole> Writers =
        new HashSet<TenantRole>([TenantRole.Owner, TenantRole.Editor]);

    public override void Configure()
    {
        Put(
            "tenants/{tenantId:guid}/projects/{projectId:guid}/" +
            "content-types/{contentTypeKey}/entries/{entryId:guid}");
        Claims("sub");
    }

    public override async Task HandleAsync(UpdateContentEntryRequest request, CancellationToken ct)
    {
        if (await tenantAccess.ResolveAsync(User, request.TenantId, Writers, ct) is null)
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
                ParseStatus(request.Status),
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

            Response = ToResponse(entry, schemaVersion);
            entry.Dispose();
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

    private static UpdateContentEntryResponse ToResponse(
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

public sealed class UpdateContentEntryRequest
{
    public Guid TenantId { get; init; }
    public Guid ProjectId { get; init; }
    public string ContentTypeKey { get; init; } = default!;
    public Guid EntryId { get; init; }
    public JsonElement Data { get; init; }
    public string Status { get; init; } = "draft";
}

public sealed class UpdateContentEntryResponse
{
    public Guid Id { get; init; }
    public int SchemaVersion { get; init; }
    public required string Status { get; init; }
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
            .Must(UpdateContentEntry.TryParseStatus)
            .WithMessage("Status must be draft or published.");
    }
}
