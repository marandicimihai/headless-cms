using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using HeadlessCms.Api.Content.Models;
using HeadlessCms.Api.Tenancy.Models;
using Microsoft.EntityFrameworkCore;

namespace HeadlessCms.Api.Models;

[Index(nameof(TenantId))]
public class Project
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 3)]
    public string Name { get; set; } = default!;

    [Required]
    public Guid TenantId { get; set; }

    public Tenant Tenant { get; set; } = default!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public List<ContentType> ContentTypes { get; set; } = [];
}
