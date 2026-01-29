using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Events.OnAuthenticationFailed = context =>
                    {
                        // This is vital for debugging!
                        Console.WriteLine($"Auth Failed: {context.Exception.Message}");
                        return Task.CompletedTask;
                    };

        // 1. Basic properties
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            // Re-use your existing logic for these
            IssuerValidator = (issuer, securityToken, validationParameters) =>
            {
                var tenants = builder.Configuration.GetSection("PrismBackOffice:Tenants").Get<List<BackOfficeTenant>>();
                if (tenants.Any(t => issuer.Contains(t.EntraTenantId))) return issuer;
                throw new SecurityTokenInvalidIssuerException("Unknown issuer");
            },
            AudienceValidator = (audiences, securityToken, validationParameters) =>
            {
                var tenants = builder.Configuration.GetSection("PrismBackOffice:Tenants").Get<List<BackOfficeTenant>>();
                var allowedClientIds = tenants.Select(t => t.ClientId).ToList();
                return audiences.Any(aud => allowedClientIds.Any(id => id.Equals(aud, StringComparison.OrdinalIgnoreCase)));
            },

            IssuerSigningKeyResolver = (token, securityToken, kid, validationParameters) =>
            {
                string? tid = null;

                // Handle the modern JsonWebToken (default in .NET 8+)
                if (securityToken is Microsoft.IdentityModel.JsonWebTokens.JsonWebToken jsonWebToken)
                {
                    tid = jsonWebToken.GetClaim("tid")?.Value;
                }
                // Fallback for the older JwtSecurityToken
                else if (securityToken is System.IdentityModel.Tokens.Jwt.JwtSecurityToken jwtSecurityToken)
                {
                    tid = jwtSecurityToken.Claims.FirstOrDefault(c => c.Type == "tid")?.Value;
                }

                if (string.IsNullOrEmpty(tid))
                {
                    Console.WriteLine("PRISM DEBUG: Could not find 'tid' claim in token.");
                    return Enumerable.Empty<SecurityKey>();
                }

                // Construct the CIAM-specific metadata address
                var metadataAddress = $"https://{tid}.ciamlogin.com/{tid}/v2.0/.well-known/openid-configuration";

                try
                {
                    var manager = KeyManagerCache.GetOrAdd(tid, metadataAddress);
                    var config = manager.GetConfigurationAsync(CancellationToken.None).GetAwaiter().GetResult();

                    Console.WriteLine($"PRISM DEBUG: Successfully fetched {config.SigningKeys.Count} keys for tenant {tid}");
                    return config.SigningKeys;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"PRISM DEBUG: Failed to fetch metadata for {tid}: {ex.Message}");
                    return Enumerable.Empty<SecurityKey>();
                }
            }
        };
    });



builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/api/backoffice/me", (IConfiguration config, ClaimsPrincipal user) =>
{
    // 1. Get the Tenant ID from the Token
    var tid = user.FindFirst("tid")?.Value 
              ?? user.FindFirst("http://schemas.microsoft.com/identity/claims/tenantid")?.Value;
              
    var email = user.FindFirst("preferred_username")?.Value ?? user.FindFirst(ClaimTypes.Email)?.Value;

    if (string.IsNullOrEmpty(tid) || string.IsNullOrEmpty(email))
        return Results.Unauthorized();

    // 2. Resolve Tenant
    var tenants = config.GetSection("PrismBackOffice:Tenants").Get<List<BackOfficeTenant>>();
    var tenant = tenants?.FirstOrDefault(t => t.EntraTenantId == tid);

    if (tenant == null) return Results.Problem("Tenant not recognized by Back Office.");

    // 3. Resolve Member (Check email AND tenant ID)
    var members = config.GetSection("PrismBackOffice:Members").Get<List<BackOfficeMember>>();
    var member = members?.FirstOrDefault(m =>
        m.Email.Equals(email, StringComparison.OrdinalIgnoreCase) &&
        m.EntraTenantId == tid);

    return Results.Ok(new
    {
        Tenant = tenant.DisplayName,
        TenantCode = tenant.Code,
        UserEmail = email,
        IsRegistered = member != null,
        BackOfficeId = member?.BackOfficeId ?? "N/A",
        AssignedRole = member?.Role ?? "Guest"
    });
}).RequireAuthorization();

app.Run();

public record BackOfficeTenant(string EntraTenantId, string ClientId, string Code, string DisplayName);
public record BackOfficeMember(string Email, string EntraTenantId, string BackOfficeId, string Role);

// Simple helper to prevent memory leaks and redundant HTTP calls
public static class KeyManagerCache
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, ConfigurationManager<OpenIdConnectConfiguration>> _cache = new();
    public static ConfigurationManager<OpenIdConnectConfiguration> GetOrAdd(string tid, string url) =>
        _cache.GetOrAdd(tid, _ => new ConfigurationManager<OpenIdConnectConfiguration>(url, new OpenIdConnectConfigurationRetriever()));
}