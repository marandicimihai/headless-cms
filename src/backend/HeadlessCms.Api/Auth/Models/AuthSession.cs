using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HeadlessCms.Api.Auth.Models;

public sealed class AuthSession
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }

    [Required]
    [StringLength(64)]
    public string UserId { get; set; } = default!;

    [Required]
    [StringLength(64)]
    public string SecretHash { get; set; } = default!;

    public DateTime CreatedAt { get; set; }
    public DateTime LastSeenAt { get; set; }
    public DateTime IdleExpiresAt { get; set; }
    public DateTime AbsoluteExpiresAt { get; set; }

    public User User { get; set; } = default!;
}
