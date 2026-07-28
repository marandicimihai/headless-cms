using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HeadlessCms.Api.Auth.Models;

public class RefreshToken
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string Id { get; set; } = default!;

    [Required]
    [StringLength(64)]
    public string UserId { get; set; } = default!;
    
    [Required]
    public string TokenHash { get; set; } = default!;

    [Required]
    public DateTime Expiry { get; set; }
}