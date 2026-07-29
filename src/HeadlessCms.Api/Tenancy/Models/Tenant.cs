using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using HeadlessCms.Api.Content.Models;

namespace HeadlessCms.Api.Tenancy.Models;

public class Tenant
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = default!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<TenantMembership> Memberships { get; set; } = [];
    public List<TenantInvitation> Invitations { get; set; } = [];
    public List<Project> Projects { get; set; } = [];
    public List<ContentType> ContentTypes { get; set; } = [];
    public List<ContentEntry> ContentEntries { get; set; } = [];
}
