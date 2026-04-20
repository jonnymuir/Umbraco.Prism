using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using UmbracoPrism.Core.Models;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Extensions;

public static class PrismAuthExtensions
{
    public static IServiceCollection AddPrismAuthentication(this IServiceCollection services, IConfiguration config)
    {
        services.AddHttpClient("prism-oidc-metadata");
        services.TryAddSingleton<IPrismSigningKeyCache, PrismSigningKeyCache>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IPrismSigningKeyCache>((options, signingKeyCache) =>
            {
                options.Events = new JwtBearerEvents
                {
                    OnAuthenticationFailed = context =>
                    {
                        Console.WriteLine($"PRISM AUTH FAILED: {context.Exception.Message}");
                        return Task.CompletedTask;
                    }
                };

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    
                    // Allow some clock drift (default is 5 mins, but let's be explicit)
                    ClockSkew = TimeSpan.FromMinutes(5),

                    IssuerValidator = (issuer, securityToken, validationParameters) =>
                    {
                        var tenants = config.GetSection("PrismBusinessApp:Tenants").Get<List<BackOfficeTenant>>();
                        if (tenants == null || tenants.Count == 0)
                            throw new SecurityTokenInvalidIssuerException("No trusted tenants configured");

                        var tokenTenantId = GetTokenTenantId(securityToken);

                        if (!string.IsNullOrWhiteSpace(tokenTenantId))
                        {
                            // Entra CIAM path
                            var configuredTenantIds = tenants
                                .Select(t => t.EntraTenantId)
                                .Where(tid => !string.IsNullOrWhiteSpace(tid))
                                .ToHashSet(StringComparer.OrdinalIgnoreCase);

                            if (!configuredTenantIds.Contains(tokenTenantId))
                                throw new SecurityTokenInvalidIssuerException("Token tenant is not trusted");

                            if (!Uri.TryCreate(issuer, UriKind.Absolute, out var issuerUri))
                                throw new SecurityTokenInvalidIssuerException("Issuer is not a valid absolute URI");

                            var expectedHost = $"{tokenTenantId}.ciamlogin.com";
                            if (!string.Equals(issuerUri.Host, expectedHost, StringComparison.OrdinalIgnoreCase))
                                throw new SecurityTokenInvalidIssuerException("Issuer host does not match token tenant");

                            var expectedPathPrefix = $"/{tokenTenantId}/v2.0";
                            if (!issuerUri.AbsolutePath.StartsWith(expectedPathPrefix, StringComparison.OrdinalIgnoreCase))
                                throw new SecurityTokenInvalidIssuerException("Issuer path does not match token tenant");

                            return issuer;
                        }

                        // Generic OIDC path (e.g. Keycloak)
                        var tokenIssuer = GetTokenIssuer(securityToken);
                        if (string.IsNullOrWhiteSpace(tokenIssuer))
                            throw new SecurityTokenInvalidIssuerException("Token has no tid or iss claim");

                        var oidcTenant = tenants.FirstOrDefault(t =>
                            !string.IsNullOrWhiteSpace(t.OidcAuthority) &&
                            string.Equals(t.OidcAuthority.TrimEnd('/'), tokenIssuer.TrimEnd('/'), StringComparison.OrdinalIgnoreCase));

                        if (oidcTenant == null)
                            throw new SecurityTokenInvalidIssuerException("Token issuer does not match any trusted OIDC authority");

                        return issuer;
                    },

                    AudienceValidator = (audiences, securityToken, validationParameters) =>
                    {
                        var tenants = config.GetSection("PrismBusinessApp:Tenants").Get<List<BackOfficeTenant>>();
                        if (tenants == null || tenants.Count == 0) return false;

                        var tokenTenantId = GetTokenTenantId(securityToken);

                        if (!string.IsNullOrWhiteSpace(tokenTenantId))
                        {
                            // Entra CIAM path
                            var tenant = tenants.FirstOrDefault(t =>
                                string.Equals(t.EntraTenantId, tokenTenantId, StringComparison.OrdinalIgnoreCase));
                            if (tenant == null || string.IsNullOrWhiteSpace(tenant.ClientId)) return false;

                            return audiences.Any(aud => string.Equals(aud, tenant.ClientId, StringComparison.OrdinalIgnoreCase));
                        }

                        // Generic OIDC path
                        var tokenIssuer = GetTokenIssuer(securityToken);
                        if (string.IsNullOrWhiteSpace(tokenIssuer)) return false;

                        var oidcTenant = tenants.FirstOrDefault(t =>
                            !string.IsNullOrWhiteSpace(t.OidcAuthority) &&
                            string.Equals(t.OidcAuthority.TrimEnd('/'), tokenIssuer.TrimEnd('/'), StringComparison.OrdinalIgnoreCase));

                        if (oidcTenant == null || string.IsNullOrWhiteSpace(oidcTenant.ClientId)) return false;

                        var audienceMatches = audiences.Any(aud =>
                            string.Equals(aud, oidcTenant.ClientId, StringComparison.OrdinalIgnoreCase));
                        var authorizedPartyMatches = string.Equals(
                            GetTokenAuthorizedParty(securityToken),
                            oidcTenant.ClientId,
                            StringComparison.OrdinalIgnoreCase);

                        return audienceMatches || authorizedPartyMatches;
                    },

                    IssuerSigningKeyResolver = (token, securityToken, kid, validationParameters) =>
                    {
                        var tenants = config.GetSection("PrismBusinessApp:Tenants").Get<List<BackOfficeTenant>>();
                        return ResolveSigningKeys(
                            GetTokenTenantId(securityToken),
                            kid,
                            tenants,
                            signingKeyCache,
                            GetTokenIssuer(securityToken));
                    }
                };
            });

        return services;
    }

    internal static IEnumerable<SecurityKey> ResolveSigningKeys(
        string? tokenTenantId,
        string? keyId,
        IReadOnlyCollection<BackOfficeTenant>? tenants,
        IPrismSigningKeyCache signingKeyCache,
        string? tokenIssuer = null)
    {
        if (!string.IsNullOrWhiteSpace(tokenTenantId))
        {
            // Entra CIAM path
            if (tenants == null || !tenants.Any(t => string.Equals(t.EntraTenantId, tokenTenantId, StringComparison.OrdinalIgnoreCase)))
                return Enumerable.Empty<SecurityKey>();

            var snapshot = signingKeyCache.GetSnapshot(tokenTenantId, keyId);

            if (snapshot.IsExpired || !snapshot.ContainsRequestedKey)
            {
                // Keys are missing or don't contain the required key — fetch them now.
                // Safe to block here: ASP.NET Core has no SynchronizationContext, and
                // WarmAsync's internal semaphore deduplicates concurrent fetches per tenant.
                signingKeyCache.WarmAsync(
                    tokenTenantId,
                    forceRefresh: true,
                    cancellationToken: CancellationToken.None).GetAwaiter().GetResult();

                snapshot = signingKeyCache.GetSnapshot(tokenTenantId, keyId);
            }
            else if (snapshot.ShouldRefresh)
            {
                // Keys exist and are valid but approaching expiry — refresh in the background.
                _ = signingKeyCache.WarmAsync(tokenTenantId, cancellationToken: CancellationToken.None);
            }

            return snapshot.ContainsRequestedKey ? snapshot.Keys : Enumerable.Empty<SecurityKey>();
        }

        // Generic OIDC path (e.g. Keycloak)
        if (string.IsNullOrWhiteSpace(tokenIssuer) || tenants == null)
            return Enumerable.Empty<SecurityKey>();

        var oidcTenant = tenants.FirstOrDefault(t =>
            !string.IsNullOrWhiteSpace(t.OidcAuthority) &&
            string.Equals(t.OidcAuthority.TrimEnd('/'), tokenIssuer.TrimEnd('/'), StringComparison.OrdinalIgnoreCase));

        if (oidcTenant == null) return Enumerable.Empty<SecurityKey>();

        var cacheKey = oidcTenant.OidcAuthority!.TrimEnd('/');
        // KEYCLOAK_BACKCHANNEL_URL: in Codespaces the GitHub forwarded-port proxy blocks
        // unauthenticated server-side requests to the external Keycloak URL. Use the
        // internal HTTP address for metadata fetches while keeping OidcAuthority as the
        // trusted issuer for token validation (same pattern as PrismOidcConfiguration).
        var backchannelBase = Environment.GetEnvironmentVariable("KEYCLOAK_BACKCHANNEL_URL");
        var metadataAddress = !string.IsNullOrEmpty(backchannelBase)
            ? $"{backchannelBase.TrimEnd('/')}{new Uri(cacheKey).AbsolutePath}/.well-known/openid-configuration"
            : $"{cacheKey}/.well-known/openid-configuration";

        var oidcSnapshot = signingKeyCache.GetSnapshot(cacheKey, keyId);

        if (oidcSnapshot.IsExpired || !oidcSnapshot.ContainsRequestedKey)
        {
            signingKeyCache.WarmAsync(
                cacheKey,
                metadataAddress,
                forceRefresh: true,
                requiredKeyId: keyId,
                cancellationToken: CancellationToken.None).GetAwaiter().GetResult();

            oidcSnapshot = signingKeyCache.GetSnapshot(cacheKey, keyId);
        }
        else if (oidcSnapshot.ShouldRefresh)
        {
            _ = signingKeyCache.WarmAsync(cacheKey, metadataAddress, cancellationToken: CancellationToken.None);
        }

        return oidcSnapshot.ContainsRequestedKey ? oidcSnapshot.Keys : Enumerable.Empty<SecurityKey>();
    }

    private static string? GetTokenTenantId(SecurityToken securityToken)
    {
        if (securityToken is Microsoft.IdentityModel.JsonWebTokens.JsonWebToken jsonWebToken)
            return GetClaimValue(jsonWebToken, "tid");

        if (securityToken is JwtSecurityToken jwtSecurityToken)
            return jwtSecurityToken.Claims.FirstOrDefault(c => c.Type == "tid")?.Value;

        return null;
    }

    private static string? GetTokenIssuer(SecurityToken securityToken)
    {
        if (securityToken is Microsoft.IdentityModel.JsonWebTokens.JsonWebToken jsonWebToken)
            return GetClaimValue(jsonWebToken, "iss")
                ?? (string.IsNullOrEmpty(jsonWebToken.Issuer) ? null : jsonWebToken.Issuer);

        if (securityToken is JwtSecurityToken jwtSecurityToken)
            return jwtSecurityToken.Claims.FirstOrDefault(c => c.Type == "iss")?.Value
                ?? (string.IsNullOrEmpty(jwtSecurityToken.Issuer) ? null : jwtSecurityToken.Issuer);

        return null;
    }

    private static string? GetTokenAuthorizedParty(SecurityToken securityToken)
    {
        if (securityToken is Microsoft.IdentityModel.JsonWebTokens.JsonWebToken jsonWebToken)
            return GetClaimValue(jsonWebToken, "azp");

        if (securityToken is JwtSecurityToken jwtSecurityToken)
            return jwtSecurityToken.Claims.FirstOrDefault(c => c.Type == "azp")?.Value;

        return null;
    }

    private static string? GetClaimValue(Microsoft.IdentityModel.JsonWebTokens.JsonWebToken jsonWebToken, string claimType) =>
        jsonWebToken.Claims.FirstOrDefault(c => c.Type == claimType)?.Value;
}
