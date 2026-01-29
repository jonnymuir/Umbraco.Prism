using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using UmbracoPrism.Core.Models;
using UmbracoPrism.Core.Services;
using Microsoft.Identity.Web;
using Microsoft.Identity.Client;
using System.Security.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Identity.Web.TokenCacheProviders;
using System.Security.Claims;
using System.Text.Json;

namespace UmbracoPrism.Core.Auth;

public class PrismOidcConfiguration(IHttpContextAccessor httpContextAccessor) : IPostConfigureOptions<OpenIdConnectOptions>
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, ConfigurationManager<OpenIdConnectConfiguration>> _cache = new();

    public void PostConfigure(string? name, OpenIdConnectOptions options)
    {
        if (name != "PrismEntraID") return;

        Console.Error.WriteLine("PRISM DEBUG: Configuration started for " + name);

        // Basic Defaults
        options.ClientId = "DYNAMIC_TENANT_PLACEHOLDER";
        options.Authority = "https://login.microsoftonline.com/common/v2.0";
        options.MapInboundClaims = false;

        options.TokenValidationParameters.ValidateIssuer = false;
        options.TokenValidationParameters.ValidateAudience = false;
        options.ResponseType = OpenIdConnectResponseType.Code;
        options.ResponseMode = OpenIdConnectResponseMode.Query;
        options.SaveTokens = true;
        options.CallbackPath = "/signin-oidc";
        options.SignedOutCallbackPath = "/signout-callback-oidc";
        options.SignedOutRedirectUri = "/";

        // Add scopes cooperatively
        if (!options.Scope.Contains("openid")) options.Scope.Add("openid");
        if (!options.Scope.Contains("profile")) options.Scope.Add("profile");
        if (!options.Scope.Contains("offline_access")) options.Scope.Add("offline_access");

        options.TokenValidationParameters.IssuerSigningKeyResolver = (token, securityToken, kid, validationParameters) =>
        {
            var httpContext = httpContextAccessor.HttpContext;
            var prismContext = httpContext?.RequestServices.GetRequiredService<IPrismContext>();
            var tenant = prismContext?.CurrentTenant;

            if (tenant == null || string.IsNullOrEmpty(tenant.EntraTenantId)) return [];

            validationParameters.ValidAudience = tenant.EntraClientId;
            validationParameters.ValidIssuer = $"https://{tenant.EntraTenantId}.ciamlogin.com/{tenant.EntraTenantId}/v2.0";
            validationParameters.ValidateAudience = true;
            validationParameters.ValidateIssuer = true;

            var metadataAddress = $"https://{tenant.EntraTenantId}.ciamlogin.com/{tenant.EntraTenantId}/v2.0/.well-known/openid-configuration";

            var manager = _cache.GetOrAdd(tenant.EntraTenantId, _ =>
                new ConfigurationManager<OpenIdConnectConfiguration>(
                    metadataAddress,
                    new OpenIdConnectConfigurationRetriever(),
                    new HttpDocumentRetriever(options.Backchannel) { RequireHttps = true }
                ));

            var config = manager.GetConfigurationAsync(CancellationToken.None).GetAwaiter().GetResult();
            return config.SigningKeys;
        };

        // --- Event Wrapping Logic ---
        // We capture the handlers registered by Microsoft.Identity.Web and wrap them.

        var onRedirectToIdentityProvider = options.Events.OnRedirectToIdentityProvider;
        options.Events.OnRedirectToIdentityProvider = async context =>
        {
            var prismContext = context.HttpContext.RequestServices.GetRequiredService<IPrismContext>();
            var tenant = prismContext?.CurrentTenant;

            if (tenant != null && !string.IsNullOrEmpty(tenant.EntraTenantId))
            {
                // 1. Set our specific tenant details BEFORE calling Microsoft's logic
                var baseUri = $"https://{tenant.EntraTenantId}.ciamlogin.com/{tenant.EntraTenantId}";
                context.ProtocolMessage.IssuerAddress = $"{baseUri}/oauth2/v2.0/authorize";
                context.ProtocolMessage.ClientId = tenant.EntraClientId;
                context.Options.Authority = $"{baseUri}/v2.0";
            }

            Console.WriteLine($"PRISM DEBUG: Redirecting to {context.ProtocolMessage.IssuerAddress} for Client {context.ProtocolMessage.ClientId}");


            // 2. Now call Microsoft's internal logic
            // It will see the message already has a ClientId and IssuerAddress and should respect them
            await onRedirectToIdentityProvider(context);

            // 3. Post-processing: Ensure the Prompt is set after MS might have messed with it
            if (tenant != null)
            {
                context.ProtocolMessage.Prompt = "select_account";
            }

            context.Properties.Items["Prism_PKCE_Verifier"] = context.Properties.Items.TryGetValue("code_verifier", out var verifier)
                ? verifier
                : "";
        };

        options.Events.OnAuthorizationCodeReceived = async context =>
        {
            var prismContext = context.HttpContext.RequestServices.GetRequiredService<IPrismContext>();
            var tenant = prismContext?.CurrentTenant;

            if (tenant != null && !string.IsNullOrEmpty(tenant.EntraTenantId))
            {
                var vault = context.HttpContext.RequestServices.GetRequiredService<ISecretVaultService>();
                var secret = await vault.GetSecretAsync(tenant.SecretKeyName);

                // CIAM / Entra ID Token Endpoint
                var authority = $"https://{tenant.EntraTenantId}.ciamlogin.com/{tenant.EntraTenantId}/oauth2/v2.0/token";
                var redirectUri = $"{context.Request.Scheme}://{context.Request.Host}{context.Request.PathBase}{options.CallbackPath}";

                string? verifier = null;
                context.Properties?.Items.TryGetValue("Prism_PKCE_Verifier", out verifier);

                // 1. Manually exchange the code for tokens via simple HTTP
                using var client = new HttpClient();
                var response = await client.PostAsync(authority, new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "client_id", tenant.EntraClientId },
                    { "client_secret", secret },
                    { "grant_type", "authorization_code" },
                    { "code", context.ProtocolMessage.Code },
                    { "redirect_uri", redirectUri },
                    { "code_verifier", verifier ?? "" },
                    { "scope", $"openid profile offline_access {tenant.EntraClientId}/.default" }
                }));

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    throw new AuthenticationException($"Token exchange failed: {error}");
                }

                var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                // 1. Manually exchange and get the payload as you are doing...
                var payload = json.RootElement;

                // 2. Clear and Store specifically what WE want
                var tokens = new List<AuthenticationToken>
                {
                    new AuthenticationToken { Name = "access_token", Value = payload.GetProperty("access_token").GetString() },
                    new AuthenticationToken { Name = "refresh_token", Value = payload.GetProperty("refresh_token").GetString() },
                    new AuthenticationToken { Name = "expires_at", Value = DateTimeOffset.UtcNow.AddSeconds(payload.GetProperty("expires_in").GetInt32()).ToString("o") }
                };

                // Use the existing properties from the context (which contain the PKCE verifier, etc.)
                var props = context.Properties ?? new AuthenticationProperties();
                props.StoreTokens(tokens);

                // 3. Create your Principal (using the ID Token you just got)
                var idToken = payload.GetProperty("id_token").GetString();
                var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
                var jwt = handler.ReadJwtToken(idToken);
                var identity = new ClaimsIdentity(jwt.Claims, "PrismEntraID", "name", "role");
                var principal = new ClaimsPrincipal(identity);

                // 4. THE CRITICAL STEP: Manual Sign-In
                // We pass 'props' here. This is what writes the encrypted cookie.
                await context.HttpContext.SignInAsync("PrismMemberCookie", principal, props);

                // 5. Tell the OIDC middleware to STOP. 
                // If we don't call HandleResponse, it will try to sign in again and overwrite our cookie.
                context.HandleResponse();

                // 6. Redirect manually
                var returnUrl = props.RedirectUri ?? "/";
                context.Response.Redirect(returnUrl);
            }
        };

        var onRedirectToIdentityProviderForSignOut = options.Events.OnRedirectToIdentityProviderForSignOut;
        options.Events.OnRedirectToIdentityProviderForSignOut = async context =>
        {
            await onRedirectToIdentityProviderForSignOut(context);

            var prismContext = context.HttpContext.RequestServices.GetRequiredService<IPrismContext>();
            var tenant = prismContext?.CurrentTenant;

            if (tenant != null && !string.IsNullOrEmpty(tenant.EntraTenantId))
            {
                var baseUri = $"https://{tenant.EntraTenantId}.ciamlogin.com/{tenant.EntraTenantId}";
                context.ProtocolMessage.IssuerAddress = $"{baseUri}/oauth2/v2.0/logout";

                var userEmail = context.HttpContext.User.FindFirst("preferred_username")?.Value
                                ?? context.HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;

                if (!string.IsNullOrEmpty(userEmail))
                {
                    context.ProtocolMessage.SetParameter("logout_hint", userEmail);
                }
            }
        };

    }

    //public void Configure(OpenIdConnectOptions options) => Configure(Options.DefaultName, options);
}