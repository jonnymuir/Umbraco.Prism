using UmbracoPrism.Core.Controllers.Models;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Controllers;

/// <summary>The checks a tenant create or update request must pass before it is saved.</summary>
internal static class TenantRequestRules
{
    /// <summary>
    /// Validates the authority and Entra tenant id. A value holding a {{TOKEN}} is checked at runtime,
    /// once the token has resolved, by <see cref="TenantService"/>.
    /// </summary>
    internal static string? ValidateIdentity(PrismTenantRequest tenant)
    {
        static string? Concrete(string? value) =>
            value is not null && value.Contains("{{", StringComparison.Ordinal) ? null : value;

        return PrismTenantValidator.Validate(Concrete(tenant.OidcAuthority), Concrete(tenant.EntraTenantId));
    }

    internal static bool TryValidateGenericOidcSecretRequest(
        string? hostname,
        string? oidcAuthority,
        string? oidcClientId,
        string? requestedKeyVaultReference,
        string? requestedProvider,
        string? requestedReference,
        bool resetOidcClientSecret,
        string? resolvedReference,
        out string? error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(oidcAuthority))
        {
            return true;
        }

        var keyVaultReference = Normalize(requestedKeyVaultReference);
        var provider = Normalize(requestedProvider);
        var reference = Normalize(requestedReference);
        error = ChangeError(resetOidcClientSecret, provider, reference, keyVaultReference);
        if (error is not null)
        {
            return false;
        }

        error = ProviderError(provider, IsRepoOwnedLocalDemoTenant(hostname, oidcAuthority, oidcClientId));
        if (error is not null)
        {
            return false;
        }

        if (!resetOidcClientSecret && string.IsNullOrWhiteSpace(resolvedReference))
        {
            error = "Generic OIDC confidential clients require an OIDC client secret provider and reference.";
            return false;
        }

        return true;
    }

    private static string? ChangeError(bool reset, string? provider, string? reference, string? keyVaultReference)
    {
        var hasSecretChange = provider != null || reference != null;
        var hasKeyVaultReference = keyVaultReference != null;
        var isInline = string.Equals(provider, PrismSecretProviderNames.Inline, StringComparison.OrdinalIgnoreCase);

        if (reset && (hasSecretChange || hasKeyVaultReference))
        {
            return "OIDC client secret reset cannot be combined with a replacement secret reference.";
        }

        if (hasSecretChange && (provider == null || reference == null))
        {
            return "OIDC client secret updates require both provider and reference.";
        }

        if (hasKeyVaultReference && isInline)
        {
            return "Inline OIDC client secrets cannot be combined with an Azure Key Vault secret reference.";
        }

        return hasKeyVaultReference
            && string.Equals(provider, PrismSecretProviderNames.AzureKeyVault, StringComparison.OrdinalIgnoreCase)
            && reference != null
            && !string.Equals(reference, keyVaultReference, StringComparison.Ordinal)
                ? "OIDC client secret reference payload is inconsistent."
                : null;
    }

    private static string? ProviderError(string? provider, bool isLocalDemoTenant)
    {
        if (provider == null)
        {
            return null;
        }

        var isInline = string.Equals(provider, PrismSecretProviderNames.Inline, StringComparison.OrdinalIgnoreCase);
        if (isInline && !isLocalDemoTenant)
        {
            return "Inline OIDC client secrets are reserved for the repo-owned localhost demo seed.";
        }

        return isInline || string.Equals(provider, PrismSecretProviderNames.AzureKeyVault, StringComparison.OrdinalIgnoreCase)
            ? null
            : "Unsupported OIDC client secret provider.";
    }

    internal static bool IsRepoOwnedLocalDemoTenant(string? hostname, string? oidcAuthority, string? oidcClientId)
    {
        if (!PrismDemoTenants.IsAllowed)
        {
            return false;
        }

        if (!string.Equals(hostname?.Trim(), "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.Equals(oidcClientId?.Trim(), "prism-client", StringComparison.Ordinal))
        {
            return false;
        }

        if (!Uri.TryCreate(oidcAuthority, UriKind.Absolute, out var authority))
        {
            return false;
        }

        return string.Equals(authority.Host, "localhost", StringComparison.OrdinalIgnoreCase);
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
