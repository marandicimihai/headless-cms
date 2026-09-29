using System.Text.Json;
using System.Text.RegularExpressions;
using FastEndpoints;
using HeadlessCms.Api.Ai;
using HeadlessCms.Api.Content.FieldTypes;
using HeadlessCms.Api.Content.Models;
using HeadlessCms.Api.Content.Services;
using HeadlessCms.Api.Endpoints;
using HeadlessCms.Api.Workspaces.Services;

namespace HeadlessCms.Api.Endpoints.Content;

public sealed class GenerateSchemaProposal(
    ISchemaAssistantClient assistant,
    SchemaAssistantRateLimiter rateLimiter,
    ContentDefinitionService definitions,
    ContentDocumentValidator documentValidator,
    WorkspaceAccessService workspaceAccess,
    ILogger<GenerateSchemaProposal> logger)
    : Endpoint<GenerateSchemaProposalRequest, GenerateSchemaProposalResponse>
{
    private static readonly Regex KeyPattern = new("^[a-z][a-z0-9_]{0,63}$", RegexOptions.Compiled);

    public override void Configure()
    {
        Post("workspaces/{workspaceId:guid}/projects/{projectId:guid}/schema-assistant/generate");
        Claims("sub");
        Description(b => b.WithTags("Schema assistant"), clearDefaults: true);
        Summary(s =>
        {
            s.Summary = "Generate a project schema proposal with AI";
            s.Description = "Access: Workspace Owner or Editor. Sends bounded chat history to the configured Groq model. The result is a draft and is not saved.";
            s.Response<GenerateSchemaProposalResponse>(200, "Generated proposal.");
            s.Response(400, "Invalid request or generated schema.");
            s.Response<ApiProblem>(401, "Authentication is required.", "application/problem+json");
            s.Response(403, "The caller does not have permission.");
            s.Response(404, "Project not found or inaccessible.");
            s.Response(429, "The user or provider has reached a rate limit.");
            s.Response<ApiProblem>(502, "The provider returned an invalid response.", "application/problem+json");
            s.Response(503, "AI generation is unavailable.");
            s.Response<ApiProblem>(504, "AI generation timed out.", "application/problem+json");
        });
    }

    public override async Task HandleAsync(GenerateSchemaProposalRequest request, CancellationToken ct)
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

        if (request.Messages is null || request.Messages.Count is 0 or > 12 ||
            request.Messages.Any(message =>
                message.Role is not ("user" or "assistant") ||
                string.IsNullOrWhiteSpace(message.Content) ||
                message.Content.Length > 2000))
        {
            AddError(r => r.Messages, "Supply 1 to 12 chat messages, each no longer than 2000 characters.");
            await Send.ErrorsAsync(cancellation: ct);
            return;
        }

        var userId = User.FindFirst("sub")?.Value;
        if (userId is null)
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        if (!rateLimiter.TryAcquire(userId, out var retryAfter))
        {
            HttpContext.Response.Headers.RetryAfter = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
            await ApiErrors.SendAsync(
                HttpContext,
                429,
                "AI_RATE_LIMITED",
                "AI requests are temporarily rate limited. Try again shortly.",
                ct);
            return;
        }

        IReadOnlyList<ContentType> currentTypes;
        try
        {
            currentTypes = await definitions.ListCurrentAsync(request.WorkspaceId, request.ProjectId, ct);
        }
        catch (ContentNotFoundException)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        try
        {
            var proposal = await assistant.GenerateAsync(
                request.Messages.Select(message => new SchemaAssistantChatMessage(message.Role, message.Content)).ToList(),
                currentTypes.Select(type => type.Key).ToArray(),
                ct);
            ValidateProposal(proposal, documentValidator);

            var existingKeys = currentTypes.Select(type => type.Key).ToHashSet(StringComparer.Ordinal);
            await Send.OkAsync(new GenerateSchemaProposalResponse
            {
                Reply = proposal.Reply,
                ContentTypes = proposal.ContentTypes,
                ExistingKeyConflicts = proposal.ContentTypes
                    .Where(type => existingKeys.Contains(type.Key))
                    .Select(type => type.Key)
                    .Distinct(StringComparer.Ordinal)
                    .ToList()
            }, ct);
        }
        catch (ContentValidationException exception)
        {
            logger.LogWarning("Generated schema proposal failed validation: {Reason}", exception.Message);
            await ApiErrors.SendAsync(
                HttpContext,
                502,
                "AI_INVALID_PROPOSAL",
                "The AI returned a schema that does not fit the CMS rules. Ask it to revise the proposal.",
                ct);
        }
        catch (SchemaAssistantProviderException exception)
        {
            var statusCode = exception.StatusCode ?? System.Net.HttpStatusCode.ServiceUnavailable;
            if (statusCode == System.Net.HttpStatusCode.TooManyRequests)
                HttpContext.Response.Headers.RetryAfter = "60";
            await ApiErrors.SendAsync(
                HttpContext,
                (int)statusCode,
                "AI_PROVIDER_ERROR",
                exception.Message,
                ct);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            await ApiErrors.SendAsync(
                HttpContext,
                504,
                "AI_TIMEOUT",
                "AI generation timed out. Try again.",
                ct);
        }
    }

    private static void ValidateProposal(
        SchemaAssistantProposal proposal,
        ContentDocumentValidator documentValidator)
    {
        if (proposal.Reply.Length > 2000 || proposal.ContentTypes.Count > 6)
            throw new ContentValidationException("The proposal exceeds its size limit.");

        var typeKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var type in proposal.ContentTypes)
        {
            if (!KeyPattern.IsMatch(type.Key) || !typeKeys.Add(type.Key))
                throw new ContentValidationException("The proposal contains an invalid or repeated content type key.");
            if (type.Fields.Count is 0 or > 20)
                throw new ContentValidationException($"Content type '{type.Key}' must have between 1 and 20 fields.");

            var fieldKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var field in type.Fields)
            {
                if (!KeyPattern.IsMatch(field.Key) || !fieldKeys.Add(field.Key) ||
                    !Enum.TryParse<ContentFieldType>(field.Type, ignoreCase: true, out var fieldType) ||
                    !Enum.IsDefined(fieldType))
                    throw new ContentValidationException("The proposal contains an invalid or repeated field key or type.");

                var settings = field.DefaultValue is { ValueKind: not JsonValueKind.Null } defaultValue
                    ? JsonSerializer.SerializeToElement(new Dictionary<string, JsonElement>
                    {
                        ["default"] = defaultValue
                    })
                    : JsonSerializer.SerializeToElement(new Dictionary<string, object?>());
                documentValidator.ValidateFieldSettings(fieldType, field.Key, field.Required, settings);
            }
        }
    }
}

public sealed class GenerateSchemaProposalRequest
{
    public Guid WorkspaceId { get; init; }
    public Guid ProjectId { get; init; }
    public required List<SchemaAssistantChatMessageRequest> Messages { get; init; }
}

public sealed class SchemaAssistantChatMessageRequest
{
    public required string Role { get; init; }
    public required string Content { get; init; }
}

public sealed class GenerateSchemaProposalResponse
{
    public required string Reply { get; init; }
    public required IReadOnlyList<SchemaAssistantTypeProposal> ContentTypes { get; init; }
    public required IReadOnlyList<string> ExistingKeyConflicts { get; init; }
}
