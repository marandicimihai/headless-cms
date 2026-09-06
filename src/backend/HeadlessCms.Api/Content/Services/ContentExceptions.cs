namespace HeadlessCms.Api.Content.Services;

public sealed class ContentValidationException(IReadOnlyList<string> errors)
    : Exception(errors.FirstOrDefault() ?? "The content request is invalid.")
{
    public IReadOnlyList<string> Errors { get; } = errors;

    public ContentValidationException(string error)
        : this([error])
    {
    }
}

public sealed class ContentConflictException(string message) : Exception(message);

public sealed class ContentNotFoundException(string message) : Exception(message);
