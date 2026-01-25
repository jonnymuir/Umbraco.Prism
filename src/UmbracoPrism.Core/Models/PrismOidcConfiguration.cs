using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using UmbracoPrism.Core.Models;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Auth;

public class PrismOidcConfiguration(IHttpContextAccessor httpContextAccessor) : IConfigureNamedOptions<OpenIdConnectOptions>
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Microsoft.IdentityModel.Protocols.ConfigurationManager<Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectConfiguration>> _cache = new();

    public void Configure(string? name, OpenIdConnectOptions options)
    {
        if (name != "PrismEntraID") return;

        // Note: The logic here only runs ONCE for the first tenant. 
        // We set safe defaults, but the Events below handle the per-tenant "hijack".
        options.ClientId = "DYNAMIC_TENANT_PLACEHOLDER";
        options.Authority = "https://login.microsoftonline.com/common/v2.0";
        options.MapInboundClaims = false;

        options.TokenValidationParameters.ValidateIssuer = false;
        options.TokenValidationParameters.ValidateAudience = false;
        options.ResponseType = OpenIdConnectResponseType.Code;
        options.ResponseMode = OpenIdConnectResponseMode.Query;
        options.SaveTokens = true;
        options.CallbackPath = "/signin-oidc";

        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("offline_access");

        options.TokenValidationParameters.IssuerSigningKeyResolver = (token, securityToken, kid, validationParameters) =>
        {
            // Get the context from the current request
            var httpContext = httpContextAccessor.HttpContext;
            var prismContext = httpContext?.RequestServices.GetRequiredService<IPrismContext>();
            var tenant = prismContext?.CurrentTenant;

            if (tenant == null) return Enumerable.Empty<SecurityKey>();

            validationParameters.ValidAudience = tenant.EntraClientId;
            validationParameters.ValidIssuer = $"https://{tenant.EntraTenantId}.ciamlogin.com/{tenant.EntraTenantId}/v2.0";
            validationParameters.ValidateAudience = true;
            validationParameters.ValidateIssuer = true;

            // Use a per-tenant cache or fetch the keys directly from the tenant's JWKS endpoint
            // For a robust version, you'd want to cache these keys for 24 hours so you don't hit MSFT on every login
            var metadataAddress = $"https://{tenant.EntraTenantId}.ciamlogin.com/{tenant.EntraTenantId}/v2.0/.well-known/openid-configuration";
            // This is a simplified fetch; in production, use a cached ConfigurationManager per tenant ID
            // Thread-safe: Get or add the manager for this specific tenant
            var manager = _cache.GetOrAdd(tenant.EntraTenantId!, _ =>
                new ConfigurationManager<OpenIdConnectConfiguration>(
                    metadataAddress,
                    new OpenIdConnectConfigurationRetriever(),
                    new HttpDocumentRetriever(options.Backchannel) { RequireHttps = true }
                ));

            // GetConfigurationAsync has internal caching; it won't hit the network every time
            var config = manager.GetConfigurationAsync(CancellationToken.None).GetAwaiter().GetResult();
            return config.SigningKeys;
        };

        options.Events = new OpenIdConnectEvents
        {
            OnRedirectToIdentityProvider = context =>
            {
                var prismContext = context.HttpContext.RequestServices.GetRequiredService<IPrismContext>();
                var tenant = prismContext?.CurrentTenant;

                if (tenant != null && !string.IsNullOrEmpty(tenant.EntraTenantId))
                {
                    // 1. Point browser to the correct Tenant's authorize endpoint
                    var baseUri = $"https://{tenant.EntraTenantId}.ciamlogin.com/{tenant.EntraTenantId}";
                    context.ProtocolMessage.IssuerAddress = $"{baseUri}/oauth2/v2.0/authorize";
                    context.ProtocolMessage.ClientId = tenant.EntraClientId;
                    context.ProtocolMessage.Prompt = "select_account";
                }

                return Task.CompletedTask;
            },
            OnAuthorizationCodeReceived = context =>
            {
                var prismContext = context.HttpContext.RequestServices.GetRequiredService<IPrismContext>();
                var tenant = prismContext?.CurrentTenant;

                if (tenant != null && !string.IsNullOrEmpty(tenant.EntraTenantId))
                {
                    // 2. Point the back-channel code exchange to the correct Tenant's token endpoint
                    // This is the CRITICAL fix for the 'User account does not exist' error.
                    var baseUri = $"https://{tenant.EntraTenantId}.ciamlogin.com/{tenant.EntraTenantId}";
                    context.TokenEndpointRequest!.TokenEndpoint = $"{baseUri}/oauth2/v2.0/token";

                    context.TokenEndpointRequest!.ClientId = tenant.EntraClientId;

                    if (!string.IsNullOrEmpty(tenant.SecretKeyName))
                    {
                        var vault = context.HttpContext.RequestServices.GetRequiredService<ISecretVaultService>();
                        context.TokenEndpointRequest.ClientSecret = vault.GetSecretAsync(tenant.SecretKeyName).GetAwaiter().GetResult();
                    }
                }
                return Task.CompletedTask;
            },
            OnRedirectToIdentityProviderForSignOut = context =>
            {
                var tenant = httpContextAccessor.HttpContext?.RequestServices.GetRequiredService<IPrismContext>()?.CurrentTenant;
                if (tenant != null)
                {
                    var baseUri = $"https://{tenant.EntraTenantId}.ciamlogin.com/{tenant.EntraTenantId}";

                    // Explicitly point to the tenant's logout endpoint
                    context.ProtocolMessage.IssuerAddress = $"{baseUri}/oauth2/v2.0/logout";

                    // Pass the hint of who we are trying to sign out 
                    // This helps Microsoft show the CORRECT account in that 'Pick an account' screen
                    var userEmail = context.HttpContext.User.FindFirst("preferred_username")?.Value
                                ?? context.HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;

                    if (!string.IsNullOrEmpty(userEmail))
                    {
                        context.ProtocolMessage.SetParameter("logout_hint", userEmail);
                    }
                }
                return Task.CompletedTask;
            }
        };
    }

    public void Configure(OpenIdConnectOptions options) => Configure(Options.DefaultName, options);
}