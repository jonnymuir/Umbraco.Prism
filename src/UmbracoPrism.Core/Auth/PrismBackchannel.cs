using Microsoft.Extensions.Hosting;

namespace UmbracoPrism.Core.Auth;

/// <summary>
/// The one place that reads <c>KEYCLOAK_BACKCHANNEL_URL</c>. The variable swaps the address Prism
/// uses for server-side identity provider calls (token, refresh, JWKS), which is only acceptable
/// in Development. Every call site asks this class, so none can honour it in another environment.
/// </summary>
public static class PrismBackchannel
{
    public const string VariableName = "KEYCLOAK_BACKCHANNEL_URL";

    /// <summary>The override base URL, or null outside Development or when it is not set.</summary>
    public static string? GetBase()
    {
        var value = Environment.GetEnvironmentVariable(VariableName);
        if (string.IsNullOrWhiteSpace(value) || !IsDevelopment())
        {
            return null;
        }

        return value.TrimEnd('/');
    }

    /// <summary>
    /// Moves <paramref name="publicEndpoint"/> onto the backchannel host, keeping its path and
    /// query. Returns the endpoint unchanged outside Development or when no override is set.
    /// </summary>
    public static string Rewrite(string publicEndpoint)
    {
        var backchannel = GetBase();
        if (backchannel is null
            || !Uri.TryCreate(backchannel, UriKind.Absolute, out var backchannelUri)
            || !Uri.TryCreate(publicEndpoint, UriKind.Absolute, out var endpointUri))
        {
            return publicEndpoint;
        }

        var builder = new UriBuilder(endpointUri)
        {
            Scheme = backchannelUri.Scheme,
            Host = backchannelUri.Host,
            Port = backchannelUri.IsDefaultPort ? -1 : backchannelUri.Port
        };
        return builder.Uri.AbsoluteUri;
    }

    /// <summary>Throws when the variable is set in an environment other than Development.</summary>
    public static void ThrowIfMisconfigured(IHostEnvironment environment)
    {
        if (!environment.IsDevelopment()
            && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(VariableName)))
        {
            throw new InvalidOperationException(
                $"{VariableName} must not be set in non-Development environments.");
        }
    }

    private static bool IsDevelopment() =>
        string.Equals(
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
            Environments.Development,
            StringComparison.OrdinalIgnoreCase);
}
