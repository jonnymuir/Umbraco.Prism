using NPoco;
using Umbraco.Cms.Infrastructure.Persistence.DatabaseAnnotations;

namespace UmbracoPrism.Core.Persistence;

/// <summary>
/// Database schema for the prismCmsWorkflowDefinition table — the authoritative, uSync-portable
/// store for backoffice-authored CMS Workflow definitions (as opposed to MockBusinessApp's
/// memory-only reference store).
/// </summary>
[TableName("prismCmsWorkflowDefinition")]
[PrimaryKey("Id", AutoIncrement = true)]
[ExplicitColumns]
public class PrismCmsWorkflowDefinitionSchema
{
    /// <summary>Gets or sets the unique identifier for the definition record.</summary>
    [Column("Id")]
    [PrimaryKeyColumn(AutoIncrement = true)]
    public int Id { get; set; }

    /// <summary>Gets or sets the workflow's definition key (e.g. "apply-for-a-juggling-licence").</summary>
    [Column("DefinitionKey")]
    [Length(200)]
    [Index(IndexTypes.UniqueNonClustered)]
    public string DefinitionKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the human-readable display name shown in the backoffice list.</summary>
    [Column("DisplayName")]
    [Length(500)]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Gets or sets the full serialized <c>WorkflowDefinitionFile</c> JSON.</summary>
    [Column("Json")]
    public string Json { get; set; } = string.Empty;

    /// <summary>Gets or sets the optimistic-concurrency version — the source of truth for save CAS checks.</summary>
    [Column("Version")]
    public int Version { get; set; }

    /// <summary>Gets or sets the UTC datetime this row was last saved.</summary>
    [Column("UpdatedUtc")]
    [Constraint(Default = "getutcdate()")]
    public DateTime UpdatedUtc { get; set; }
}
