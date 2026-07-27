namespace UmbracoPrism.Core.Configuration;

/// <summary>
/// Configuration options for the Prism Workflow Engine.
/// Bind from configuration section "Prism:Workflow".
/// </summary>
public class PrismServiceDesignOptions
{
    /// <summary>
    /// How long a workflow step nonce remains valid in the distributed cache.
    /// Defaults to 2 hours. Increase for slow multi-step workflows.
    /// </summary>
    public TimeSpan NonceExpiry { get; set; } = TimeSpan.FromHours(2);
}
