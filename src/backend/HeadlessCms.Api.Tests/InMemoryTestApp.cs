using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using FastEndpoints.Testing;
using HeadlessCms.Api.Auth.Models;
using HeadlessCms.Api.Auth.Services;
using HeadlessCms.Api.Ai;
using HeadlessCms.Api.Content.Models;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Endpoints.Auth;
using HeadlessCms.Api.Workspaces.Models;
using HeadlessCms.Api.Workspaces.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace HeadlessCms.Api.Tests;

public class InMemoryTestApp : AppFixture<Program>
{
    private const string AuthenticationScheme = "TestAuthentication";
    private const string UserHeader = "X-Test-User";
    internal const string DatabaseHeader = "X-Test-Database";
    public HttpClient HttpsClient { get; private set; } = null!;

    protected override void ConfigureApp(IWebHostBuilder builder) =>
        builder.UseEnvironment("Testing");

    protected override void ConfigureServices(IServiceCollection services)
    {
        services.RemoveAll<ApplicationDbContext>();
        services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
        services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
        services.AddSingleton<TestDatabaseAccessor>();
        services.AddHttpContextAccessor();
        services.AddDbContext<ApplicationDbContext>((provider, options) =>
            options.UseInMemoryDatabase(
                provider.GetRequiredService<TestDatabaseAccessor>().CurrentDatabaseName));

        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = AuthenticationScheme;
                options.DefaultChallengeScheme = AuthenticationScheme;
                options.DefaultForbidScheme = AuthenticationScheme;
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                AuthenticationScheme,
                _ => { });

        services.RemoveAll<IInvitationEmailSender>();
        services.AddSingleton<TestInvitationEmailSender>();
        services.AddSingleton<IInvitationEmailSender>(
            provider => provider.GetRequiredService<TestInvitationEmailSender>());
    }

    protected override ValueTask SetupAsync()
    {
        HttpsClient = CreateClient(new ClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
            BaseAddress = new Uri("https://localhost")
        });
        return ValueTask.CompletedTask;
    }

    protected override ValueTask TearDownAsync()
    {
        HttpsClient.Dispose();
        return ValueTask.CompletedTask;
    }

    public string BeginTestDatabase()
    {
        return Services.GetRequiredService<TestDatabaseAccessor>().CurrentDatabaseName;
    }

    public async Task InitializeDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureCreatedAsync();
        Services.GetRequiredService<TestInvitationEmailSender>().Clear();
    }

    public async Task CleanupDatabaseAsync(string databaseName)
    {
        var accessor = Services.GetRequiredService<TestDatabaseAccessor>();
        if (accessor.CurrentDatabaseName != databaseName)
            throw new InvalidOperationException("Attempted to clean up another test's database.");
        using var scope = Services.CreateScope();
        await scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>()
            .Database
            .EnsureDeletedAsync();
    }

    public async Task<User> SeedUserAsync(
        string identifier,
        string password,
        PlatformRole platformRole = PlatformRole.User,
        string? email = null)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        var user = new User
        {
            Email = EmailNormalizer.Normalize(email ?? TestApp.AsEmail(identifier)),
            PlatformRole = platformRole
        };
        user.PasswordHash = hasher.HashPassword(user, password);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    public Task<Workspace> SeedWorkspaceAsync(params (User User, WorkspaceRole Role)[] members) =>
        WithDatabaseAsync(async db =>
        {
            var workspace = new Workspace
            {
                Name = $"Workspace {Guid.NewGuid():N}",
                Memberships = members.Select(member => new WorkspaceMembership
                {
                    UserId = member.User.Id,
                    Role = member.Role
                }).ToList()
            };
            db.Workspaces.Add(workspace);
            await db.SaveChangesAsync();
            return workspace;
        });

    public Task<Project> SeedProjectAsync(Guid workspaceId, string name) =>
        WithDatabaseAsync(async db =>
        {
            var now = DateTime.UtcNow;
            var project = new Project
            {
                WorkspaceId = workspaceId,
                Name = name,
                CreatedAt = now,
                UpdatedAt = now
            };
            db.Projects.Add(project);
            await db.SaveChangesAsync();
            return project;
        });

    public async Task<string> LoginAsync(string identifier, string password)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        var email = EmailNormalizer.Normalize(TestApp.AsEmail(identifier));
        var user = await db.Users.SingleAsync(candidate => candidate.Email == email);
        if (hasher.VerifyHashedPassword(user, user.PasswordHash, password)
            == PasswordVerificationResult.Failed)
            throw new InvalidOperationException("Test credentials are invalid.");
        return user.Id;
    }

    public async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        string userId,
        object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Add(UserHeader, userId);
        request.Headers.Add(
            DatabaseHeader,
            Services.GetRequiredService<TestDatabaseAccessor>().CurrentDatabaseName);
        if (body is not null)
            request.Content = JsonContent.Create(body);
        return await HttpsClient.SendAsync(request);
    }

    public async Task<HttpResponseMessage> SendAnonymousAsync(
        HttpMethod method,
        string path,
        object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Add(
            DatabaseHeader,
            Services.GetRequiredService<TestDatabaseAccessor>().CurrentDatabaseName);
        if (body is not null)
            request.Content = JsonContent.Create(body);
        return await HttpsClient.SendAsync(request);
    }

    public async Task<TResult> WithDatabaseAsync<TResult>(
        Func<ApplicationDbContext, Task<TResult>> action)
    {
        using var scope = Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    public async Task<TResult> WithServiceAsync<TService, TResult>(
        Func<TService, Task<TResult>> action) where TService : notnull
    {
        using var scope = Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<TService>());
    }

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ApplicationDbContext db)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var userId = Request.Headers[UserHeader].SingleOrDefault();
            if (userId is null)
                return AuthenticateResult.NoResult();
            var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(
                candidate => candidate.Id == userId,
                Context.RequestAborted);
            if (user is null)
                return AuthenticateResult.NoResult();
            var identity = new ClaimsIdentity(
                [new Claim("sub", user.Id), new Claim("email", user.Email),
                 new Claim("role", user.PlatformRole.ToString())],
                Scheme.Name,
                "email",
                "role");
            return AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
        }
    }
}

public sealed class TestDatabaseAccessor(IHttpContextAccessor httpContextAccessor)
{
    public string CurrentDatabaseName
    {
        get
        {
            var requestDatabase = httpContextAccessor.HttpContext?
                .Request.Headers[InMemoryTestApp.DatabaseHeader]
                .SingleOrDefault();
            if (!string.IsNullOrWhiteSpace(requestDatabase))
                return requestDatabase;
            return TestContext.Current.TestCase is { } testCase
                ? $"headless-cms-test-{testCase.UniqueID}"
                : "headless-cms-test-bootstrap";
        }
    }
}

public sealed class WorkspaceEndpointTestApp : InMemoryTestApp;
public sealed class WorkspaceMembershipTestApp : InMemoryTestApp;
public sealed class WorkspaceAdministrationTestApp : InMemoryTestApp;
public sealed class ProjectEndpointTestApp : InMemoryTestApp;
public sealed class ContentEndpointTestApp : InMemoryTestApp
{
    protected override void ConfigureServices(IServiceCollection services)
    {
        base.ConfigureServices(services);
        services.RemoveAll<ISchemaAssistantClient>();
        services.AddScoped<ISchemaAssistantClient, TestSchemaAssistantClient>();
    }
}

public sealed class TestSchemaAssistantClient : ISchemaAssistantClient
{
    public Task<SchemaAssistantProposal> GenerateAsync(
        IReadOnlyList<SchemaAssistantChatMessage> messages,
        IReadOnlyCollection<string> existingKeys,
        CancellationToken cancellationToken)
    {
        var key = existingKeys.Contains("articles", StringComparer.Ordinal)
            ? "articles"
            : "generated_posts";
        var proposal = new SchemaAssistantProposal(
            "I drafted one content type for your project.",
            [new SchemaAssistantTypeProposal(
                key,
                [new SchemaAssistantFieldProposal("title", "text", true, null)])]);
        return Task.FromResult(proposal);
    }
}
public sealed class RemainingEndpointTestApp : InMemoryTestApp;
