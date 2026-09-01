using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace HeadlessCms.Api.Content.Models;

public class ContentField
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }

    public Guid WorkspaceId { get; set; }
    public Guid ProjectId { get; set; }

    public Guid ContentTypeId { get; set; }
    public ContentType ContentType { get; set; } = default!;

    [Required]
    [StringLength(64)]
    public string Key { get; set; } = default!;

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = default!;

    public ContentFieldType Type { get; set; }
    public bool Required { get; set; }
    public bool Nullable { get; set; }
    public int Position { get; set; }

    [Column(TypeName = "jsonb")]
    public JsonElement Settings { get; set; }
}
