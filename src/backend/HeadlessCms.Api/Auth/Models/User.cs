using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using HeadlessCms.Api.Workspaces.Models;

namespace HeadlessCms.Api.Auth.Models;

public class User
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string Id { get; set; } = default!;
    
    [Required]
    [StringLength(256)]
    public string PasswordHash { get; set; } = default!;

    [Required]
    [StringLength(320)]
    public string Email { get; set; } = default!;

    [Required]
    public PlatformRole PlatformRole { get; set; } = PlatformRole.User;

    public List<WorkspaceMembership> WorkspaceMemberships { get; set; } = [];
}
