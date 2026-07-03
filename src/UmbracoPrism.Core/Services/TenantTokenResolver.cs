using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using UmbracoPrism.Core.Persistence;

namespace UmbracoPrism.Core.Services;

/// <summary>
/// Resolves {{TOKEN_NAME}} placeholders in tenant field values using IConfiguration.
/// Tokens must be ALL_CAPS_WITH_UNDERSCORES; unresolved tokens are left as-is.
/// </summary>
public sealed class TenantTokenResolver : ITenantTokenResolver
{
    // Matches {{TOKEN_NAME}} where the name is uppercase letters, digits, and underscores.
    private static readonly Regex TokenPattern =
        new(@"\{\{([A-Z][A-Z0-9_]*)\}\}", RegexOptions.Compiled);

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
            _configuration[match.Groups[1].Value] ?? match.Value);
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
                var configValue = _configuration[tokenName];
                results.Add(new TenantTokenResult(field, value, tokenName, configValue, configValue is not null));
            }
        }

        return results;
    }
}
