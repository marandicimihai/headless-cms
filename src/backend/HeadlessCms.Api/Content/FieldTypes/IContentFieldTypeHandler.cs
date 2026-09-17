using System.Text.Json;
using HeadlessCms.Api.Content.Models;
using HeadlessCms.Api.Content.Services;

namespace HeadlessCms.Api.Content.FieldTypes;

public interface IContentFieldTypeHandler
{
    ContentFieldType Type { get; }
    bool SupportsTextSearch { get; }
    void ValidateValue(ContentField field, JsonElement value, ICollection<string> errors);
    IQueryable<ContentEntry> ApplyFilter(IQueryable<ContentEntry> query, string key, string operation, string value);
    IOrderedQueryable<ContentEntry> ApplySort(IQueryable<ContentEntry> query, string key, bool descending);
}
