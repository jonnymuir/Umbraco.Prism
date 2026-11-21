using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using UmbracoPrism.Core.Persistence;

namespace UmbracoPrism.Core.Services;

/// <summary>
/// Resolves {{TOKEN_NAME}} placeholders in tenant field values using IConfiguration.
/// Tokens must be ALL_CAPS_WITH_UNDERSCORES; unresolved tokens are left as-is.
/// A token whose name looks like a secret or a framework setting never resolves, so a tenant
/// field cannot be used to read or send out such a value. Setting <c>Prism:TenantTokens:AllowedPrefixes</c>
/// narrows resolution further to tokens starting with one of the listed prefixes.
/// </summary>
public sealed class TenantTokenResolver : ITenantTokenResolver
{
    // Matches {{TOKEN_NAME}} where the name is uppercase letters, digits, and underscores.
    private static readonly Regex TokenPattern =
        new(@"\{\{([A-Z][A-Z0-9_]*)\}\}", RegexOptions.Compiled);

    // Names that carry credentials or framework configuration. These never resolve.
    private static readonly string[] BlockedFragments =
    [
        "SECRET", "PASSWORD", "PASSWD", "PWD", "KEY", "TOKEN", "CREDENTIAL", "CONNECTIONSTRING",
        "CONNECTION_STRING", "SAS", "SIGNATURE", "CERT", "PRIVATE"
    ];

    private static readonly string[] BlockedPrefixes = ["ASPNETCORE_", "DOTNET_", "UMBRACO_", "CONNECTIONSTRINGS_", "AZURE_", "ARM_"];

    private readonly IConfiguration _configuration;

    public TenantTokenResolver(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <inheritdoc />
    public string Resolve(string? value)
    {
        if (string.IsNullOrEmpty(value)) return value ?? string.Empty;

        return TokenPattern.Replace(value, match =>
            IsBlocked(match.Groups[1].Value) ? match.Value : _configuration[match.Groups[1].Value] ?? match.Value);
    }

    /// <summary>Whether a token name is refused: it looks sensitive, or is outside the configured prefixes.</summary>
    internal bool IsBlocked(string tokenName)
    {
        if (BlockedPrefixes.Any(prefix => tokenName.StartsWith(prefix, StringComparison.Ordinal))
            || BlockedFragments.Any(fragment => tokenName.Contains(fragment, StringComparison.Ordinal)))
        {
            return true;
        }

        var allowed = _configuration.GetSection("Prism:TenantTokens:AllowedPrefixes").Get<string[]>();
        return allowed is { Length: > 0 }
            && !allowed.Any(prefix => !string.IsNullOrWhiteSpace(prefix) && tokenName.StartsWith(prefix, StringComparison.Ordinal));
    }

    /// <inheritdoc />
    public IReadOnlyList<TenantTokenResult> ExtractTokenStatus(PrismTenantSchema tenant)
    {
        // Hostname is intentionally excluded — it is the DB lookup key and must be
        // stored as a resolved value (tokens are resolved at uSync import time, not runtime).
        (string Field, string? Value)[] fields =
        [
            ("EntraTenantId",           tenant.EntraTenantId),
            ("EntraClientId",           tenant.EntraClientId),
            ("SecretKeyName",           tenant.SecretKeyName),
            ("OidcAuthority",           tenant.OidcAuthority),
            ("OidcClientId",            tenant.OidcClientId),
            ("OidcClientSecretProvider",tenant.OidcClientSecretProvider),
            ("OidcClientSecretReference",tenant.OidcClientSecretReference),
        ];

        var results = new List<TenantTokenResult>();

        foreach (var (field, value) in fields)
        {
            if (string.IsNullOrWhiteSpace(value)) continue;

            foreach (Match match in TokenPattern.Matches(value))
            {
                var tokenName = match.Groups[1].Value;
                var blocked = IsBlocked(tokenName);
                var configValue = blocked ? null : _configuration[tokenName];
                results.Add(new TenantTokenResult(field, value, tokenName, configValue, configValue is not null, blocked));
            }
        }

        return results;
    }
}
