using Microsoft.Extensions.Hosting;

namespace UmbracoPrism.Core.Auth;

/// <summary>Refuses to start a non-Development host that has the backchannel override set.</summary>
internal sealed class PrismBackchannelGuard(IHostEnvironment environment) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        PrismBackchannel.ThrowIfMisconfigured(environment);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
