using NPoco;
using Umbraco.Cms.Infrastructure.Persistence.DatabaseAnnotations;

namespace UmbracoPrism.Core.Persistence;

/// <summary>
/// Database schema for the prismPageAccessPolicies table — one row per content node that
/// carries a Prism-owned access policy.
/// </summary>
[TableName("prismPageAccessPolicies")]
[PrimaryKey("Id", AutoIncrement = true)]
[ExplicitColumns]
public class PrismPageAccessPolicySchema
{
    [Column("Id")]
    [PrimaryKeyColumn(AutoIncrement = true)]
    public int Id { get; set; }

    /// <summary>The Umbraco content node this policy applies to.</summary>
    [Column("ContentKey")]
    [Index(IndexTypes.UniqueNonClustered)]
    public Guid ContentKey { get; set; }

    /// <summary>
    /// Best-effort route/URL for <see cref="ContentKey"/>, refreshed automatically whenever the
    /// page is (re)published (see PrismPageAccessRouteRefreshHandler) — never read for
    /// enforcement (that's ContentKey-only, so a rename can never un-protect a page), only for
    /// uSync export/import portability across environments, where ContentKey itself is
    /// meaningless (see PrismPageAccessPolicySerializer).
    /// </summary>
    [Column("ContentRoute")]
    [NullSetting(NullSetting = NullSettings.Null)]
    public string? ContentRoute { get; set; }

    [Column("RequiresSignIn")]
    public bool RequiresSignIn { get; set; }

    /// <summary>
    /// JSON array of tenant Names this page is available to — Name, not Id, matching
    /// PrismTenantSerializer's own choice of stable cross-environment tenant identity (Id is a
    /// per-database auto-increment value; Hostname is itself environment-specific). Null/empty
    /// means every tenant.
    /// </summary>
    [Column("TenantAllowListJson")]
    [NullSetting(NullSetting = NullSettings.Null)]
    public string? TenantAllowListJson { get; set; }
}
