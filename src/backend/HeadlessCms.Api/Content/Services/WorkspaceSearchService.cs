using System.Data;
using System.Data.Common;
using System.Text.Json;
using HeadlessCms.Api.Content.FieldTypes;
using HeadlessCms.Api.Content.Models;
using HeadlessCms.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace HeadlessCms.Api.Content.Services;

public sealed record WorkspaceSearchProject(Guid Id, string Name);

public sealed record WorkspaceSearchContentType(
    Guid Id,
    string Key,
    Guid ProjectId,
    string ProjectName);

public sealed record WorkspaceSearchEntry(
    Guid Id,
    ContentEntryStatus Status,
    string ContentTypeKey,
    Guid ProjectId,
    string ProjectName,
    string MatchedFieldKey,
    string Snippet);

public sealed record WorkspaceSearchGroup<T>(IReadOnlyList<T> Items, int Total);

public sealed record WorkspaceSearchResult(
    WorkspaceSearchGroup<WorkspaceSearchProject> Projects,
    WorkspaceSearchGroup<WorkspaceSearchContentType> ContentTypes,
    WorkspaceSearchGroup<WorkspaceSearchEntry> Entries);

public sealed class WorkspaceSearchService(ApplicationDbContext db, ContentFieldTypeRegistry fieldTypes)
{
    private const int SnippetLength = 140;

    public async Task<WorkspaceSearchResult> SearchAsync(
        Guid workspaceId,
        string query,
        int limit,
        CancellationToken ct = default)
    {
        return db.Database.IsNpgsql()
            ? await SearchPostgreSqlAsync(workspaceId, query, limit, ct)
            : await SearchPortableAsync(workspaceId, query, limit, ct);
    }

    private async Task<WorkspaceSearchResult> SearchPostgreSqlAsync(
        Guid workspaceId,
        string query,
        int limit,
        CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection();
        var closeConnection = connection.State != ConnectionState.Open;

        if (closeConnection)
            await connection.OpenAsync(ct);

        try
        {
            var projects = await SearchProjectsAsync(connection, workspaceId, query, limit, ct);
            var contentTypes = await SearchContentTypesAsync(connection, workspaceId, query, limit, ct);
            var entries = await SearchEntriesAsync(connection, workspaceId, query, limit, ct);

            return new WorkspaceSearchResult(projects, contentTypes, entries);
        }
        finally
        {
            if (closeConnection)
                await connection.CloseAsync();
        }
    }

    private static async Task<WorkspaceSearchGroup<WorkspaceSearchProject>> SearchProjectsAsync(
        DbConnection connection,
        Guid workspaceId,
        string query,
        int limit,
        CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            WITH matches AS (
                SELECT "Id", "Name",
                    CASE
                        WHEN lower("Name") = lower(@query) THEN 0
                        WHEN "Name" ILIKE @prefixPattern ESCAPE E'\\' THEN 1
                        ELSE 2
                    END AS match_rank
                FROM "Projects"
                WHERE "WorkspaceId" = @workspaceId
                    AND "Name" ILIKE @containsPattern ESCAPE E'\\'
            )
            SELECT "Id", "Name", count(*) OVER () AS total
            FROM matches
            ORDER BY match_rank, lower("Name"), "Id"
            LIMIT @limit;
            """;
        AddParameters(command, workspaceId, query, limit);

        var items = new List<WorkspaceSearchProject>();
        var total = 0;
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            items.Add(new WorkspaceSearchProject(reader.GetGuid(0), reader.GetString(1)));
            total = reader.GetInt32(2);
        }

        return new WorkspaceSearchGroup<WorkspaceSearchProject>(items, total);
    }

    private static async Task<WorkspaceSearchGroup<WorkspaceSearchContentType>> SearchContentTypesAsync(
        DbConnection connection,
        Guid workspaceId,
        string query,
        int limit,
        CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            WITH matches AS (
                SELECT content_type."Id", content_type."Key", project."Id" AS project_id,
                    project."Name" AS project_name,
                    CASE
                        WHEN lower(content_type."Key") = lower(@query) THEN 0
                        WHEN content_type."Key" ILIKE @prefixPattern ESCAPE E'\\' THEN 1
                        ELSE 2
                    END AS match_rank
                FROM "ContentTypes" AS content_type
                JOIN "Projects" AS project ON project."Id" = content_type."ProjectId"
                    AND project."WorkspaceId" = content_type."WorkspaceId"
                WHERE content_type."WorkspaceId" = @workspaceId
                    AND content_type."Key" ILIKE @containsPattern ESCAPE E'\\'
            )
            SELECT "Id", "Key", project_id, project_name, count(*) OVER () AS total
            FROM matches
            ORDER BY match_rank, lower("Key"), lower(project_name), "Id"
            LIMIT @limit;
            """;
        AddParameters(command, workspaceId, query, limit);

        var items = new List<WorkspaceSearchContentType>();
        var total = 0;
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            items.Add(new WorkspaceSearchContentType(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetGuid(2),
                reader.GetString(3)));
            total = reader.GetInt32(4);
        }

        return new WorkspaceSearchGroup<WorkspaceSearchContentType>(items, total);
    }

    private async Task<WorkspaceSearchGroup<WorkspaceSearchEntry>> SearchEntriesAsync(
        DbConnection connection,
        Guid workspaceId,
        string query,
        int limit,
        CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            WITH matching_values AS (
                SELECT entry."Id", entry."Status", entry."UpdatedAt", content_type."Key" AS content_type_key,
                    project."Id" AS project_id, project."Name" AS project_name,
                    field."Key" AS matched_field_key, value.value AS matched_value,
                    CASE
                        WHEN lower(value.value) = lower(@query) THEN 0
                        WHEN value.value ILIKE @prefixPattern ESCAPE E'\\' THEN 1
                        ELSE 2
                    END AS match_rank,
                    row_number() OVER (
                        PARTITION BY entry."Id"
                        ORDER BY
                            CASE
                                WHEN lower(value.value) = lower(@query) THEN 0
                                WHEN value.value ILIKE @prefixPattern ESCAPE E'\\' THEN 1
                                ELSE 2
                            END,
                            field."Position",
                            field."Key"
                    ) AS match_position
                FROM "ContentEntries" AS entry
                JOIN "ContentTypes" AS content_type ON content_type."Id" = entry."ContentTypeId"
                    AND content_type."WorkspaceId" = entry."WorkspaceId"
                    AND content_type."ProjectId" = entry."ProjectId"
                JOIN "Projects" AS project ON project."Id" = entry."ProjectId"
                    AND project."WorkspaceId" = entry."WorkspaceId"
                JOIN "ContentFields" AS field ON field."ContentTypeId" = entry."ContentTypeId"
                    AND field."WorkspaceId" = entry."WorkspaceId"
                    AND field."ProjectId" = entry."ProjectId"
                CROSS JOIN LATERAL jsonb_each_text(entry."Data") AS value(key, value)
                WHERE entry."WorkspaceId" = @workspaceId
                    AND field."Type" = ANY(@textSearchTypes)
                    AND field."Key" = value.key
                    AND value.value ILIKE @containsPattern ESCAPE E'\\'
            ), matches AS (
                SELECT * FROM matching_values WHERE match_position = 1
            )
            SELECT "Id", "Status", content_type_key, project_id, project_name, matched_field_key,
                matched_value, count(*) OVER () AS total
            FROM matches
            ORDER BY match_rank, "UpdatedAt" DESC, "Id"
            LIMIT @limit;
            """;
        AddParameters(command, workspaceId, query, limit);
        var typesParameter = command.CreateParameter();
        typesParameter.ParameterName = "textSearchTypes";
        typesParameter.Value = fieldTypes.TextSearchTypes.Select(type => type.ToString()).ToArray();
        command.Parameters.Add(typesParameter);

        var items = new List<WorkspaceSearchEntry>();
        var total = 0;
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var matchedValue = reader.GetString(6);
            items.Add(new WorkspaceSearchEntry(
                reader.GetGuid(0),
                Enum.Parse<ContentEntryStatus>(reader.GetString(1), ignoreCase: true),
                reader.GetString(2),
                reader.GetGuid(3),
                reader.GetString(4),
                reader.GetString(5),
                CreateSnippet(matchedValue, query)));
            total = reader.GetInt32(7);
        }

        return new WorkspaceSearchGroup<WorkspaceSearchEntry>(items, total);
    }

    private async Task<WorkspaceSearchResult> SearchPortableAsync(
        Guid workspaceId,
        string query,
        int limit,
        CancellationToken ct)
    {
        var projects = await db.Projects.AsNoTracking()
            .Where(project => project.WorkspaceId == workspaceId)
            .ToListAsync(ct);
        var contentTypes = await db.ContentTypes.AsNoTracking()
            .Where(contentType => contentType.WorkspaceId == workspaceId)
            .ToListAsync(ct);
        var textSearchTypes = fieldTypes.TextSearchTypes;
        var fields = await db.ContentFields.AsNoTracking()
            .Where(field => field.WorkspaceId == workspaceId && textSearchTypes.Contains(field.Type))
            .ToListAsync(ct);
        var entries = await db.ContentEntries.AsNoTracking()
            .Where(entry => entry.WorkspaceId == workspaceId)
            .ToListAsync(ct);

        var projectNames = projects.ToDictionary(project => project.Id, project => project.Name);
        var matchingProjects = projects
            .Where(project => Contains(project.Name, query))
            .OrderBy(project => MatchRank(project.Name, query))
            .ThenBy(project => project.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(project => project.Id)
            .Select(project => new WorkspaceSearchProject(project.Id, project.Name))
            .ToList();
        var matchingContentTypes = contentTypes
            .Where(contentType => Contains(contentType.Key, query))
            .OrderBy(contentType => MatchRank(contentType.Key, query))
            .ThenBy(contentType => contentType.Key, StringComparer.OrdinalIgnoreCase)
            .ThenBy(contentType => projectNames[contentType.ProjectId], StringComparer.OrdinalIgnoreCase)
            .ThenBy(contentType => contentType.Id)
            .Select(contentType => new WorkspaceSearchContentType(
                contentType.Id,
                contentType.Key,
                contentType.ProjectId,
                projectNames[contentType.ProjectId]))
            .ToList();
        var fieldsByContentType = fields
            .GroupBy(field => field.ContentTypeId)
            .ToDictionary(group => group.Key, group => group.ToList());
        var contentTypesById = contentTypes.ToDictionary(contentType => contentType.Id);
        var matchingEntries = new List<(WorkspaceSearchEntry Item, int Rank, int Position, DateTime UpdatedAt)>();

        foreach (var entry in entries)
        {
            if (!fieldsByContentType.TryGetValue(entry.ContentTypeId, out var textFields) ||
                !contentTypesById.TryGetValue(entry.ContentTypeId, out var contentType) ||
                !projectNames.TryGetValue(entry.ProjectId, out var projectName))
                continue;

            var match = textFields
                .Select(field => new { Field = field, Value = GetTextValue(entry.Data, field.Key) })
                .Where(candidate => candidate.Value is not null && Contains(candidate.Value, query))
                .OrderBy(candidate => MatchRank(candidate.Value!, query))
                .ThenBy(candidate => candidate.Field.Position)
                .ThenBy(candidate => candidate.Field.Key, StringComparer.Ordinal)
                .FirstOrDefault();
            if (match?.Value is null)
                continue;

            matchingEntries.Add((
                new WorkspaceSearchEntry(
                    entry.Id,
                    entry.Status,
                    contentType.Key,
                    entry.ProjectId,
                    projectName,
                    match.Field.Key,
                    CreateSnippet(match.Value, query)),
                MatchRank(match.Value, query),
                match.Field.Position,
                entry.UpdatedAt));
        }

        var orderedEntries = matchingEntries
            .OrderBy(entry => entry.Rank)
            .ThenByDescending(entry => entry.UpdatedAt)
            .ThenBy(entry => entry.Item.Id)
            .Select(entry => entry.Item)
            .ToList();

        foreach (var entry in entries)
            entry.Dispose();

        return new WorkspaceSearchResult(
            new WorkspaceSearchGroup<WorkspaceSearchProject>(matchingProjects.Take(limit).ToList(), matchingProjects.Count),
            new WorkspaceSearchGroup<WorkspaceSearchContentType>(matchingContentTypes.Take(limit).ToList(), matchingContentTypes.Count),
            new WorkspaceSearchGroup<WorkspaceSearchEntry>(orderedEntries.Take(limit).ToList(), orderedEntries.Count));
    }

    private static void AddParameters(DbCommand command, Guid workspaceId, string query, int limit)
    {
        AddParameter(command, "@workspaceId", workspaceId);
        AddParameter(command, "@query", query);
        var escapedQuery = EscapeLikePattern(query);
        AddParameter(command, "@containsPattern", $"%{escapedQuery}%");
        AddParameter(command, "@prefixPattern", $"{escapedQuery}%");
        AddParameter(command, "@limit", limit);
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static string? GetTextValue(JsonDocument data, string fieldKey)
    {
        return data.RootElement.TryGetProperty(fieldKey, out var value) &&
               value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static bool Contains(string value, string query) =>
        value.Contains(query, StringComparison.OrdinalIgnoreCase);

    private static string EscapeLikePattern(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);

    private static int MatchRank(string value, string query) =>
        value.Equals(query, StringComparison.OrdinalIgnoreCase)
            ? 0
            : value.StartsWith(query, StringComparison.OrdinalIgnoreCase)
                ? 1
                : 2;

    private static string CreateSnippet(string value, string query)
    {
        if (value.Length <= SnippetLength)
            return value;

        var matchIndex = value.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        var start = Math.Clamp(matchIndex - SnippetLength / 3, 0, value.Length - SnippetLength);
        var length = Math.Min(SnippetLength, value.Length - start);
        var prefix = start > 0 ? "…" : string.Empty;
        var suffix = start + length < value.Length ? "…" : string.Empty;
        return prefix + value.Substring(start, length) + suffix;
    }
}
