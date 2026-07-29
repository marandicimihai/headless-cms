using HeadlessCms.Api.Auth.Models;

namespace HeadlessCms.Api.Tenancy.Models;

public class TenantMembership
{
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = default!;

    public string UserId { get; set; } = default!;
    public User User { get; set; } = default!;

    public TenantRole Role { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}
