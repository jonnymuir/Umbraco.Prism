using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;

namespace UmbracoPrism.Core.Auth;

/// <summary>
/// Per-address limit on the endpoints that start or finish a sign-in: the login and registration
/// redirects, the OIDC callback, and the mobile handover. It uses the framework limiter, which
/// creates a partition per address and discards idle ones. Behind a reverse proxy the remote address
/// is the proxy's unless the host configures forwarded headers for it.
/// </summary>
public static class PrismAuthRateLimiting
{
    /// <summary>Requests per address per minute to the sign-in endpoints, unless configured otherwise.</summary>
    public const int DefaultPermitPerMinute = 60;

    /// <summary>Configuration key that overrides <see cref="DefaultPermitPerMinute"/>; 0 turns the limit off.</summary>
    public const string PermitConfigurationKey = "Prism:RateLimiting:SignInRequestsPerMinute";

    private static readonly string[] ExactPaths =
    [
        "/auth/login", "/auth/register", "/signin-oidc", "/signout-callback-oidc"
    ];

    private const string MobileHandoverPrefix = "/umbraco/prism/mobile/auth/";

    /// <summary>Whether <paramref name="path"/> is one of the limited sign-in endpoints.</summary>
    public static bool IsSignInPath(PathString path) =>
        path.HasValue
        && (ExactPaths.Any(exact => path.Equals(exact, StringComparison.OrdinalIgnoreCase))
            || path.StartsWithSegments(MobileHandoverPrefix.TrimEnd('/'), StringComparison.OrdinalIgnoreCase));

    internal static RateLimitPartition<string> Partition(HttpContext context, int permitPerMinute)
    {
        if (permitPerMinute <= 0 || !IsSignInPath(context.Request.Path))
        {
            return RateLimitPartition.GetNoLimiter("not-limited");
        }

        return RateLimitPartition.GetFixedWindowLimiter(
            $"prism-signin:{context.Connection.RemoteIpAddress}",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = permitPerMinute, Window = TimeSpan.FromMinutes(1) });
    }

    internal static void Configure(RateLimiterOptions options, int permitPerMinute)
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context => Partition(context, permitPerMinute));
    }
}
