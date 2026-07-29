using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using HeadlessCms.Api.Auth.Models;

namespace HeadlessCms.Api.Tenancy.Models;

public class TenantInvitation
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = default!;

    [Required]
    [StringLength(320)]
    public string Email { get; set; } = default!;

    public TenantRole Role { get; set; }

    [Required]
    [StringLength(64)]
    public string TokenHash { get; set; } = default!;

    public DateTime ExpiresAt { get; set; }
    public DateTime? AcceptedAt { get; set; }

    public string InvitedByUserId { get; set; } = default!;
    public User InvitedByUser { get; set; } = default!;

    public string? AcceptedByUserId { get; set; }
    public User? AcceptedByUser { get; set; }
}
