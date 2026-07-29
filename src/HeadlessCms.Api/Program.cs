using FastEndpoints.Security;
using HeadlessCms.Api.Auth.Models;
using HeadlessCms.Api.Auth.Services;
using HeadlessCms.Api.Content.Services;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Tenancy.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var signingKey = builder.Configuration["Auth:SigningKey"] ??
                 throw new InvalidOperationException("Missing configuration Auth:SigningKey");

builder.Services
    .AddAuthenticationJwtBearer(s => s.SigningKey = signingKey) 
    .AddAuthorization()
    .AddFastEndpoints();

builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
});

builder.Services.AddScoped<UserManager>();
builder.Services.AddScoped<TenantAccessService>();
builder.Services.AddScoped<TenantInvitationService>();
builder.Services.AddScoped<ContentDocumentValidator>();
builder.Services.AddScoped<ContentDefinitionService>();
builder.Services.AddScoped<ContentEntryService>();

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
       });
app.Run();

public partial class Program;
