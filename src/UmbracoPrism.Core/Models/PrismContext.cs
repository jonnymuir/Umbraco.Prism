namespace UmbracoPrism.Core.Models;

/// <summary>
/// Context implementation for managing the current tenant.
/// </summary>
public class PrismContext : IPrismContext
{
    /// <summary>
    /// Gets or sets the current tenant.
    /// </summary>
    public PrismTenant? CurrentTenant { get; set; }
}