using HeadlessCms.Api.Workspaces.Models;

namespace HeadlessCms.Api.Workspaces.Services;

public interface IInvitationEmailSender
{
    Task SendAsync(
        string email,
        string workspaceName,
        WorkspaceRole role,
        string token,
        CancellationToken ct = default);
}

public sealed class LoggingInvitationEmailSender(
    ILogger<LoggingInvitationEmailSender> logger,
    IConfiguration configuration) : IInvitationEmailSender
{
    public Task SendAsync(
        string email,
        string workspaceName,
        WorkspaceRole role,
        string token,
        CancellationToken ct = default)
    {
        var frontendBaseUrl = configuration["Frontend:BaseUrl"]
                              ?? throw new InvalidOperationException("Frontend:BaseUrl is not configured.");
        var url = BuildInvitationUrl(frontendBaseUrl, token);

        logger.LogInformation(
            "Development invitation for {Email} to {WorkspaceName} as {Role}: {InvitationUrl}",
            email,
            workspaceName,
            role,
            url);

        return Task.CompletedTask;
    }

    private static string BuildInvitationUrl(string frontendBaseUrl, string token)
    {
        var baseUri = new Uri(frontendBaseUrl, UriKind.Absolute);
        var invitationUri = new Uri(baseUri, "/auth/invitations/accept");
        var separator = invitationUri.Query.Length == 0 ? "?" : "&";

        return $"{invitationUri}{separator}token={Uri.EscapeDataString(token)}";
    }
}

public sealed class UnconfiguredInvitationEmailSender : IInvitationEmailSender
{
    public Task SendAsync(
        string email,
        string workspaceName,
        WorkspaceRole role,
        string token,
        CancellationToken ct = default) =>
        throw new InvalidOperationException(
            "No production invitation email sender has been configured.");
}
