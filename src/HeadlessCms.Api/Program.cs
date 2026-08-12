using FastEndpoints.Security;
using System.Text.Json.Serialization;
using HeadlessCms.Api.Auth.Models;
using HeadlessCms.Api.Auth.Services;
using HeadlessCms.Api.Content.Services;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Endpoints.Auth;
using HeadlessCms.Api.Workspaces.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var signingKey = builder.Configuration["Auth:SigningKey"] ??
                 throw new InvalidOperationException("Missing configuration Auth:SigningKey");

builder.Services
    .AddAuthenticationJwtBearer(s => s.SigningKey = signingKey) 
    .AddAuthorization()
    .AddFastEndpoints();
builder.Services.ConfigureHttpJsonOptions(
    options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
});

builder.Services.AddScoped<UserManager>();
builder.Services.AddScoped<WorkspaceAccessService>();
builder.Services.AddScoped<WorkspaceInvitationService>();
builder.Services.AddScoped<ContentDocumentValidator>();
builder.Services.AddScoped<ContentDefinitionService>();
builder.Services.AddScoped<ContentEntryService>();
if (builder.Environment.IsDevelopment())
    builder.Services.AddScoped<IInvitationEmailSender, LoggingInvitationEmailSender>();
else
    builder.Services.AddScoped<IInvitationEmailSender, UnconfiguredInvitationEmailSender>();
builder.Services.AddScoped<Refresh>();

var app = builder.Build();

await app.SeedPlatformAdminUser();

app.UseHttpsRedirection();

app.UseAuthentication()
   .UseAuthorization()
   .UseFastEndpoints(
       c =>
       {
           c.Endpoints.RoutePrefix = "api";
           c.Errors.UseProblemDetails();
           c.Serializer.Options.Converters.Add(new JsonStringEnumConverter());
       });
app.Run();

public partial class Program;
