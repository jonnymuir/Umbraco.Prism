using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Workspaces;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Services;

namespace UmbracoPrism.TestSite.FieldRecording;

/// <summary>
/// Seeds the AI setup (<see cref="FieldRecordingAiSetup"/>) and then the "Butterfly identification"
/// automation, on every boot, so the support system declared in <c>appsettings.json</c> has a real
/// automation waiting. Runs as a background service because Automate and Umbraco.AI stand up their
/// own schema after the runtime reaches Run, so it polls until both are ready. Never throws: a demo
/// setup failing must not stop the site.
/// </summary>
public sealed class ButterflyIdentificationSeeder(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    IRuntimeState runtimeState,
    ILogger<ButterflyIdentificationSeeder> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            for (var attempt = 0; attempt < 60 && !stoppingToken.IsCancellationRequested; attempt++)
            {
                if (runtimeState.Level == RuntimeLevel.Run && await TrySeedAsync(stoppingToken))
                {
                    return;
                }

                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }

            logger.LogWarning("BUTTERFLY IDENTIFICATION SEEDER: gave up seeding.");
        }
        catch (OperationCanceledException)
        {
            // Host shutting down.
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "BUTTERFLY IDENTIFICATION SEEDER: could not seed the automation.");
        }
    }

    private async Task<bool> TrySeedAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var services = scope.ServiceProvider;

        var workspace = await AutomateWorkspaceEnsurer.EnsureAsync(
            services.GetRequiredService<IWorkspaceService>(),
            services.GetRequiredService<IUserService>(),
            configuration,
            ct);
        if (workspace is null)
        {
            return false;
        }

        Guid agentId;
        try
        {
            agentId = await services.GetRequiredService<FieldRecordingAiSetup>().EnsureAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Umbraco.AI's own schema is not ready yet: try again next tick.
            logger.LogDebug(ex, "BUTTERFLY IDENTIFICATION SEEDER: the AI setup is not ready yet.");
            return false;
        }

        var automation = ButterflyIdentificationAutomation.Build(workspace.Id, agentId, SigningKey());
        var automationService = services.GetRequiredService<IAutomationService>();
        var id = ButterflyIdentificationAutomation.AutomationId;

        var existing = await automationService.GetAutomationAsync(id, ct);
        if (existing is null)
        {
            await automationService.CreateAutomationAsync(automation, cancellationToken: ct);
            logger.LogInformation("BUTTERFLY IDENTIFICATION SEEDER: created automation {Id}.", id);
        }
        else
        {
            existing.Name = automation.Name;
            existing.Description = automation.Description;
            existing.WorkspaceId = automation.WorkspaceId;
            existing.Trigger = automation.Trigger;
            existing.Steps = automation.Steps;
            existing.Connections = automation.Connections;
            await automationService.UpdateAutomationAsync(existing, cancellationToken: ct);
            logger.LogInformation("BUTTERFLY IDENTIFICATION SEEDER: refreshed automation {Id}.", id);
        }

        await automationService.PublishAutomationAsync(id, cancellationToken: ct);
        logger.LogInformation("BUTTERFLY IDENTIFICATION SEEDER: published automation {Id}.", id);
        return true;
    }

    /// <summary>
    /// Mirrors whatever the config-driven webhook client will actually sign with, so the seeded
    /// trigger's authenticator and the client's outbound signature always agree. The support system
    /// is found by key, not by its position in the array.
    /// </summary>
    private string? SigningKey()
    {
        var entry = configuration.GetSection("Wayfinder:SupportSystems").GetChildren()
            .FirstOrDefault(s => s["key"] == ButterflyIdentificationAutomation.SupportSystemKey);
        var auth = entry?.GetSection("endpoint:auth");
        return string.Equals(auth?["type"], "hmac-sha256", StringComparison.OrdinalIgnoreCase)
            ? configuration[auth?["secretRef"] ?? "BUTTERFLY_IDENTIFICATION_SIGNING_KEY"]
            : null;
    }
}
