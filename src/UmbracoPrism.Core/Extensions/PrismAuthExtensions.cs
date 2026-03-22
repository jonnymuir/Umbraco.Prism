using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Collections.Concurrent;

namespace UmbracoPrism.Core.Extensions;

public static class PrismAuthExtensions
{
    private static readonly ConcurrentDictionary<string, ConfigurationManager<OpenIdConnectConfiguration>> _keyCache = new();

    public static IServiceCollection AddPrismAuthentication(this IServiceCollection services, IConfiguration config)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
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
                        var tenants = config.GetSection("PrismBackOffice:Tenants").Get<List<BackOfficeTenant>>();
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
                        var tenants = config.GetSection("PrismBackOffice:Tenants").Get<List<BackOfficeTenant>>();
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
                        string? tid = null;

                        if (securityToken is Microsoft.IdentityModel.JsonWebTokens.JsonWebToken jsonWebToken)
                            tid = jsonWebToken.GetClaim("tid")?.Value;
                        else if (securityToken is System.IdentityModel.Tokens.Jwt.JwtSecurityToken jwtSecurityToken)
                            tid = jwtSecurityToken.Claims.FirstOrDefault(c => c.Type == "tid")?.Value;

                        if (string.IsNullOrEmpty(tid)) return Enumerable.Empty<SecurityKey>();

                        var tenants = config.GetSection("PrismBackOffice:Tenants").Get<List<BackOfficeTenant>>();
                        if (tenants == null || !tenants.Any(t => string.Equals(t.EntraTenantId, tid, StringComparison.OrdinalIgnoreCase)))
                            return Enumerable.Empty<SecurityKey>();

                        var metadataAddress = $"https://{tid}.ciamlogin.com/{tid}/v2.0/.well-known/openid-configuration";

                        var manager = _keyCache.GetOrAdd(tid, _ => 
                            new ConfigurationManager<OpenIdConnectConfiguration>(metadataAddress, new OpenIdConnectConfigurationRetriever()));

                        try
                        {
                            var oidcConfig = manager.GetConfigurationAsync(CancellationToken.None).GetAwaiter().GetResult();
                            return oidcConfig.SigningKeys;
                        }
                        catch
                        {
                            return Enumerable.Empty<SecurityKey>();
                        }
                    }
                };
            });

        return services;
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

public record BackOfficeTenant(string EntraTenantId, string ClientId, string Code, string DisplayName);
