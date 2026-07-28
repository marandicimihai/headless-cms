using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
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
}