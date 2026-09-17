using HeadlessCms.Api.Content.Models;
using HeadlessCms.Api.Content.Services;

namespace HeadlessCms.Api.Content.FieldTypes;

public sealed class ContentFieldTypeRegistry
{
    private readonly IReadOnlyDictionary<ContentFieldType, IContentFieldTypeHandler> handlers;

    public ContentFieldTypeRegistry(IEnumerable<IContentFieldTypeHandler> registrations)
    {
        var registered = new Dictionary<ContentFieldType, IContentFieldTypeHandler>();
        foreach (var handler in registrations)
        {
            if (!registered.TryAdd(handler.Type, handler))
                throw new InvalidOperationException($"Field type '{handler.Type}' has multiple registered handlers.");
        }
        handlers = registered;
    }

    public ContentFieldType[] TextSearchTypes => handlers.Values
        .Where(handler => handler.SupportsTextSearch)
        .Select(handler => handler.Type)
        .ToArray();

    public IContentFieldTypeHandler Get(ContentFieldType type) =>
        handlers.TryGetValue(type, out var handler)
            ? handler
            : throw new ContentValidationException($"Field type '{type}' is not registered.");
}
