namespace UmbracoPrism.Core.Services.Workflow;

/// <summary>
/// Service for loading and seeding workflow definitions from JSON files.
/// </summary>
public interface IWorkflowSeedService
{
    /// <summary>
    /// Seeds workflow definitions and field groups from embedded resources or filesystem.
    /// Idempotent - only seeds if definitions don't already exist.
    /// </summary>
    Task SeedAsync(CancellationToken cancellationToken = default);
}
