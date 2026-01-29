using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
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
                        if (tenants != null && tenants.Any(t => issuer.Contains(t.EntraTenantId))) return issuer;
                        throw new SecurityTokenInvalidIssuerException("Unknown or untrusted issuer");
                    },

                    AudienceValidator = (audiences, securityToken, validationParameters) =>
                    {
                        var tenants = config.GetSection("PrismBackOffice:Tenants").Get<List<BackOfficeTenant>>();
                        if (tenants == null) return false;
                        
                        var allowedClientIds = tenants.Select(t => t.ClientId).ToList();
                        return audiences.Any(aud => allowedClientIds.Any(id => id.Equals(aud, StringComparison.OrdinalIgnoreCase)));
                    },

                    IssuerSigningKeyResolver = (token, securityToken, kid, validationParameters) =>
                    {
                        string? tid = null;

                        if (securityToken is Microsoft.IdentityModel.JsonWebTokens.JsonWebToken jsonWebToken)
                            tid = jsonWebToken.GetClaim("tid")?.Value;
                        else if (securityToken is System.IdentityModel.Tokens.Jwt.JwtSecurityToken jwtSecurityToken)
                            tid = jwtSecurityToken.Claims.FirstOrDefault(c => c.Type == "tid")?.Value;

                        if (string.IsNullOrEmpty(tid)) return Enumerable.Empty<SecurityKey>();

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
}

public record BackOfficeTenant(string EntraTenantId, string ClientId, string Code, string DisplayName);
