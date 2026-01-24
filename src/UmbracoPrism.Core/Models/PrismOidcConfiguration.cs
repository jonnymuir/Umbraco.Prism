using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using UmbracoPrism.Core.Models;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Auth;

public class PrismOidcConfiguration(
    IHttpContextAccessor httpContextAccessor,
    ISecretVaultService secretVault) : IConfigureNamedOptions<OpenIdConnectOptions>
{
    public void Configure(string? name, OpenIdConnectOptions options)
    {
        if (name != "PrismEntraID") return;

        // Use IPrismContext to find WHICH tenant we are talking to based on the URL
        var prismContext = httpContextAccessor.HttpContext?.RequestServices.GetRequiredService<IPrismContext>();
        var tenant = prismContext?.CurrentTenant;
        
        if (tenant == null || string.IsNullOrEmpty(tenant.EntraTenantId)) 
        {
            return;
        }

        // 1. CIAM Authority format
        // External ID tenants use: {tenant-prefix}.ciamlogin.com
        options.Authority = $"https://{tenant.EntraTenantId}.ciamlogin.com/"; 
        options.ClientId = tenant.EntraClientId;

        // 2. Fetch the secret (Keep the .GetAwaiter().GetResult() for synchronous config)
        if (!string.IsNullOrEmpty(tenant.SecretKeyName))
        {
            options.ClientSecret = secretVault.GetSecretAsync(tenant.SecretKeyName).GetAwaiter().GetResult();
        }

        options.ResponseType = "code";
        options.ResponseMode = "query";
        options.SaveTokens = true;
        
        // CIAM requires these scopes usually
        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("offline_access");

        options.CallbackPath = "/signin-oidc";
    }

    public void Configure(OpenIdConnectOptions options) => Configure(Options.DefaultName, options);
}