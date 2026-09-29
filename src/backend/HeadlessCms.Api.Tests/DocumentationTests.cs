using System.Text.RegularExpressions;
using FastEndpoints;
using FastEndpoints.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NSwag;
using Shouldly;
using Xunit;

namespace HeadlessCms.Api.Tests;

public sealed class DocumentationTestApp : InMemoryTestApp;
public sealed class DocumentationCollection : TestCollection<DocumentationTestApp>;

[Collection<DocumentationCollection>]
public sealed class DocumentationTests(DocumentationTestApp app) : TestBase
{
    private async Task<OpenApiDocument> ReadDocument()
    {
        using var response = await app.HttpsClient.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        return await OpenApiDocument.FromJsonAsync(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Contract_CoversEveryProductRoute_WithNativeMetadataAndCookieSecurity()
    {
        var document = await ReadDocument();
        var expected = app.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<EndpointDefinition>() is not null)
            .SelectMany(endpoint => endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Select(method =>
                $"{method} /{Regex.Replace(endpoint.RoutePattern.RawText!.TrimStart('/'), @":[^}]+", "")}"))
            .Order().ToArray();
        var actual = document.Operations.Select(operation => $"{operation.Method.ToUpperInvariant()} {operation.Path}").Order().ToArray();
        expected.ShouldNotBeEmpty();
        actual.ShouldBe(expected);
        actual.ShouldNotContain(item => item.Contains("/health/", StringComparison.Ordinal));

        var security = document.SecurityDefinitions["SessionCookie"];
        security.Type.ShouldBe(OpenApiSecuritySchemeType.ApiKey);
        security.In.ShouldBe(OpenApiSecurityApiKeyLocation.Cookie);
        security.Name.ShouldBe("cms_session");
        document.SecurityDefinitions.Count.ShouldBe(1);
        foreach (var entry in document.Operations)
        {
            var operation = entry.Operation;
            operation.Summary.ShouldNotBeNullOrWhiteSpace();
            operation.Description.ShouldStartWith("Access: ");
            operation.Tags.Count.ShouldBe(1);
            if (entry.Path is "/api/auth/login" or "/api/auth/logout" or "/api/auth/invitations/preview" or "/api/auth/invitations/register")
                operation.Security?.Count.ShouldBe(0);
            else
                operation.Security.ShouldContain(requirement => requirement.ContainsKey("SessionCookie"));
            if (entry.Method == "get" || entry.Method == "delete")
                operation.RequestBody.ShouldBeNull();
            foreach (var parameter in operation.Parameters)
                parameter.Description.ShouldNotBeNullOrWhiteSpace();
            if (operation.Responses.TryGetValue("204", out var empty))
                empty.Content.ShouldBeEmpty();
        }
    }

    [Fact]
    public async Task Examples_ValidateAgainstGeneratedRequestAndResponseSchemas()
    {
        var document = await ReadDocument();
        foreach (var entry in document.Operations)
        {
            foreach (var media in entry.Operation.RequestBody?.Content.Values ?? [])
            {
                var example = media.Example ?? media.Schema.ActualSchema.Example;
                example.ShouldNotBeNull($"{entry.Method} {entry.Path} needs a request example");
                media.Schema.Validate(Newtonsoft.Json.JsonConvert.SerializeObject(example)).ShouldBeEmpty($"Invalid request example: {entry.Path}");
            }
            foreach (var response in entry.Operation.Responses.Where(response => response.Key.StartsWith('2') && response.Key != "204"))
            {
                response.Value.Content.ShouldNotBeEmpty();
                foreach (var media in response.Value.Content.Values)
                {
                    media.Example.ShouldNotBeNull($"{entry.Method} {entry.Path} needs a response example");
                    // NSwag examples are JTokens; preserve their JSON representation.
                    media.Schema.Validate(Newtonsoft.Json.JsonConvert.SerializeObject(media.Example))
                        .ShouldBeEmpty($"Invalid response example: {entry.Path}");
                }
            }
        }
    }

    [Fact]
    public async Task ContentAndPagination_DescribeActualWireContract()
    {
        var document = await ReadDocument();
        var path = "/api/workspaces/{workspaceId}/projects/{projectId}/content-types/{contentTypeKey}/entries";
        var list = document.Paths[path]["get"];
        list.Parameters
            .Where(parameter => parameter.Kind == OpenApiParameterKind.Query)
            .Select(parameter => parameter.Name)
            .Order()
            .ShouldBe(["filter[field][operator]", "page", "pageSize", "sort", "status"]);
        foreach (var parameter in list.Parameters.Where(parameter => parameter.Name is "page" or "pageSize"))
        {
            parameter.IsRequired.ShouldBeFalse();
            Convert.ToInt32(parameter.Schema.Default).ShouldBe(parameter.Name == "page" ? 1 : 25);
        }
        list.Parameters.Single(parameter => parameter.Name == "sort").Schema.Default.ShouldBe("-$updatedAt");
        list.Parameters.Single(parameter => parameter.Name == "status").Schema.Enumeration
            .ShouldBe(new object[] { "draft", "published" });
        var filter = list.Parameters.Single(parameter => parameter.Name == "filter[field][operator]");
        filter.Kind.ShouldBe(OpenApiParameterKind.Query);
        filter.Schema.Type.ShouldBe(NJsonSchema.JsonObjectType.String);
        list.Description.ShouldContain("filter[field][operator]");
        list.Description.ShouldContain("Both drafts and published");
        var create = document.Paths[path]["post"];
        create.Responses.Keys.ShouldContain("201");
        create.Responses.Keys.ShouldNotContain("200");
        var request = create.RequestBody.Content["application/json"].Schema.ActualSchema;
        request.ActualProperties.Keys.ShouldNotContain("workspaceId");
        request.ActualProperties["data"].ActualSchema.IsObject.ShouldBeTrue();
        request.ActualProperties["data"].ActualSchema.Properties.ShouldBeEmpty();
        request.ActualProperties["status"].ActualSchema.Enumeration.ShouldBe(new object[] { "draft", "published" });
        var invitationList = document.Paths["/api/workspaces/{workspaceId}/invitations"]["get"];
        invitationList.Parameters
            .Where(parameter => parameter.Kind == OpenApiParameterKind.Query)
            .Select(parameter => parameter.Name)
            .Order()
            .ShouldBe(["page", "pageSize", "status"]);
        invitationList.Parameters.Single(parameter => parameter.Name == "status").Schema.Enumeration
            .ShouldBe(new object[] { "pending", "accepted", "expired", "revoked" });
        invitationList.Description.ShouldContain("ownerless workspace");
        Newtonsoft.Json.JsonConvert.SerializeObject(invitationList.Responses["200"].Content["application/json"].Example)
            .ShouldNotContain("invitationUrl");
        (await app.HttpsClient.GetAsync("/api/openapi/v1.json", TestContext.Current.CancellationToken)).StatusCode
            .ShouldBe(System.Net.HttpStatusCode.NotFound);
    }
}
