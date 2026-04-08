using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace UmbracoPrism.Core.Services.Workflow;

/// <summary>
/// Background service that seeds workflow definitions on application startup.
/// Runs once during application initialization.
/// </summary>
public class WorkflowSeedService : IHostedService
{
    private readonly IWorkflowSeedService _seedService;
    private readonly ILogger<WorkflowSeedService> _logger;

    public WorkflowSeedService(
        IWorkflowSeedService seedService,
        ILogger<WorkflowSeedService> logger)
    {
        _seedService = seedService;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Workflow seed service starting - seeding workflow definitions");

        try
        {
            await _seedService.SeedAsync(cancellationToken);
            _logger.LogInformation("Workflow definitions seeded successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to seed workflow definitions");
            // Don't throw - allow app to continue even if seeding fails
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Workflow seed service stopping");
        return Task.CompletedTask;
    }
}
