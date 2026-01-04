namespace UmbracoPrism.Core.Models;

/// <summary>
/// Context interface for managing the current tenant.
/// </summary>
public interface IPrismContext
{
    /// <summary>
    /// Gets or sets the current tenant.
    /// </summary>
    PrismTenant? CurrentTenant { get; set; }
}
