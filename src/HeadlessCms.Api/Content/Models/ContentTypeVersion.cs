using System.ComponentModel.DataAnnotations.Schema;

namespace HeadlessCms.Api.Content.Models;

public class ContentTypeVersion
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }

    public Guid WorkspaceId { get; set; }
    public Guid ProjectId { get; set; }

    public Guid ContentTypeId { get; set; }
    public ContentType ContentType { get; set; } = default!;

    public int Version { get; set; }
    public DateTime CreatedAt { get; set; }

    public List<ContentField> Fields { get; set; } = [];
    public List<ContentEntry> Entries { get; set; } = [];
}
