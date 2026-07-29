using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using HeadlessCms.Api.Auth.Models;
using Microsoft.EntityFrameworkCore;

namespace HeadlessCms.Api.Models;

[Index(nameof(OwnerId))]
public class Project
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 3)]
    public string Name { get; set; } = default!;

    [Required]
    public string OwnerId { get; set; } = default!;

    public User Owner { get; set; } = default!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
