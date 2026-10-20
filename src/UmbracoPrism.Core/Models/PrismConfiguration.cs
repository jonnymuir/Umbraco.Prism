namespace UmbracoPrism.Core.Models;

/// <summary>
/// Core configuration options for Umbraco Prism.
/// Bind from appsettings.json under "Prism".
/// </summary>
public class PrismConfiguration
{
    /// <summary>
    /// Configuration section path used to bind Prism options.
    /// </summary>
    public const string SectionName = "Prism";

    /// <summary>
    /// Opt-in: seeds (and reconciles on every boot) a single tenant record from configuration.
    /// Only runs if <see cref="PrismSeedTenantOptions.Hostname"/> is set. See
    /// <see cref="PrismSeedTenantOptions"/> for the fields and
    /// <c>docs/umbraco-setup.md</c> for the promotion-through-environments story.
    /// </summary>
    public PrismSeedTenantOptions SeedTenant { get; set; } = new();
}

/// <summary>
/// A single tenant to create (and reconcile back to these values on every boot, the same
/// idempotent pattern used elsewhere for seeded content) from plain configuration — no backoffice
/// interaction, no uSync file needed. Bind under <c>Prism:SeedTenant</c>.
/// </summary>
public class PrismSeedTenantOptions
{
    /// <summary>Hostname this tenant matches. Seeding is skipped entirely while this is empty.</summary>
    public string? Hostname { get; set; }

    /// <summary>Display name for the tenant. Defaults to <see cref="Hostname"/> if not set.</summary>
    public string? Name { get; set; }

    public string? EntraTenantId { get; set; }
    public string? EntraClientId { get; set; }

    /// <summary>Key Vault secret name for an Entra tenant (used when <see cref="EntraTenantId"/> is set).</summary>
    public string? SecretKeyName { get; set; }

    /// <summary>Generic OIDC authority (e.g. Keycloak) — an alternative to the Entra fields above.</summary>
    public string? OidcAuthority { get; set; }
    public string? OidcClientId { get; set; }
    public string? OidcClientSecretProvider { get; set; }
    public string? OidcClientSecretReference { get; set; }

    public bool AllowBiometricLogin { get; set; } = true;

    /// <summary>
    /// The tenant's mobile apps, for app sign-in (the tenant editor's App Sign-In settings). Set
    /// values replace the tenant's saved ones on every boot; the tenant's other mobile settings are
    /// left as they are.
    /// </summary>
    public PrismSeedMobileAppOptions MobileApp { get; set; } = new();
}

/// <summary>
/// The app identities a tenant's site vouches for, bound under <c>Prism:SeedTenant:MobileApp</c>.
/// None of these is secret: the site publishes them in apple-app-site-association and
/// assetlinks.json.
/// </summary>
public class PrismSeedMobileAppOptions
{
    /// <summary>The app's bundle ID, which is also its Android package name unless that is set.</summary>
    public string? AppId { get; set; }

    public string? AppleTeamId { get; set; }

    public string? AndroidPackageName { get; set; }

    /// <summary>SHA-256 fingerprints of the Android signing certificates, such as Google Play's app signing key.</summary>
    public List<string> AndroidCertificateFingerprints { get; set; } = [];

    public bool HasAny =>
        !string.IsNullOrWhiteSpace(AppId)
        || !string.IsNullOrWhiteSpace(AppleTeamId)
        || !string.IsNullOrWhiteSpace(AndroidPackageName)
        || AndroidCertificateFingerprints.Count > 0;
}
