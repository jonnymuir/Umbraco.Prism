using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Api.Common.Attributes;
using Umbraco.Cms.Api.Management.Controllers;
using Umbraco.Cms.Api.Management.Routing;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Web.Common.Authorization;
using Umbraco.Extensions;
using UmbracoPrism.Core.Controllers.Models;
using UmbracoPrism.Core.Persistence;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Controllers;

/// <summary>
/// Controller for managing tenants in the Prism package via the Umbraco Management API.
/// </summary>
[Authorize(Policy = AuthorizationPolicies.BackOfficeAccess)]
[Authorize(Policy = "PrismAdmins")]
[VersionedApiBackOfficeRoute("prism")]
[ApiExplorerSettings(GroupName = "Prism")]
[MapToApi("Prism")]
public class TenantManagementController(
    IUmbracoDatabaseFactory databaseFactory,
    ITenantService tenantService,
    IBrandingService brandingService,
    IMobileBundleService mobileBundleService,
    IPrismBrandingMetadataService brandingMetadataService,
    ITenantTokenResolver tokenResolver) : ManagementApiControllerBase
{
    /// <summary>
    /// Gets all registered tenants.
    /// </summary>
    [HttpGet("tenants")]
    public ActionResult<IEnumerable<PrismTenantResponse>> GetTenants()
    {
        using var db = databaseFactory.CreateDatabase();
        var tenants = db.Fetch<PrismTenantSchema>();
        return Ok(tenants.Select(ToTenantResponse));
    }

    /// <summary>
    /// Creates a new tenant.
    /// </summary>
    [HttpPost("tenants")]
    public IActionResult CreateTenant([FromBody] PrismTenantRequest tenant)
    {
        if (tenant == null) return BadRequest();

        using var db = databaseFactory.CreateDatabase();

        var genericSecret = ResolveGenericSecretFields(
            tenant.Hostname,
            tenant.OidcAuthority,
            tenant.OidcClientId,
            tenant.SecretKeyName,
            tenant.OidcClientSecretProvider,
            tenant.OidcClientSecretReference,
            existingProvider: null,
            existingReference: null,
            preserveExisting: false);

        if (!TryValidateGenericOidcSecretRequest(
                tenant.Hostname,
                tenant.OidcAuthority,
                tenant.OidcClientId,
                tenant.SecretKeyName,
                tenant.OidcClientSecretProvider,
                tenant.OidcClientSecretReference,
                tenant.ResetOidcClientSecret,
                genericSecret.Reference,
                out var createError))
        {
            return BadRequest(new { error = createError });
        }

        var schema = new PrismTenantSchema
        {
            Id = 0,
            Name = tenant.Name,
            Hostname = tenant.Hostname,
            EntraTenantId = tenant.EntraTenantId,
            EntraClientId = tenant.EntraClientId,
            SecretKeyName = string.IsNullOrWhiteSpace(tenant.OidcAuthority) ? NormalizeOptionalString(tenant.SecretKeyName) : null,
            OidcAuthority = NormalizeOptionalString(tenant.OidcAuthority),
            OidcClientId = NormalizeOptionalString(tenant.OidcClientId),
            OidcClientSecret = genericSecret.LegacyInlineSecret,
            OidcClientSecretProvider = genericSecret.Provider,
            OidcClientSecretReference = genericSecret.Reference,
            BrandingOverrides = SerializeBrandingOverrides(tenant.BrandingOverrides),
            MobileBrandingOverrides = SerializeBrandingOverrides(tenant.MobileBrandingOverrides),
            MobileAppConfig = SerializeMobileAppConfig(tenant.MobileAppConfig),
            AllowBiometricLogin = tenant.AllowBiometricLogin
        };

        db.Insert(schema);
        tenantService.InvalidateDomain(schema.Hostname, "tenant-create");

        return Ok(ToTenantResponse(schema));
    }

    /// <summary>
    /// Updates an existing tenant.
    /// </summary>
    [HttpPut("tenants/{id:int}")]
    public IActionResult UpdateTenant(int id, [FromBody] PrismTenantRequest updatedTenant)
    {
        if (updatedTenant == null) return BadRequest();

        using var db = databaseFactory.CreateDatabase();
        var existing = db.SingleOrDefaultById<PrismTenantSchema>(id);
        if (existing == null) return NotFound();

        var oldHostname = existing.Hostname;
        var existingProvider = NormalizeSecretProvider(existing);
        var existingReference = NormalizeSecretReference(existing);
        var genericSecret = ResolveGenericSecretFields(
            updatedTenant.Hostname,
            updatedTenant.OidcAuthority,
            updatedTenant.OidcClientId,
            updatedTenant.ResetOidcClientSecret ? null : updatedTenant.SecretKeyName,
            updatedTenant.ResetOidcClientSecret ? null : updatedTenant.OidcClientSecretProvider,
            updatedTenant.ResetOidcClientSecret ? null : updatedTenant.OidcClientSecretReference,
            existingProvider,
            existingReference,
            preserveExisting: !updatedTenant.ResetOidcClientSecret);

        if (!TryValidateGenericOidcSecretRequest(
                updatedTenant.Hostname,
                updatedTenant.OidcAuthority,
                updatedTenant.OidcClientId,
                updatedTenant.SecretKeyName,
                updatedTenant.OidcClientSecretProvider,
                updatedTenant.OidcClientSecretReference,
                updatedTenant.ResetOidcClientSecret,
                genericSecret.Reference,
                out var updateError))
        {
            return BadRequest(new { error = updateError });
        }

        existing.Name = updatedTenant.Name;
        existing.Hostname = updatedTenant.Hostname;
        existing.EntraTenantId = updatedTenant.EntraTenantId;
        existing.EntraClientId = updatedTenant.EntraClientId;
        existing.SecretKeyName = string.IsNullOrWhiteSpace(updatedTenant.OidcAuthority) ? NormalizeOptionalString(updatedTenant.SecretKeyName) : null;
        existing.OidcAuthority = NormalizeOptionalString(updatedTenant.OidcAuthority);
        existing.OidcClientId = NormalizeOptionalString(updatedTenant.OidcClientId);
        existing.OidcClientSecret = genericSecret.LegacyInlineSecret;
        existing.OidcClientSecretProvider = genericSecret.Provider;
        existing.OidcClientSecretReference = genericSecret.Reference;
        existing.BrandingOverrides = SerializeBrandingOverrides(updatedTenant.BrandingOverrides);
        existing.MobileBrandingOverrides = SerializeBrandingOverrides(updatedTenant.MobileBrandingOverrides);
        existing.MobileAppConfig = SerializeMobileAppConfig(updatedTenant.MobileAppConfig);
        existing.AllowBiometricLogin = updatedTenant.AllowBiometricLogin;

        db.Update(existing);
        tenantService.InvalidateDomains([oldHostname, updatedTenant.Hostname], "tenant-update");

        return Ok(ToTenantResponse(existing));
    }

    /// <summary>
    /// Gets branding tabs with overrides for a tenant.
    /// </summary>
    [HttpGet("tenants/{id:int}/branding-tabs")]
    public IActionResult GetBrandingTabs(int id)
    {
        using var db = databaseFactory.CreateDatabase();
        var tenant = db.SingleOrDefaultById<PrismTenantSchema>(id);
        if (tenant == null) return NotFound();

        var overrides = DeserializeBrandingOverrides(tenant.BrandingOverrides);
        var tabs = brandingService.GetBrandingTabsWithOverrides(overrides);

        return Ok(new PrismBrandingTabResponse
        {
            TenantId = id,
            Tabs = tabs.ToList()
        });
    }

    /// <summary>
    /// Generates and downloads a Capacitor mobile bundle for a tenant.
    /// </summary>
    [HttpPost("tenants/{id:int}/produce-mobile")]
    public async Task<IActionResult> ProduceMobileBundle(int id, [FromBody] PrismMobileBundleRequest? request, CancellationToken cancellationToken)
    {
        using var db = databaseFactory.CreateDatabase();
        var tenant = db.SingleOrDefaultById<PrismTenantSchema>(id);
        if (tenant == null) return NotFound();

        var payload = request ?? new PrismMobileBundleRequest();
        var savedConfig = DeserializeMobileAppConfig(tenant.MobileAppConfig);
        payload.AppName ??= savedConfig?.AppName;
        payload.AppId ??= savedConfig?.AppId;
        payload.Version ??= savedConfig?.Version;
        payload.StartUrl ??= savedConfig?.StartUrl;
        payload.UserAgentMarker ??= savedConfig?.UserAgentMarker;
        payload.IconUrl ??= savedConfig?.IconUrl;
        payload.SplashUrl ??= savedConfig?.SplashUrl;
        payload.ErrorBackgroundColor ??= savedConfig?.ErrorBackgroundColor;
        payload.ErrorTextColor ??= savedConfig?.ErrorTextColor;
        payload.ErrorTitle ??= savedConfig?.ErrorTitle;
        payload.ErrorMessage ??= savedConfig?.ErrorMessage;
        payload.ShowErrorDiagnostics ??= savedConfig?.ShowErrorDiagnostics;

        try
        {
            var bundle = await mobileBundleService.BuildBundleAsync(tenant, payload, cancellationToken);
            var fileName = $"prism-mobile-{SanitizeFileSegment(tenant.Name)}.zip";
            return File(bundle, "application/zip", fileName);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Deletes a tenant by its database ID.
    /// </summary>
    [HttpDelete("tenants/{id:int}")]
    public IActionResult DeleteTenant(int id)
    {
        using var db = databaseFactory.CreateDatabase();

        var tenant = db.SingleOrDefaultById<PrismTenantSchema>(id);
        if (tenant == null) return NotFound();

        db.Delete<PrismTenantSchema>(id);
        tenantService.InvalidateDomain(tenant.Hostname, "tenant-delete");

        return Ok();
    }

    /// <summary>
    /// Returns the token resolution status for a tenant's identity fields.
    /// Used by the "Environment Tokens" backoffice tab to show editors what
    /// {{TOKEN_NAME}} placeholders exist and whether they are configured.
    /// </summary>
    [HttpGet("tenants/{id:int}/token-status")]
    public IActionResult GetTenantTokenStatus(int id)
    {
        using var db = databaseFactory.CreateDatabase();
        var tenant = db.SingleOrDefaultById<PrismTenantSchema>(id);
        if (tenant is null) return NotFound();

        var tokens = tokenResolver.ExtractTokenStatus(tenant);
        return Ok(tokens.Select(t => new
        {
            fieldName = t.FieldName,
            rawValue = t.RawValue,
            tokenName = t.TokenName,
            resolvedValue = t.ResolvedValue,
            isResolved = t.IsResolved
        }));
    }

    /// <summary>
    /// Gets branding variable metadata from CSS files for dynamic form generation.
    /// </summary>
    [HttpGet("branding/metadata")]
    [Authorize(Policy = AuthorizationPolicies.BackOfficeAccess)]
    public IActionResult GetBrandingMetadata()
    {
        var sections = brandingMetadataService.GetBrandingMetadata();
        return Ok(new { sections });
    }

    private static string? SerializeBrandingOverrides(Dictionary<string, string>? overrides)
    {
        if (overrides == null || overrides.Count == 0) return null;
        return JsonSerializer.Serialize(overrides);
    }

    private static Dictionary<string, string> DeserializeBrandingOverrides(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new Dictionary<string, string>();
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
        }
        catch
        {
            return new Dictionary<string, string>();
        }
    }

    private static string? SerializeMobileAppConfig(PrismMobileAppConfig? config)
    {
        if (config == null) return null;

        var hasAnyValue = !string.IsNullOrWhiteSpace(config.AppName)
            || !string.IsNullOrWhiteSpace(config.AppId)
            || !string.IsNullOrWhiteSpace(config.Version)
            || !string.IsNullOrWhiteSpace(config.StartUrl)
            || !string.IsNullOrWhiteSpace(config.UserAgentMarker)
            || !string.IsNullOrWhiteSpace(config.IconUrl)
            || !string.IsNullOrWhiteSpace(config.SplashUrl)
            || !string.IsNullOrWhiteSpace(config.ErrorBackgroundColor)
            || !string.IsNullOrWhiteSpace(config.ErrorTextColor)
            || !string.IsNullOrWhiteSpace(config.ErrorTitle)
            || !string.IsNullOrWhiteSpace(config.ErrorMessage)
            || config.ShowErrorDiagnostics.HasValue;

        if (!hasAnyValue) return null;
        return JsonSerializer.Serialize(config);
    }

    private static PrismMobileAppConfig? DeserializeMobileAppConfig(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            return JsonSerializer.Deserialize<PrismMobileAppConfig>(json);
        }
        catch
        {
            return null;
        }
    }

    private static string SanitizeFileSegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "tenant";

        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(value
            .Trim()
            .ToLowerInvariant()
            .Select(ch => invalidChars.Contains(ch) ? '-' : ch)
            .ToArray());

        return string.IsNullOrWhiteSpace(sanitized) ? "tenant" : sanitized;
    }

    private static PrismTenantResponse ToTenantResponse(PrismTenantSchema tenant)
    {
        var provider = NormalizeSecretProvider(tenant);
        var reference = NormalizeSecretReference(tenant);

        return new PrismTenantResponse
        {
            Id = tenant.Id,
            Name = tenant.Name,
            Hostname = tenant.Hostname,
            EntraTenantId = tenant.EntraTenantId,
            EntraClientId = tenant.EntraClientId,
            SecretKeyName = string.IsNullOrWhiteSpace(tenant.OidcAuthority)
                ? tenant.SecretKeyName
                : null,
            OidcAuthority = tenant.OidcAuthority,
            OidcClientId = tenant.OidcClientId,
            OidcClientSecretProvider = provider,
            HasOidcClientSecret = !string.IsNullOrWhiteSpace(reference),
            BrandingOverrides = tenant.BrandingOverrides,
            MobileBrandingOverrides = tenant.MobileBrandingOverrides,
            MobileAppConfig = tenant.MobileAppConfig,
            AllowBiometricLogin = tenant.AllowBiometricLogin
        };
    }

    private static (string? Provider, string? Reference, string? LegacyInlineSecret) ResolveGenericSecretFields(
        string? hostname,
        string? oidcAuthority,
        string? oidcClientId,
        string? requestedKeyVaultReference,
        string? requestedProvider,
        string? requestedReference,
        string? existingProvider,
        string? existingReference,
        bool preserveExisting)
    {
        if (string.IsNullOrWhiteSpace(oidcAuthority))
        {
            return (null, null, null);
        }

        var normalizedKeyVaultReference = NormalizeOptionalString(requestedKeyVaultReference);
        var normalizedProvider = NormalizeOptionalString(requestedProvider);
        var normalizedReference = NormalizeOptionalString(requestedReference);
        if (!string.IsNullOrWhiteSpace(normalizedProvider) && !string.IsNullOrWhiteSpace(normalizedReference))
        {
            if (string.Equals(normalizedProvider, PrismSecretProviderNames.Inline, StringComparison.OrdinalIgnoreCase)
                && IsRepoOwnedLocalDemoTenant(hostname, oidcAuthority, oidcClientId))
            {
                return (PrismSecretProviderNames.Inline, normalizedReference, normalizedReference);
            }

            if (string.Equals(normalizedProvider, PrismSecretProviderNames.AzureKeyVault, StringComparison.OrdinalIgnoreCase))
            {
                return (PrismSecretProviderNames.AzureKeyVault, normalizedReference, null);
            }
        }

        if (!string.IsNullOrWhiteSpace(normalizedKeyVaultReference))
        {
            return (PrismSecretProviderNames.AzureKeyVault, normalizedKeyVaultReference, null);
        }

        if (preserveExisting)
        {
            var provider = NormalizeOptionalString(existingProvider);
            var reference = NormalizeOptionalString(existingReference);
            return (provider, reference, string.Equals(provider, PrismSecretProviderNames.Inline, StringComparison.OrdinalIgnoreCase) ? reference : null);
        }

        return (null, null, null);
    }

    private static bool TryValidateGenericOidcSecretRequest(
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

        var keyVaultReference = NormalizeOptionalString(requestedKeyVaultReference);
        var provider = NormalizeOptionalString(requestedProvider);
        var reference = NormalizeOptionalString(requestedReference);
        var hasRequestedSecretChange = provider != null || reference != null;
        var hasRequestedKeyVaultReference = keyVaultReference != null;

        if (resetOidcClientSecret && (hasRequestedSecretChange || hasRequestedKeyVaultReference))
        {
            error = "OIDC client secret reset cannot be combined with a replacement secret reference.";
            return false;
        }

        if (hasRequestedSecretChange && (provider == null || reference == null))
        {
            error = "OIDC client secret updates require both provider and reference.";
            return false;
        }

        if (hasRequestedKeyVaultReference && string.Equals(provider, PrismSecretProviderNames.Inline, StringComparison.OrdinalIgnoreCase))
        {
            error = "Inline OIDC client secrets cannot be combined with an Azure Key Vault secret reference.";
            return false;
        }

        if (hasRequestedKeyVaultReference
            && string.Equals(provider, PrismSecretProviderNames.AzureKeyVault, StringComparison.OrdinalIgnoreCase)
            && reference != null
            && !string.Equals(reference, keyVaultReference, StringComparison.Ordinal))
        {
            error = "OIDC client secret reference payload is inconsistent.";
            return false;
        }

        if (provider != null)
        {
            if (string.Equals(provider, PrismSecretProviderNames.Inline, StringComparison.OrdinalIgnoreCase)
                && !IsRepoOwnedLocalDemoTenant(hostname, oidcAuthority, oidcClientId))
            {
                error = "Inline OIDC client secrets are reserved for the repo-owned localhost demo seed.";
                return false;
            }

            if (!string.Equals(provider, PrismSecretProviderNames.AzureKeyVault, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(provider, PrismSecretProviderNames.Inline, StringComparison.OrdinalIgnoreCase))
            {
                error = "Unsupported OIDC client secret provider.";
                return false;
            }
        }

        if (!resetOidcClientSecret && string.IsNullOrWhiteSpace(resolvedReference))
        {
            error = "Generic OIDC confidential clients require an OIDC client secret provider and reference.";
            return false;
        }

        return true;
    }

    private static string? NormalizeSecretProvider(PrismTenantSchema tenant)
    {
        if (!string.IsNullOrWhiteSpace(tenant.OidcClientSecretProvider))
        {
            return tenant.OidcClientSecretProvider;
        }

        return string.IsNullOrWhiteSpace(tenant.OidcClientSecret) ? null : PrismSecretProviderNames.Inline;
    }

    private static string? NormalizeSecretReference(PrismTenantSchema tenant)
    {
        return NormalizeOptionalString(tenant.OidcClientSecretReference)
            ?? NormalizeOptionalString(tenant.OidcClientSecret);
    }

    private static bool IsRepoOwnedLocalDemoTenant(string? hostname, string? oidcAuthority, string? oidcClientId)
    {
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

    private static string? NormalizeOptionalString(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
