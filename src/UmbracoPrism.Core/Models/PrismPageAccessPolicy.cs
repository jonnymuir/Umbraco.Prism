namespace UmbracoPrism.Core.Models;

/// <summary>
/// A Prism-owned access policy for a single content node — independent of which Umbraco content
/// type owns that node (deliberately not a property on any one content type; see
/// <see cref="IPrismContext"/>'s own tenant-scoping for the same "Prism config, not content
/// schema" placement). Two independent axes: <see cref="RequiresSignIn"/> (global, no tenant
/// variation) and <see cref="AllowedTenantIds"/> (per-tenant availability — an empty list means
/// available to every tenant).
/// </summary>
public class PrismPageAccessPolicy
{
    public int Id { get; set; }

    public Guid ContentKey { get; set; }

    /// <summary>
    /// Best-effort route for <see cref="ContentKey"/> — bookkeeping only, never consulted for
    /// enforcement. See the schema's own remarks.
    /// </summary>
    public string? ContentRoute { get; set; }

    public bool RequiresSignIn { get; set; }

    /// <summary>
    /// Tenant Names this page is available to. Empty means every tenant. Non-empty and the
    /// current tenant's Name isn't in it (or there is no current tenant) means the page doesn't
    /// exist for this request — see <see cref="PrismPageAccessResult.Unavailable"/>.
    /// </summary>
    public IReadOnlyList<string> AllowedTenantNames { get; set; } = [];
}

/// <summary>
/// The resolved access state for one content node against one request's tenant/authentication
/// state. <see cref="Unavailable"/> means the page should 404 — it doesn't exist for this
/// tenant, so nothing about it (including that it requires sign-in) should be revealed.
/// </summary>
public enum PrismPageAccessResult
{
    Public,
    RequiresSignIn,
    Unavailable
}
