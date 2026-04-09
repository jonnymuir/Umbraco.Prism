using NPoco;
using Umbraco.Cms.Infrastructure.Persistence.DatabaseAnnotations;

namespace UmbracoPrism.Core.Persistence;

/// <summary>
/// Database schema for the prismFieldGroupDefinitions table.
/// </summary>
[TableName("prismFieldGroupDefinitions")]
[PrimaryKey("Id", AutoIncrement = true)]
[ExplicitColumns]
public class PrismFieldGroupDefinitionSchema
{
    /// <summary>
    /// Gets or sets the database primary key.
    /// </summary>
    [Column("Id")]
    [PrimaryKeyColumn(AutoIncrement = true)]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the field group key (stable identifier across versions).
    /// </summary>
    [Column("FieldGroupKey")]
    [Length(255)]
    public string FieldGroupKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the semantic version (e.g., "1.0.0", "2.1.0").
    /// </summary>
    [Column("Version")]
    [Length(50)]
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the tenant identifier for multi-tenant isolation.
    /// </summary>
    [Column("TenantId")]
    [Length(450)]
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display title for the field group.
    /// </summary>
    [Column("Title")]
    [Length(500)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    [Column("Description")]
    [NullSetting(NullSetting = NullSettings.Null)]
    [SpecialDbType(SpecialDbTypes.NVARCHARMAX)]
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the status: Draft, Published, Retired.
    /// </summary>
    [Column("Status")]
    [Length(50)]

    public string Status { get; set; } = "Draft";

    /// <summary>
    /// Gets or sets the fields collection as JSON.
    /// </summary>
    [Column("FieldsJson")]
    [SpecialDbType(SpecialDbTypes.NVARCHARMAX)]
    public string FieldsJson { get; set; } = "[]";

    /// <summary>
    /// Gets or sets the UTC timestamp when this definition was created.
    /// </summary>
    [Column("CreatedAt")]
    [Constraint(Default = "getutcdate()")]
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when this definition was published. Null if never published.
    /// </summary>
    [Column("PublishedAt")]
    [NullSetting(NullSetting = NullSettings.Null)]
    public DateTime? PublishedAt { get; set; }

    /// <summary>
    /// Gets or sets the user ID who created this definition.
    /// </summary>
    [Column("CreatedBy")]
    [Length(450)]
    public string CreatedBy { get; set; } = string.Empty;
}
