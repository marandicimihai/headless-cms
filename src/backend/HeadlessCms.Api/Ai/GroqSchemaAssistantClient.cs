using System.ClientModel;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI;
using ChatClient = OpenAI.Chat.ChatClient;

namespace HeadlessCms.Api.Ai;

public sealed record SchemaAssistantChatMessage(string Role, string Content);

public sealed record SchemaAssistantFieldProposal(
    string Key,
    string Type,
    bool Required,
    JsonElement? DefaultValue);

public sealed record SchemaAssistantTypeProposal(
    string Key,
    IReadOnlyList<SchemaAssistantFieldProposal> Fields);

public sealed record SchemaAssistantProposal(
    string Reply,
    IReadOnlyList<SchemaAssistantTypeProposal> ContentTypes);

public interface ISchemaAssistantClient
{
    Task<SchemaAssistantProposal> GenerateAsync(
        IReadOnlyList<SchemaAssistantChatMessage> messages,
        IReadOnlyCollection<string> existingKeys,
        CancellationToken cancellationToken);
}

public sealed class SchemaAssistantProviderException(string message, HttpStatusCode? statusCode = null)
    : Exception(message)
{
    public HttpStatusCode? StatusCode { get; } = statusCode;
}

public sealed class GroqSchemaAssistantClient(
    IOptions<AiOptions> options,
    ILogger<GroqSchemaAssistantClient> logger) : ISchemaAssistantClient
{
    private const string Model = "openai/gpt-oss-20b";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public async Task<SchemaAssistantProposal> GenerateAsync(
        IReadOnlyList<SchemaAssistantChatMessage> messages,
        IReadOnlyCollection<string> existingKeys,
        CancellationToken cancellationToken)
    {
        var apiKey = options.Value.GroqApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new SchemaAssistantProviderException("AI schema chat is not configured.");

        var openAi = new ChatClient(
            Model,
            new ApiKeyCredential(apiKey),
            new OpenAIClientOptions
            {
                Endpoint = new Uri("https://api.groq.com/openai/v1")
            });
        var chat = openAi.AsIChatClient();
        var chatMessages = new List<ChatMessage>
        {
            new(ChatRole.System, BuildInstructions(existingKeys))
        };
        chatMessages.AddRange(messages.Select(message =>
            new ChatMessage(message.Role == "user" ? ChatRole.User : ChatRole.Assistant, message.Content)));

        var chatOptions = new ChatOptions
        {
            Temperature = 0.2f,
            MaxOutputTokens = 4000,
            ResponseFormat = ChatResponseFormat.ForJsonSchema(
                JsonSerializer.SerializeToElement(ProposalJsonSchema, JsonOptions),
                "cms_schema_proposal"),
            AdditionalProperties = new AdditionalPropertiesDictionary
            {
                ["strict"] = true
            }
        };

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(35));
        try
        {
            var response = await chat.GetResponseAsync(chatMessages, chatOptions, timeout.Token);
            if (string.IsNullOrWhiteSpace(response.Text))
                throw new JsonException("The provider returned an empty response.");

            var proposal = JsonSerializer.Deserialize<SchemaAssistantProposal>(response.Text, JsonOptions);
            if (proposal is null || string.IsNullOrWhiteSpace(proposal.Reply) || proposal.ContentTypes is null)
                throw new JsonException("The provider response is incomplete.");
            return proposal;
        }
        catch (ClientResultException exception)
        {
            logger.LogWarning("Groq schema generation returned HTTP {StatusCode}", exception.Status);
            var statusCode = exception.Status == (int)HttpStatusCode.TooManyRequests
                ? HttpStatusCode.TooManyRequests
                : exception.Status >= 500
                    ? HttpStatusCode.ServiceUnavailable
                    : HttpStatusCode.BadGateway;
            throw new SchemaAssistantProviderException(
                statusCode == HttpStatusCode.TooManyRequests
                    ? "The AI provider is busy. Wait a moment and try again."
                    : "The AI provider could not generate a schema. Try again shortly.",
                statusCode);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Unable to reach Groq for schema generation");
            throw new SchemaAssistantProviderException(
                "The AI provider is temporarily unavailable. Try again shortly.",
                HttpStatusCode.ServiceUnavailable);
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "Groq returned an invalid structured schema proposal");
            throw new SchemaAssistantProviderException(
                "The AI returned an invalid schema proposal. Try rephrasing your request.",
                HttpStatusCode.BadGateway);
        }
    }

    private static string BuildInstructions(IReadOnlyCollection<string> existingKeys) =>
        "You help users define content types for a headless CMS. Return a complete current proposal " +
        "in every response so follow-up requests refine or replace the draft. The CMS supports only " +
        "text, number, and boolean fields. Content type and field keys must be lowercase snake_case " +
        "matching ^[a-z][a-z0-9_]*$ and at most 64 characters. Every type needs at least one field. " +
        "Required fields must have defaultValue null. Optional defaults must match the field type; " +
        "use null when there is no default. Do not invent descriptions, relationships, dates, or " +
        "other field types. Explain unsupported requests in reply and make the closest valid proposal " +
        "when possible. Propose no more than six content types and twenty fields per type. Existing " +
        "content type keys in this project are: " +
        (existingKeys.Count == 0 ? "(none)" : string.Join(", ", existingKeys)) +
        ". Never modify those existing content types.";

    private static readonly object ProposalJsonSchema = new
    {
        type = "object",
        additionalProperties = false,
        properties = new
        {
            reply = new { type = "string" },
            contentTypes = new
            {
                type = "array",
                items = new
                {
                    type = "object",
                    additionalProperties = false,
                    properties = new
                    {
                        key = new { type = "string" },
                        fields = new
                        {
                            type = "array",
                            items = new
                            {
                                type = "object",
                                additionalProperties = false,
                                properties = new
                                {
                                    key = new { type = "string" },
                                    type = new { type = "string", @enum = new[] { "text", "number", "boolean" } },
                                    required = new { type = "boolean" },
                                    defaultValue = new
                                    {
                                        anyOf = new object[]
                                        {
                                            new { type = "string" },
                                            new { type = "number" },
                                            new { type = "boolean" },
                                            new { type = "null" }
                                        }
                                    }
                                },
                                required = new[] { "key", "type", "required", "defaultValue" }
                            }
                        }
                    },
                    required = new[] { "key", "fields" }
                }
            }
        },
        required = new[] { "reply", "contentTypes" }
    };
}
