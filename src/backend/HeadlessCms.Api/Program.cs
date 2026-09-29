using System.Text.Json;
using System.Text.Json.Serialization;
using HeadlessCms.Api.Auth;
using HeadlessCms.Api.Auth.Models;
using HeadlessCms.Api.Auth.Services;
using HeadlessCms.Api.Content.Services;
using HeadlessCms.Api.Content.FieldTypes;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Endpoints.Auth;
using HeadlessCms.Api.Workspaces.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.HttpOverrides;
using System.Net;
using HeadlessCms.Api.Documentation;

if (args.Contains("--initialize", StringComparer.Ordinal))
{
    try
    {
        await DatabaseInitializer.RunAsync(args.Where(arg => arg != "--initialize").ToArray());
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"Database initialization failed: {exception.Message}");
        Environment.ExitCode = 1;
    }
    return;
}

var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(30));
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.ForwardLimit = 1;
    foreach (var proxy in builder.Configuration.GetSection("Proxy:KnownProxies").Get<string[]>() ?? [])
        o.KnownProxies.Add(IPAddress.Parse(proxy));
});
if (builder.Environment.IsProduction())
{
    builder.Logging.ClearProviders();
    builder.Logging.AddJsonConsole();
}
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = SessionAuthenticationDefaults.Scheme;
        options.DefaultChallengeScheme = SessionAuthenticationDefaults.Scheme;
        options.DefaultForbidScheme = SessionAuthenticationDefaults.Scheme;
    })
    .AddScheme<AuthenticationSchemeOptions, SessionAuthenticationHandler>(
        SessionAuthenticationDefaults.Scheme,
        _ => { });
builder.Services
    .AddAuthorization()
    .AddFastEndpoints();
builder.Services.AddApiDocumentation();
builder.Services.ConfigureHttpJsonOptions(
    options => options.SerializerOptions.Converters.Add(
        new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false)));

builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
});

builder.Services.AddScoped<UserManager>();
builder.Services.AddScoped<AuthSessionService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<WorkspaceAccessService>();
builder.Services.AddScoped<WorkspaceInvitationService>();
builder.Services.AddScoped<WorkspaceOwnershipLimitService>();
builder.Services.AddSingleton<IContentFieldTypeHandler, TextFieldTypeHandler>();
builder.Services.AddSingleton<IContentFieldTypeHandler, NumberFieldTypeHandler>();
builder.Services.AddSingleton<IContentFieldTypeHandler, BooleanFieldTypeHandler>();
builder.Services.AddSingleton<ContentFieldTypeRegistry>();
builder.Services.AddScoped<ContentDocumentValidator>();
builder.Services.AddScoped<ContentDefinitionService>();
builder.Services.AddScoped<ContentEntryService>();
builder.Services.AddScoped<WorkspaceSearchService>();
builder.Services.AddScoped<IInvitationEmailSender, LinkOnlyInvitationEmailSender>();
if (!builder.Environment.IsEnvironment("Testing") &&
    (!Uri.TryCreate(builder.Configuration["Frontend:BaseUrl"], UriKind.Absolute, out var frontendUrl) ||
     (builder.Environment.IsProduction() && frontendUrl.Scheme != "https")))
    throw new InvalidOperationException("Frontend:BaseUrl must be a valid URL (HTTPS in production).");

var app = builder.Build();

// Validate handler registrations before accepting requests.
app.Services.GetRequiredService<ContentFieldTypeRegistry>();

// HTTPS is enforced by Caddy. Internal HTTP and health probes must not redirect.
app.UseForwardedHeaders();

app.MapGet("/health/live", () => Results.Ok(new { status = "healthy" }))
    .AllowAnonymous();
app.MapGet(
        "/health/ready",
        async (ApplicationDbContext db, CancellationToken cancellationToken) =>
            await db.Database.CanConnectAsync(cancellationToken)
                ? Results.Ok(new { status = "healthy" })
                : Results.StatusCode(StatusCodes.Status503ServiceUnavailable))
    .AllowAnonymous();

app.UseAuthentication()
   .UseAuthorization()
   .UseFastEndpoints(
       c =>
       {
           c.Endpoints.RoutePrefix = "api";
           c.Errors.UseProblemDetails();
           c.Serializer.Options.Converters.Add(
               new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
       });
// Only Caddy's /api/* boundary is public; the frontend reads this over the service network.
app.UseOpenApi(options => options.Path = "/openapi/{documentName}.json");
app.Run();

public partial class Program;
