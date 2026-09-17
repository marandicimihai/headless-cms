using System.Text.Json;
using HeadlessCms.Api.Content.FieldTypes;
using HeadlessCms.Api.Content.Models;
using HeadlessCms.Api.Content.Services;
using HeadlessCms.Api.Data;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace HeadlessCms.Api.Tests;

public sealed class ContentFieldTypeHandlerTests
{
    private static ContentFieldTypeRegistry Registry() => new(
        [new TextFieldTypeHandler(), new NumberFieldTypeHandler(), new BooleanFieldTypeHandler()]);

    [Theory]
    [InlineData(ContentFieldType.Text, "\"hello\"", "1")]
    [InlineData(ContentFieldType.Number, "12.5", "\"12.5\"")]
    [InlineData(ContentFieldType.Boolean, "false", "0")]
    public void Values_AreValidatedByTheirType(ContentFieldType type, string valid, string invalid)
    {
        var handler = Registry().Get(type);
        var errors = new List<string>();
        var field = new ContentField { Key = "value", Type = type };
        handler.ValidateValue(field, JsonSerializer.Deserialize<JsonElement>(valid), errors);
        errors.ShouldBeEmpty();
        handler.ValidateValue(field, JsonSerializer.Deserialize<JsonElement>(invalid), errors);
        errors.ShouldBe([$"Field 'value' must contain a {type.ToString().ToLowerInvariant()} value."]);
    }

    [Theory]
    [InlineData(ContentFieldType.Text, "\"alpha\"", "\"beta\"", "eq", "alpha", 1)]
    [InlineData(ContentFieldType.Text, "\"alpha\"", "\"beta\"", "contains", "a", 2)]
    [InlineData(ContentFieldType.Number, "1", "2", "eq", "1", 1)]
    [InlineData(ContentFieldType.Number, "1", "2", "gt", "1", 1)]
    [InlineData(ContentFieldType.Number, "1", "2", "gte", "1", 2)]
    [InlineData(ContentFieldType.Number, "1", "2", "lt", "2", 1)]
    [InlineData(ContentFieldType.Number, "1", "2", "lte", "2", 2)]
    [InlineData(ContentFieldType.Boolean, "false", "true", "eq", "true", 1)]
    public void FiltersAndSorting_PreserveTypedBehavior(
        ContentFieldType type, string low, string high, string operation, string value, int count)
    {
        var handler = Registry().Get(type);
        var first = new ContentEntry { Id = Guid.NewGuid(), Data = JsonDocument.Parse($"{{\"value\":{low}}}") };
        var second = new ContentEntry { Id = Guid.NewGuid(), Data = JsonDocument.Parse($"{{\"value\":{high}}}") };
        var query = new[] { second, first }.AsQueryable();
        handler.ApplyFilter(query, "value", operation, value).Count().ShouldBe(count);
        handler.ApplySort(query, "value", false).Select(entry => entry.Id).ToArray()
            .ShouldBe([first.Id, second.Id]);
        handler.ApplySort(query, "value", true).Select(entry => entry.Id).ToArray()
            .ShouldBe([second.Id, first.Id]);
    }

    [Theory]
    [InlineData(ContentFieldType.Text, "gt", "a", "Operator 'gt' is not supported for text fields.")]
    [InlineData(ContentFieldType.Number, "eq", "bad", "Filter value 'bad' is not a valid number.")]
    [InlineData(ContentFieldType.Number, "contains", "1", "Operator 'contains' is not supported for number fields.")]
    [InlineData(ContentFieldType.Boolean, "eq", "bad", "Boolean fields support only 'eq' with a true or false value.")]
    [InlineData(ContentFieldType.Boolean, "gt", "true", "Boolean fields support only 'eq' with a true or false value.")]
    public void InvalidFilters_PreserveErrors(ContentFieldType type, string operation, string value, string error)
    {
        Should.Throw<ContentValidationException>(() => Registry().Get(type)
            .ApplyFilter(Array.Empty<ContentEntry>().AsQueryable(), "value", operation, value))
            .Message.ShouldBe(error);
    }

    [Fact]
    public void Registry_RejectsDuplicatesAndDisabledTypes()
    {
        Should.Throw<InvalidOperationException>(() => new ContentFieldTypeRegistry(
            [new TextFieldTypeHandler(), new TextFieldTypeHandler()]));
        var registry = new ContentFieldTypeRegistry([]);
        registry.TextSearchTypes.ShouldBeEmpty();
        Should.Throw<ContentValidationException>(() => registry.Get(ContentFieldType.Text))
            .Message.ShouldBe("Field type 'Text' is not registered.");
        var validator = new ContentDocumentValidator(registry);
        Should.Throw<ContentValidationException>(() => validator.ValidateFieldSettings(
            ContentFieldType.Text, "title", false, JsonSerializer.Deserialize<JsonElement>("{}")));
        Should.Throw<ContentValidationException>(() => validator.Validate(
            JsonSerializer.Deserialize<JsonElement>("{}"),
            [new ContentField { Key = "title", Type = ContentFieldType.Text }]));
        Registry().TextSearchTypes.ShouldBe([ContentFieldType.Text]);
    }

    [Fact]
    public void DocumentRules_StillApplyDefaultsAndRejectRequiredNulls()
    {
        var validator = new ContentDocumentValidator(Registry());
        var field = new ContentField
        {
            Key = "title", Type = ContentFieldType.Text,
            Settings = JsonSerializer.Deserialize<JsonElement>("{\"default\":\"Untitled\"}")
        };
        using var result = validator.Validate(JsonSerializer.Deserialize<JsonElement>("{\"title\":null}"), [field]);
        result.RootElement.GetProperty("title").GetString().ShouldBe("Untitled");
        field.Required = true;
        field.Settings = JsonSerializer.Deserialize<JsonElement>("{}");
        Should.Throw<ContentValidationException>(() => validator.Validate(
            JsonSerializer.Deserialize<JsonElement>("{\"title\":null}"), [field]))
            .Message.ShouldBe("Field 'title' must contain a text value.");
        Should.Throw<ContentValidationException>(() => validator.ValidateFieldSettings(
            ContentFieldType.Text, "title", false, JsonSerializer.Deserialize<JsonElement>("{\"default\":1}")))
            .Message.ShouldBe("Field 'title' must contain a text value.");
    }

    [Fact]
    public async Task PortableSearch_UsesRegisteredSearchTypes()
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var workspaceId = Guid.NewGuid();
        var project = new Project { Id = Guid.NewGuid(), WorkspaceId = workspaceId, Name = "Catalog" };
        var type = new ContentType { Id = Guid.NewGuid(), WorkspaceId = workspaceId, ProjectId = project.Id, Key = "article" };
        db.Projects.Add(project);
        db.ContentTypes.Add(type);
        db.ContentFields.Add(new ContentField
        {
            WorkspaceId = workspaceId, ProjectId = project.Id, ContentTypeId = type.Id,
            Key = "title", Type = ContentFieldType.Text, Settings = JsonSerializer.Deserialize<JsonElement>("{}")
        });
        db.ContentEntries.Add(new ContentEntry
        {
            WorkspaceId = workspaceId, ProjectId = project.Id, ContentTypeId = type.Id,
            Data = JsonDocument.Parse("{\"title\":\"needle\"}")
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        (await new WorkspaceSearchService(db, Registry()).SearchAsync(workspaceId, "needle", 5, TestContext.Current.CancellationToken))
            .Entries.Total.ShouldBe(1);
        (await new WorkspaceSearchService(db, new ContentFieldTypeRegistry([])).SearchAsync(workspaceId, "needle", 5, TestContext.Current.CancellationToken))
            .Entries.Total.ShouldBe(0);
    }
}
