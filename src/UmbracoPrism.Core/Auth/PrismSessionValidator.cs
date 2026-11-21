using System.Globalization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UmbracoPrism.Core.Models;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Auth;

/// <summary>
/// Bounds a member session beyond the cookie's own sliding window: an absolute maximum lifetime,
/// and a periodic re-check with the identity provider so a member removed there loses the session.
/// Both markers live in the cookie ticket, so every way of signing in is covered the same way.
/// </summary>
internal static partial class PrismSessionValidator
{
    internal const string SessionStartedKey = "prism_session_started";
    internal const string RevalidatedKey = "prism_session_revalidated";

    /// <summary>When the provider could not be asked, try again after this long rather than on every request.</summary>
    private static readonly TimeSpan RetryAfterUnreachable = TimeSpan.FromMinutes(5);

    /// <summary>
    /// The member cookie's validation: tenant binding first (the principal must belong to the
    /// tenant this hostname resolves to), then the session's own lifetime and revalidation.
    /// </summary>
    internal static async Task OnValidatePrincipalAsync(CookieValidatePrincipalContext context)
    {
        var validator = context.HttpContext.RequestServices.GetRequiredService<IPrismTenantBindingValidator>();
        var prismContext = context.HttpContext.RequestServices.GetRequiredService<IPrismContext>();
        var tenant = prismContext.CurrentTenant;

        if (tenant is null ||
            !tenant.HasSignInConfigured() ||
            context.Principal is null ||
            !validator.IsBound(context.Principal, tenant))
        {
            context.RejectPrincipal();
            return;
        }

        await ValidateAsync(context);
    }

    internal static async Task ValidateAsync(CookieValidatePrincipalContext context, TimeProvider? clock = null)
    {
        var services = context.HttpContext.RequestServices;
        var options = services.GetRequiredService<IOptions<PrismSessionOptions>>().Value;
        var now = (clock ?? TimeProvider.System).GetUtcNow();
        var properties = context.Properties;

        var started = ReadTime(properties, SessionStartedKey);
        if (started is null)
        {
            // A session from before these markers existed: it starts counting now.
            started = now;
            properties.Items[SessionStartedKey] = Format(now);
            properties.Items[RevalidatedKey] = Format(now);
            context.ShouldRenew = true;
        }

        if (now - started.Value > TimeSpan.FromMinutes(options.AbsoluteMaxLifetimeMinutes))
        {
            context.RejectPrincipal();
            return;
        }

        if (options.RevalidationIntervalMinutes <= 0 || context.Principal is null)
        {
            return;
        }

        var lastChecked = ReadTime(properties, RevalidatedKey) ?? started.Value;
        if (now - lastChecked < TimeSpan.FromMinutes(options.RevalidationIntervalMinutes))
        {
            return;
        }

        var prismContext = services.GetRequiredService<IPrismContext>();
        var outcome = await prismContext.RevalidateSessionAsync(context.HttpContext, properties, context.Principal);
        switch (outcome)
        {
            case PrismSessionCheck.Revoked:
                var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("UmbracoPrism.Core.Auth.PrismSessionValidator");
                LogSessionRevoked(logger);
                context.RejectPrincipal();
                break;
            case PrismSessionCheck.Active or PrismSessionCheck.Skipped:
                properties.Items[RevalidatedKey] = Format(now);
                context.ShouldRenew = true;
                break;
            default:
                properties.Items[RevalidatedKey] = Format(now - TimeSpan.FromMinutes(options.RevalidationIntervalMinutes) + RetryAfterUnreachable);
                context.ShouldRenew = true;
                break;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "The identity provider no longer accepts a member session; signing the member out.")]
    private static partial void LogSessionRevoked(ILogger logger);

    private static DateTimeOffset? ReadTime(AuthenticationProperties properties, string key) =>
        properties.Items.TryGetValue(key, out var value)
        && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed
            : null;

    private static string Format(DateTimeOffset value) => value.ToString("o", CultureInfo.InvariantCulture);
}
