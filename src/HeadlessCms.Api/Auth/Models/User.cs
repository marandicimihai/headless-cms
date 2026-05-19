using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace HeadlessCms.Api.Auth.Models;

[PrimaryKey(nameof(Email))]
[Index(nameof(Email), IsUnique = true)]
public class User
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = default!;
}