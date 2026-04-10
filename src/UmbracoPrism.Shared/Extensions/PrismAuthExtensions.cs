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

                        var configuredTenantIds = tenants
                            .Select(t => t.EntraTenantId)
                            .Where(tid => !string.IsNullOrWhiteSpace(tid))
                            .ToHashSet(StringComparer.OrdinalIgnoreCase);

                        var tokenTenantId = GetTokenTenantId(securityToken);
                        if (string.IsNullOrWhiteSpace(tokenTenantId) || !configuredTenantIds.Contains(tokenTenantId))
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
                    },

                    AudienceValidator = (audiences, securityToken, validationParameters) =>
                    {
                        var tenants = config.GetSection("PrismBusinessApp:Tenants").Get<List<BackOfficeTenant>>();
                        if (tenants == null || tenants.Count == 0) return false;

                        var tokenTenantId = GetTokenTenantId(securityToken);
                        if (string.IsNullOrWhiteSpace(tokenTenantId)) return false;

                        var tenant = tenants.FirstOrDefault(t =>
                            string.Equals(t.EntraTenantId, tokenTenantId, StringComparison.OrdinalIgnoreCase));
                        if (tenant == null || string.IsNullOrWhiteSpace(tenant.ClientId)) return false;

                        return audiences.Any(aud => string.Equals(aud, tenant.ClientId, StringComparison.OrdinalIgnoreCase));
                    },

                    IssuerSigningKeyResolver = (token, securityToken, kid, validationParameters) =>
                    {
                        var tenants = config.GetSection("PrismBusinessApp:Tenants").Get<List<BackOfficeTenant>>();
                        return ResolveSigningKeys(
                            GetTokenTenantId(securityToken),
                            kid,
                            tenants,
                            signingKeyCache);
                    }
                };
            });

        return services;
    }

    internal static IEnumerable<SecurityKey> ResolveSigningKeys(
        string? tokenTenantId,
        string? keyId,
        IReadOnlyCollection<BackOfficeTenant>? tenants,
        IPrismSigningKeyCache signingKeyCache)
    {
        if (string.IsNullOrWhiteSpace(tokenTenantId))
            return Enumerable.Empty<SecurityKey>();

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

    private static string? GetTokenTenantId(SecurityToken securityToken)
    {
        if (securityToken is Microsoft.IdentityModel.JsonWebTokens.JsonWebToken jsonWebToken)
            return jsonWebToken.GetClaim("tid")?.Value;

        if (securityToken is JwtSecurityToken jwtSecurityToken)
            return jwtSecurityToken.Claims.FirstOrDefault(c => c.Type == "tid")?.Value;

        return null;
    }
}
