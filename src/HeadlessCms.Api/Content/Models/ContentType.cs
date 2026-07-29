using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using HeadlessCms.Api.Models;
using HeadlessCms.Api.Tenancy.Models;

namespace HeadlessCms.Api.Content.Models;

public class ContentType
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = default!;

    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = default!;

    [Required]
    [StringLength(64)]
    public string Key { get; set; } = default!;

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = default!;

    public Guid? CurrentVersionId { get; set; }
    public ContentTypeVersion? CurrentVersion { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<ContentTypeVersion> Versions { get; set; } = [];
    public List<ContentEntry> Entries { get; set; } = [];
}
