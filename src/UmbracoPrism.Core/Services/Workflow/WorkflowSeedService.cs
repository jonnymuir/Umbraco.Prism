using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace UmbracoPrism.Core.Services.Workflow;

/// <summary>
/// Background service that seeds workflow definitions on application startup.
/// Runs once during application initialization.
/// </summary>
public class WorkflowSeedService : IHostedService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<WorkflowSeedService> _logger;

    public WorkflowSeedService(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<WorkflowSeedService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Workflow seed service starting - seeding workflow definitions");

        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var seedService = scope.ServiceProvider.GetRequiredService<IWorkflowSeedService>();
            await seedService.SeedAsync(cancellationToken);
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
