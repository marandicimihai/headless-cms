using HeadlessCms.Api.Tenancy.Models;

namespace HeadlessCms.Api.Tenancy.Services;

public interface IInvitationEmailSender
{
    Task SendAsync(
        string email,
        string tenantName,
        TenantRole role,
        string token,
        CancellationToken ct = default);
}

public sealed class LoggingInvitationEmailSender(
    ILogger<LoggingInvitationEmailSender> logger,
    IConfiguration configuration) : IInvitationEmailSender
{
    public Task SendAsync(
        string email,
        string tenantName,
        TenantRole role,
        string token,
        CancellationToken ct = default)
    {
        var baseUrl = configuration["Tenancy:InvitationUrl"]
                      ?? "https://localhost/invitations/accept";
        var url = $"{baseUrl}?token={Uri.EscapeDataString(token)}";

        logger.LogInformation(
            "Development invitation for {Email} to {TenantName} as {Role}: {InvitationUrl}",
            email,
            tenantName,
            role,
            url);

        return Task.CompletedTask;
    }
}

public sealed class UnconfiguredInvitationEmailSender : IInvitationEmailSender
{
    public Task SendAsync(
        string email,
        string tenantName,
        TenantRole role,
        string token,
        CancellationToken ct = default) =>
        throw new InvalidOperationException(
            "No production invitation email sender has been configured.");
}
