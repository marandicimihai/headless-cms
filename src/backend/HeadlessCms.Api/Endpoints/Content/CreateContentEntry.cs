using System.Text.Json;
using FluentValidation;
using HeadlessCms.Api.Content.Models;
using HeadlessCms.Api.Content.Services;
using HeadlessCms.Api.Workspaces.Models;
using HeadlessCms.Api.Workspaces.Services;

using HeadlessCms.Api.Documentation;

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
        Description(b => b.WithTags("Content entries"), clearDefaults: true);
        Summary((EndpointSummary s) =>
        {
            s.Summary = "Create an entry";
            s.Description = "Access: Workspace Owner or Editor.\n\nData must be a JSON object matching the current field definition. Unknown fields and invalid required values are rejected. Status defaults to draft; use published to publish immediately.";
            s.Params["WorkspaceId"] = "Workspace UUID.";
            s.Params["ProjectId"] = "Project UUID within the workspace.";
            s.Params["ContentTypeKey"] = "Immutable content-type key, for example articles.";
            s.Params["Data"] = "Complete JSON data object matching the current content-type definition. Unknown fields are rejected.";
            s.Params["Status"] = "draft or published; defaults to draft when omitted.";
            s.ExampleRequest = new { Data = ApiExamples.EntryData, Status = "draft" };
            s.Response<CreateContentEntryResponse>(201, "Created.");
            s.ResponseExamples[201] = ApiExamples.Entry;
            s.Response<FastEndpoints.ProblemDetails>(400, "Invalid request or validation failure.", "application/problem+json");
            s.Response<ApiProblem>(401, "Authentication is required, or the session has expired.", "application/problem+json");
            s.Response(403, "The caller does not have permission.");
            s.Response(404, "Resource not found or inaccessible.");
        });
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
