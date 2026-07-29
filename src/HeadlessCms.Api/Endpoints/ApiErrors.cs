using FastEndpoints;
using HeadlessCms.Api.Tenancy.Services;
using Microsoft.AspNetCore.WebUtilities;

namespace HeadlessCms.Api.Endpoints;

public sealed record ApiProblem(
    string Type,
    string Title,
    int Status,
    string Code,
    string Detail);

public static class ApiErrors
{
    public static Task SendAsync(
        HttpContext context,
        int statusCode,
        string code,
        string detail,
        CancellationToken ct)
    {
        context.Response.ContentType = "application/problem+json";
        return context.Response.SendAsync(
            new ApiProblem(
                "about:blank",
                ReasonPhrases.GetReasonPhrase(statusCode),
                statusCode,
                code,
                detail),
            statusCode,
            cancellation: ct);
    }

    public static Task SendAsync(
        HttpContext context,
        InvitationFlowException exception,
        CancellationToken ct) =>
        SendAsync(context, exception.StatusCode, exception.Code, exception.Message, ct);
}
