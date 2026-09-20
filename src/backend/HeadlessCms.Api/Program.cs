using HeadlessCms.Api.Caching;
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

var builder = WebApplication.CreateBuilder(args);
builder.AddResourceCache();

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
if (builder.Environment.IsDevelopment())
    builder.Services.AddScoped<IInvitationEmailSender, LoggingInvitationEmailSender>();
else
    builder.Services.AddScoped<IInvitationEmailSender, UnconfiguredInvitationEmailSender>();

var app = builder.Build();

// Validate handler registrations before accepting requests.
app.Services.GetRequiredService<ContentFieldTypeRegistry>();

await app.SeedPlatformAdminUser();

app.UseHttpsRedirection();

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
app.Run();

public partial class Program;
