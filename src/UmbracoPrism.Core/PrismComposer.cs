using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using UmbracoPrism.Core.Models;
using UmbracoPrism.Core.Services;
using Umbraco.Cms.Web.Common.ApplicationBuilder;
using UmbracoPrism.Core.Middleware;
using Umbraco.Cms.Core.Notifications;
using UmbracoPrism.Core.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Identity.Web;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace UmbracoPrism.Core;

public class PrismComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        // 1. Core Services
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton<ISecretVaultService, SecretVaultService>();
        builder.Services.AddSingleton<ITenantService, TenantService>();
        builder.Services.AddSingleton<IBrandingService, BrandingService>();
        builder.Services.AddSingleton<IMobileBundleService, MobileBundleService>();
        builder.Services.AddSingleton<IPrismSigningKeyCache, PrismSigningKeyCache>();
        builder.Services.AddHttpClient("prism-oidc-metadata");
        builder.Services.AddHttpClient("PrismTokenRefresh");
        builder.Services.Configure<PrismTokenRefreshOptions>(
            builder.Config.GetSection(PrismTokenRefreshOptions.SectionName));
        builder.Services.AddSingleton<IPrismTokenRefreshService, PrismTokenRefreshService>();
        builder.Services.AddScoped<IPrismContext, PrismContext>();
        builder.Services.AddScoped<IPrismUserContext, PrismUserContext>();

        // 2. Middleware Registration
        builder.Services.Configure<UmbracoPipelineOptions>(options =>
        {
            options.AddFilter(new UmbracoPipelineFilter(
                "PrismTenantResolution",
                app =>
                {
                    app.UseMiddleware<PrismTenantMiddleware>();
                    app.UseMiddleware<PrismBrandingMiddleware>();
                }
            ));
        });

        // 3. Authorization Handler
        builder.Services.AddSingleton<IAuthorizationHandler, PrismTenantHandler>();
        builder.Services.AddSingleton<IAuthorizationHandler, PrismAdminHandler>();

        // 4. Dynamic OIDC Config & Credential Provider
        // Registering our custom PostConfigure logic as a Singleton is fine because 
        // it acts on the 'options' object passed in per-request.
        builder.Services.AddSingleton<IPostConfigureOptions<OpenIdConnectOptions>, PrismOidcConfiguration>();
        
        // 5. Authentication & Cookie Setup
        var vaultUri = builder.Config["Prism:VaultUri"];
        bool isAuthEnabled = !string.IsNullOrEmpty(vaultUri);

        var authBuilder = builder.Services.AddAuthentication(options =>
        {
            if (isAuthEnabled)
            {
                options.DefaultAuthenticateScheme = "PrismMemberCookie";
                options.DefaultSignInScheme = "PrismMemberCookie";
                options.DefaultChallengeScheme = "PrismEntraID";
            }
        });

        authBuilder.AddMicrosoftIdentityWebApp(identityOptions =>
        {
            // Placeholders satisfy startup validation. 
            // Our CredentialProvider and OidcConfiguration will swap these out at runtime.
            identityOptions.Instance = "https://login.microsoftonline.com/";
            identityOptions.TenantId = "common";
            identityOptions.ClientId = "DYNAMIC_PLACEHOLDER";
            identityOptions.CallbackPath = "/signin-oidc";
            identityOptions.SignedOutCallbackPath = "/signout-oidc";
            
            // Note: We no longer need TEMPORARY_PLACEHOLDER for the secret 
            // because the presence of an IClientAssertionProvider tells MSAL to use that instead.
        }, cookieOptions =>
        {
            cookieOptions.Cookie.Name = "PrismMemberCookie";
            cookieOptions.LoginPath = "/auth/login";
            cookieOptions.Cookie.SameSite = SameSiteMode.Lax;
            cookieOptions.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        }, openIdConnectScheme: "PrismEntraID", cookieScheme: "PrismMemberCookie")
        .EnableTokenAcquisitionToCallDownstreamApi()
        .AddInMemoryTokenCaches();

        // 6. Authorization Policy
        builder.Services.Configure<AuthorizationOptions>(options =>
        {
            options.AddPolicy("PrismStrictIsolation", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new PrismTenantRequirement());
            });

            options.AddPolicy("PrismAdmins", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new PrismAdminRequirement());
            });
        });

        builder.Services.Configure<PrismAdminOptions>(builder.Config.GetSection("Prism:AdminGroups"));

        // 7. Management API & Notifications
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartingNotification, PrismMigrationHandler>();
        builder.Services.ConfigureOptions<PrismManagementApiConfiguration>();
    }
}