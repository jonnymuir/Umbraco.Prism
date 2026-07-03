using UmbracoPrism.Core.Persistence;

namespace UmbracoPrism.Core.Services;

/// <summary>
/// Resolves {{TOKEN_NAME}} placeholders in tenant field values using IConfiguration
/// (appsettings.json, environment variables, Key Vault, etc.).
/// </summary>
public interface ITenantTokenResolver
{
    /// <summary>
    /// Replaces every {{TOKEN_NAME}} occurrence in <paramref name="value"/> with the
    /// corresponding value from IConfiguration, leaving unmatched tokens intact.
    /// Returns the original string unchanged when no tokens are present.
    /// </summary>
    string Resolve(string? value);

    /// <summary>
    /// Scans all tokenizable fields on <paramref name="tenant"/> and returns one result
    /// per distinct token found, showing both the raw placeholder and its resolved value.
    /// </summary>
    IReadOnlyList<TenantTokenResult> ExtractTokenStatus(PrismTenantSchema tenant);
}

/// <summary>
/// Describes a single {{TOKEN_NAME}} found in a tenant field and its resolution outcome.
/// </summary>
/// <param name="FieldName">The property name on the tenant schema (e.g. "OidcAuthority").</param>
/// <param name="RawValue">The full field value as stored, including the token placeholder.</param>
/// <param name="TokenName">The bare token name, without the {{ }} delimiters.</param>
/// <param name="ResolvedValue">The value from IConfiguration, or <see langword="null"/> when unresolved.</param>
/// <param name="IsResolved">True when a configuration value was found for this token.</param>
public record TenantTokenResult(
    string FieldName,
    string RawValue,
    string TokenName,
    string? ResolvedValue,
    bool IsResolved);
