namespace UmbracoPrism.Core.Controllers.Models;

public class PrismPageAccessPolicyRequest
{
    public Guid ContentKey { get; set; }
    public bool RequiresSignIn { get; set; }

    /// <summary>Empty/null means available to every tenant. Tenant Name, not Id — see
    /// PrismPageAccessPolicySchema's own remarks on why Name is the stable reference.</summary>
    public string[]? AllowedTenantNames { get; set; }
}
