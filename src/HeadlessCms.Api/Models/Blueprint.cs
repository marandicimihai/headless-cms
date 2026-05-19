using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HeadlessCms.Api.Models;

public class Blueprint
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }
    
    [Required]
    [Length(3, 50)]
    public string Name { get; set; } = default!;

    public List<Column> Columns { get; set; } = [];
}