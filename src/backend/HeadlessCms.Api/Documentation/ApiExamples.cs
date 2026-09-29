namespace HeadlessCms.Api.Documentation;

// Synthetic examples only. Shapes are checked against the generated schemas in contract tests.
internal static class ApiExamples
{
    public static readonly Guid WorkspaceId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    public static readonly Guid ProjectId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    public static readonly Guid ContentTypeId = Guid.Parse("33333333-3333-4333-8333-333333333333");
    public static readonly Guid EntryId = Guid.Parse("44444444-4444-4444-8444-444444444444");
    public static readonly Guid InvitationId = Guid.Parse("55555555-5555-4555-8555-555555555555");
    public const string UserId = "example-user";
    public const string Timestamp = "2026-09-21T12:00:00Z";
    public static readonly object Session = new
    {
        UserId, Email = "editor@example.com", PlatformRole = "user",
        IdleExpiresAt = "2026-10-21T12:00:00Z", AbsoluteExpiresAt = "2027-09-21T12:00:00Z"
    };
    public static readonly object Workspace = new { Id = WorkspaceId, Name = "Editorial", CreatedAt = Timestamp, CurrentRole = "owner" };
    public static readonly object AdminWorkspace = new { Id = WorkspaceId, Name = "Editorial", CreatedAt = Timestamp, CurrentRole = (string?)null };
    public static readonly object Project = new { Id = ProjectId, WorkspaceId, Name = "Website", CreatedAt = Timestamp, UpdatedAt = Timestamp };
    public static readonly object Member = new { UserId, Email = "editor@example.com", Role = "editor", JoinedAt = Timestamp };
    public static readonly object Membership = new { WorkspaceId, WorkspaceName = "Editorial", Role = "editor", JoinedAt = Timestamp };
    public static readonly object Invitation = new
    {
        Id = InvitationId, WorkspaceId, Email = "editor@example.com", Role = "editor", Status = "pending",
        CreatedAt = Timestamp, ExpiresAt = "2026-09-28T12:00:00Z", LastSentAt = Timestamp,
        AcceptedAt = (string?)null, RevokedAt = (string?)null
    };
    public static readonly object InvitationWithLink = new
    {
        Id = InvitationId, WorkspaceId, Email = "editor@example.com", Role = "editor", Status = "pending",
        CreatedAt = Timestamp, ExpiresAt = "2026-09-28T12:00:00Z", LastSentAt = Timestamp,
        AcceptedAt = (string?)null, RevokedAt = (string?)null,
        InvitationUrl = "https://cms.example.com/auth/invitations/accept?token=EXAMPLE_TOKEN"
    };
    public static readonly object[] Fields = [new { Key = "title", Type = "text", Required = true, Settings = new { } }];
    public static readonly object ContentType = new
    {
        Id = ContentTypeId, ProjectId, Key = "articles", CreatedAt = Timestamp, UpdatedAt = Timestamp,
        Fields = new[] { new { Key = "title", Type = "text", Required = true, Position = 0, Settings = new { } } }
    };
    public static readonly object EntryData = new { Title = "Hello world" };
    public static readonly object Entry = new { Id = EntryId, Status = "draft", Data = EntryData, CreatedAt = Timestamp, UpdatedAt = Timestamp };
    public static object Page(object item, int pageSize = 20) => new { Items = new[] { item }, Page = 1, PageSize = pageSize, Total = 1 };
}
