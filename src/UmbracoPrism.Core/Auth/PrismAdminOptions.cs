namespace UmbracoPrism.Core.Auth;

/// <summary>
/// Configuration options for Prism administrative authorization.
/// </summary>
public class PrismAdminOptions
{
    /// <summary>
    /// Gets or sets the allowed Umbraco backoffice group aliases for Prism admin access.
    /// </summary>
    public string[] GroupAliases { get; set; } = ["admin"];
}
