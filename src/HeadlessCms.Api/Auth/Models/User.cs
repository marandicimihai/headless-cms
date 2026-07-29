using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using HeadlessCms.Api.Tenancy.Models;
using Microsoft.EntityFrameworkCore;

namespace HeadlessCms.Api.Auth.Models;

[Index(nameof(Username), IsUnique = true)]
public class User
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string Id { get; set; } = default!;
    
    [Required]
    [StringLength(64)]
    public string Username { get; set; } = default!;

    [Required]
    [StringLength(256)]
    public string PasswordHash { get; set; } = default!;

    [StringLength(320)]
    public string? Email { get; set; }

    [Required]
    public PlatformRole PlatformRole { get; set; } = PlatformRole.User;

    public List<TenantMembership> TenantMemberships { get; set; } = [];
}
