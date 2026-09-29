using FastEndpoints;
using HeadlessCms.Api.Workspaces.Services;

using HeadlessCms.Api.Documentation;

namespace HeadlessCms.Api.Endpoints.Workspaces;

public sealed class ResendWorkspaceInvitation(
    WorkspaceInvitationService invitations)
    : EndpointWithoutRequest<WorkspaceInvitationResponse>
{
    public override void Configure()
    {
        Post("workspaces/{workspaceId:guid}/invitations/{invitationId:guid}/resend");
        Claims("sub");
        Description(b => b.WithTags("Invitations"), clearDefaults: true);
        Summary((EndpointSummary s) =>
        {
            s.Summary = "Resend an invitation";
            s.Description = "Access: Workspace Owner; PlatformAdmin may manage owner invitations only in an ownerless workspace.\n\nRotates the single-use token, renews expiry, and returns a new invitationUrl. The old link stops working. Expired invitations may be resent; accepted return 409 and revoked return 410.";
            s.Params["workspaceId"] = "Workspace UUID.";
            s.Params["invitationId"] = "Invitation UUID.";
            s.Response<WorkspaceInvitationResponse>(200, "Success.");
            s.ResponseExamples[200] = ApiExamples.InvitationWithLink;
            s.Response<FastEndpoints.ProblemDetails>(400, "Invalid request or validation failure.", "application/problem+json");
            s.Response<ApiProblem>(401, "Authentication is required, or the session has expired.", "application/problem+json");
            s.Response(404, "Resource not found or inaccessible.");
            s.Response<ApiProblem>(409, "Conflicting membership or invitation state.", "application/problem+json");
            s.Response<ApiProblem>(410, "Invitation expired or revoked.", "application/problem+json");
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var workspaceId = Route<Guid>("workspaceId");
        var invitationId = Route<Guid>("invitationId");
        var invitation = await invitations.FindManageableAsync(User, workspaceId, invitationId, ct);
        if (invitation is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        try
        {
            var created = await invitations.ResendAsync(invitation, ct);
            HttpContext.Response.Headers.CacheControl = "no-store";
            Response = WorkspaceInvitationResponse.FromInvitation(invitation) with
            { InvitationUrl = invitations.GetInvitationUrl(created.Token) };
        }
        catch (InvitationFlowException exception)
        {
            await ApiErrors.SendAsync(HttpContext, exception, ct);
        }
    }

}
