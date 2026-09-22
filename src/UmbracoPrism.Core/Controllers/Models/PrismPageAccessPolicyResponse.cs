namespace UmbracoPrism.Core.Controllers.Models;

public class PrismPageAccessPolicyResponse
{
    public int Id { get; set; }
    public Guid ContentKey { get; set; }
    public bool RequiresSignIn { get; set; }
    public string[] AllowedTenantNames { get; set; } = [];

    /// <summary>
    /// The guarded content node's own name, resolved at read time purely for display in the
    /// backoffice list — never stored, and null if the node no longer exists.
    /// </summary>
    public string? ContentName { get; set; }

    /// <summary>Best-effort route, kept fresh on publish — see the schema's own remarks.</summary>
    public string? ContentRoute { get; set; }
}
