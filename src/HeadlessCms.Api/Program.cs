using HeadlessCms.Api.Data;
using Microsoft.EntityFrameworkCore;

var bld = WebApplication.CreateBuilder(args);
bld.Services
    .AddAuthorization()
    .AddFastEndpoints(o => o.SourceGeneratorDiscoveredTypes = DiscoveredTypes.All);

bld.Services
    .AddDbContext<ApplicationDbContext>(options =>
    {
        options.UseNpgsql(bld.Configuration.GetConnectionString("DefaultConnection"));
    });

var app = bld.Build();
app.UseAuthentication()
   .UseAuthorization()
   .UseFastEndpoints(
       c =>
       {
           c.Errors.UseProblemDetails();
       });
app.Run();