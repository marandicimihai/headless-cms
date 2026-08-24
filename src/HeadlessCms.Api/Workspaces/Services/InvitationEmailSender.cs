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
        var baseUrl = configuration["Workspaces:InvitationUrl"]
                      ?? "http://localhost/invitations/accept";
        var url = $"{baseUrl}?token={Uri.EscapeDataString(token)}";

        logger.LogInformation(
            "Development invitation for {Email} to {WorkspaceName} as {Role}: {InvitationUrl}",
            email,
            workspaceName,
            role,
            url);

        return Task.CompletedTask;
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
