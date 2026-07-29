using HeadlessCms.Api.Auth.Data;
using HeadlessCms.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace HeadlessCms.Api.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : AuthDbContext(options)
{
    public DbSet<Project> Projects { get; set; }
}
