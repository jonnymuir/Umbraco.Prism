using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Sessions;

internal static class PrismSessionStoreExtensions
{
    /// <summary>
    /// Registers the ticket store and the storage behind it, chosen by <c>Prism:Session:Storage</c>.
    /// A host that registers its own <see cref="IPrismSessionStorage"/> after this one replaces it.
    /// </summary>
    internal static IServiceCollection AddPrismSessionStore(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IPrismSessionStorage>(provider =>
            provider.GetRequiredService<IOptions<PrismSessionOptions>>().Value.Storage == PrismSessionStorageKind.DistributedCache
                ? new PrismDistributedCacheSessionStorage(provider.GetRequiredService<IDistributedCache>())
                : ActivatorUtilities.CreateInstance<PrismDatabaseSessionStorage>(provider));
        services.AddSingleton<PrismTicketStore>();
        return services;
    }

    /// <summary>Starts the hourly deletion of expired sessions. It needs Umbraco's runtime state, so the composer adds it rather than <c>AddPrismMemberSignIn</c>.</summary>
    internal static IServiceCollection AddPrismSessionCleanup(this IServiceCollection services) =>
        services.AddHostedService<PrismSessionCleanup>();
}
