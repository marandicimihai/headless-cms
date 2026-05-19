using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace HeadlessCms.Api.Models;

public class Entry : IDisposable
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }
    
    [Required]
    public Guid BlueprintId { get; set; }
    public Blueprint Blueprint { get; set; } = default!;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Required]
    public JsonDocument Data { get; set; } = default!;
    
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        Data?.Dispose();
    }
}