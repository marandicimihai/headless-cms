using System.Text.Json;
using System.Text.Json.Serialization;
using FastEndpoints.Swagger;
using NJsonSchema;
using NJsonSchema.Generation.TypeMappers;
using NSwag;

namespace HeadlessCms.Api.Documentation;

internal static class ApiDocumentation
{
    public static IServiceCollection AddApiDocumentation(this IServiceCollection services) =>
        services.SwaggerDocument(options =>
        {
            options.EnableJWTBearerAuth = false;
            options.ExcludeNonFastEndpoints = true;
            options.AutoTagPathSegmentIndex = 0;
            options.RemoveEmptyRequestSchema = true;
            options.SerializerSettings = serializer =>
                serializer.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, false));
            options.DocumentSettings = settings =>
            {
                settings.DocumentName = "v1";
                settings.Title = "Headless CMS API";
                settings.Version = "v1";
                settings.Description = "The current CMS product API. Authentication uses the cms_session cookie. Platform roles do not grant workspace membership.";
                settings.PostProcess = document =>
                {
                    // This endpoint supports an open-ended filter key that cannot be represented by a DTO property.
                    // Keep it in the generated contract so the frontend renders it with the other query inputs.
                    const string entriesPath = "/api/workspaces/{workspaceId}/projects/{projectId}/content-types/{contentTypeKey}/entries";
                    if (document.Paths.TryGetValue(entriesPath, out var entries) && entries.TryGetValue("get", out var listEntries))
                    {
                        listEntries.Parameters.Add(new OpenApiParameter
                        {
                            Name = "filter[field][operator]",
                            Kind = OpenApiParameterKind.Query,
                            IsRequired = false,
                            Description = "Use a content or system field and its supported operator.",
                            Schema = new JsonSchema { Type = JsonObjectType.String },
                            CustomSchema = new JsonSchema { Type = JsonObjectType.String }
                        });
                    }

                    DescribeQueryParameter(document, entriesPath, "sort", defaultValue: "-$updatedAt");
                    DescribeQueryParameter(document, entriesPath, "status", allowedValues: ["draft", "published"]);
                    DescribeQueryParameter(document, entriesPath, "page", defaultValue: 1, minimum: 1);
                    DescribeQueryParameter(document, entriesPath, "pageSize", defaultValue: 25, minimum: 1, maximum: 100);

                    const string searchPath = "/api/workspaces/{workspaceId}/search";
                    DescribeQueryParameter(document, searchPath, "query", minimumLength: 2, maximumLength: 100);
                    DescribeQueryParameter(document, searchPath, "limit", defaultValue: 5, minimum: 1, maximum: 10);

                    const string invitationsPath = "/api/workspaces/{workspaceId}/invitations";
                    DescribeQueryParameter(document, invitationsPath, "page", defaultValue: 1, minimum: 1);
                    DescribeQueryParameter(document, invitationsPath, "pageSize", defaultValue: 20, minimum: 1, maximum: 100);
                    DescribeQueryParameter(document, invitationsPath, "status", allowedValues: ["pending", "accepted", "expired", "revoked"]);

                    const string membersPath = "/api/workspaces/{workspaceId}/members";
                    DescribeQueryParameter(document, membersPath, "page", defaultValue: 1, minimum: 1);
                    DescribeQueryParameter(document, membersPath, "pageSize", defaultValue: 20, minimum: 1, maximum: 100);

                    const string workspacesPath = "/api/workspaces";
                    DescribeQueryParameter(document, workspacesPath, "page", defaultValue: 1, minimum: 1);
                    DescribeQueryParameter(document, workspacesPath, "pageSize", defaultValue: 20, minimum: 1, maximum: 100);
                };
                settings.AddAuth("SessionCookie", new OpenApiSecurityScheme
                {
                    Type = OpenApiSecuritySchemeType.ApiKey,
                    In = OpenApiSecurityApiKeyLocation.Cookie,
                    Name = "cms_session",
                    Description = "Opaque HttpOnly session cookie set by login or invitation registration. Use a cookie jar; the secret is never returned in JSON."
                });
                // JsonElement represents user-defined content, not its CLR implementation properties.
                settings.SchemaSettings.TypeMappers.Add(new PrimitiveTypeMapper(typeof(JsonElement), schema =>
                {
                    schema.Type = JsonObjectType.Object;
                    schema.AllowAdditionalProperties = true;
                }));
            };
        });

    private static void DescribeQueryParameter(
        OpenApiDocument document,
        string path,
        string name,
        object? defaultValue = null,
        string[]? allowedValues = null,
        decimal? minimum = null,
        decimal? maximum = null,
        int? minimumLength = null,
        int? maximumLength = null)
    {
        if (!document.Paths.TryGetValue(path, out var pathItem) ||
            !pathItem.TryGetValue("get", out var operation))
            return;

        var parameter = operation.Parameters.SingleOrDefault(parameter =>
            parameter.Kind == OpenApiParameterKind.Query && parameter.Name == name);
        if (parameter is null)
            return;

        var schema = parameter.Schema ??= new JsonSchema();
        if (defaultValue is not null)
        {
            schema.Default = defaultValue;
            schema.Example = defaultValue;
            parameter.IsRequired = false;
        }
        if (allowedValues is not null)
        {
            schema.Type = JsonObjectType.String;
            schema.Enumeration.Clear();
            foreach (var value in allowedValues)
                schema.Enumeration.Add(value);
        }
        if (minimum is not null)
            schema.Minimum = minimum.Value;
        if (maximum is not null)
            schema.Maximum = maximum.Value;
        if (minimumLength is not null)
            schema.MinLength = minimumLength.Value;
        if (maximumLength is not null)
            schema.MaxLength = maximumLength.Value;
    }
}
