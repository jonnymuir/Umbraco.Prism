using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UmbracoPrism.Core.Auth;

namespace UmbracoPrism.Core.Extensions;

/// <summary>Registers the startup checks and request limits that guard Prism's security settings. <see cref="PrismComposer"/> calls these.</summary>
public static class PrismGuardExtensions
{
    /// <summary>Refuses to start on an unsafe backchannel setting, and warns about the key ring.</summary>
    public static IServiceCollection AddPrismStartupGuards(this IServiceCollection services)
    {
        services.AddHostedService<PrismBackchannelGuard>();
        services.AddHostedService<PrismDataProtectionGuard>();
        return services;
    }

    /// <summary>Limits requests to the sign-in endpoints per address.</summary>
    public static IServiceCollection AddPrismAuthRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var permit = configuration.GetValue(PrismAuthRateLimiting.PermitConfigurationKey, PrismAuthRateLimiting.DefaultPermitPerMinute);
        services.AddRateLimiter(options => PrismAuthRateLimiting.Configure(options, permit));
        return services;
    }
}
