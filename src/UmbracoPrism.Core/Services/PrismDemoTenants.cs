namespace UmbracoPrism.Core.Services;

/// <summary>
/// Whether the repo-owned demo behaviours are allowed: the localhost and Codespaces demo tenants,
/// their inline client secrets, and loopback identity providers. They exist for local development,
/// so they are on in Development and off everywhere else unless the host opts in explicitly with the
/// <c>Prism__AllowDemoTenants</c> environment variable.
/// </summary>
public static class PrismDemoTenants
{
    /// <summary>The environment variable a non-Development host sets to allow demo tenants.</summary>
    public const string OptInVariable = "Prism__AllowDemoTenants";

    /// <summary>True when demo behaviours are allowed in the current process.</summary>
    public static bool IsAllowed => IsAllowedFor(
        Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
        Environment.GetEnvironmentVariable(OptInVariable));

    /// <summary>The decision for a given environment name and opt-in value.</summary>
    public static bool IsAllowedFor(string? environmentName, string? optIn) =>
        string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase)
        || string.Equals(optIn?.Trim(), "true", StringComparison.OrdinalIgnoreCase);
}
