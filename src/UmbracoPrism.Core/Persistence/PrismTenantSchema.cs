using Umbraco.Cms.Infrastructure.Persistence.DatabaseAnnotations;
using NPoco;

namespace UmbracoPrism.Core.Persistence;

/// <summary>
/// Database schema for the PrismTenant table.
/// </summary>
[TableName("prismTenants")]
[PrimaryKey("id", AutoIncrement = true)]
[ExplicitColumns]
public class PrismTenantSchema
{
    /// <summary>
    /// Gets or sets the unique identifier for the tenant.
    /// </summary>
    [Column("id")]
    [PrimaryKeyColumn(AutoIncrement = true)]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the name of the tenant.
    /// </summary>
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the hostname associated with the tenant.
    /// </summary>
    [Column("hostname")]
    [Index(IndexTypes.UniqueNonClustered)]
    public string Hostname { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the theme color for the tenant's branding.
    /// </summary>
    [Column("themeColor")]
    public string ThemeColor { get; set; } = "#3490dc";
}