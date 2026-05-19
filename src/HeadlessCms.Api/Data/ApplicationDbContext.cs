using HeadlessCms.Api.Auth.Data;
using Microsoft.EntityFrameworkCore;

namespace HeadlessCms.Api.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : AuthDbContext(options)
{
    
}