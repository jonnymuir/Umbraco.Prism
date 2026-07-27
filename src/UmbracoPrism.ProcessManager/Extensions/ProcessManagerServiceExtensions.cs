using Microsoft.Extensions.DependencyInjection;
using UmbracoPrism.ProcessManager.Abstractions;
using UmbracoPrism.ProcessManager.Services;
using UmbracoPrism.ProcessManager.Stores;

namespace UmbracoPrism.ProcessManager.Extensions;

public static class ProcessManagerServiceExtensions
{
    public static IServiceCollection AddPrismProcessManager(
        this IServiceCollection services,
        string blueprintSeedPath) =>
        services.AddPrismProcessManager<ProcessManagerEngine>(blueprintSeedPath);

    public static IServiceCollection AddPrismProcessManager<TEngine>(
        this IServiceCollection services,
        string blueprintSeedPath)
        where TEngine : class, IProcessManager
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(blueprintSeedPath);

        services.AddSingleton<IServiceBlueprintStore>(
            _ => new FilesystemServiceBlueprintStore(blueprintSeedPath));
        services.AddSingleton<TEngine>();
        services.AddSingleton<IProcessManager>(sp => sp.GetRequiredService<TEngine>());

        return services;
    }

    /// <summary>
    /// Registers <see cref="ServiceBlueprintAuthoringService"/>. The host must already have an
    /// <see cref="Abstractions.IServiceBlueprintSourceStore"/> registered — this does not provide one.
    /// </summary>
    public static IServiceCollection AddPrismServiceBlueprintAuthoring(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<ServiceBlueprintAuthoringService>();

        return services;
    }
}
