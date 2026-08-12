using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace HeadlessCms.Api.Content.Models;

public class ContentEntry : IDisposable
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }

    public Guid WorkspaceId { get; set; }
    public Guid ProjectId { get; set; }

    public Guid ContentTypeId { get; set; }
    public ContentType ContentType { get; set; } = default!;

    public Guid ContentTypeVersionId { get; set; }
    public ContentTypeVersion ContentTypeVersion { get; set; } = default!;

    [Required]
    [Column(TypeName = "jsonb")]
    public JsonDocument Data { get; set; } = default!;

    public ContentEntryStatus Status { get; set; } = ContentEntryStatus.Draft;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public void Dispose()
    {
        Data?.Dispose();
        GC.SuppressFinalize(this);
    }
}
