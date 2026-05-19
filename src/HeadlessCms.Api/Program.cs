using HeadlessCms.Api.Data;
using Microsoft.EntityFrameworkCore;

var bld = WebApplication.CreateBuilder(args);
bld.Services
    .AddAuthorization()
    .AddFastEndpoints(o => o.SourceGeneratorDiscoveredTypes = DiscoveredTypes.All);

bld.Services.ConfigureDataServices(bld.Configuration);

var app = bld.Build();

await app.SeedData();

app.UseAuthentication()
   .UseAuthorization()
   .UseFastEndpoints(
       c =>
       {
           c.Errors.UseProblemDetails();
       });
app.Run();