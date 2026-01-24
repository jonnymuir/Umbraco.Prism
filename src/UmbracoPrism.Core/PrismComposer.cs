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

namespace UmbracoPrism.Core;

/// <summary>
/// Composer for registering Prism services, middleware, and migrations.
/// </summary>
public class PrismComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        // 1. Core Services
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton<ISecretVaultService, SecretVaultService>();
        builder.Services.AddSingleton<ITenantService, TenantService>();
        builder.Services.AddScoped<IPrismContext, PrismContext>();
        builder.Services.AddScoped<IPrismUserContext, PrismUserContext>();

        // 2. Middleware Registration
        builder.Services.Configure<UmbracoPipelineOptions>(options =>
        {
            options.AddFilter(new UmbracoPipelineFilter(
                "PrismTenantResolution",
                app => app.UseMiddleware<PrismTenantMiddleware>()
            ));
        });

        // 3. Authorization Handler
        builder.Services.AddSingleton<IAuthorizationHandler, PrismTenantHandler>();

        // 4. Dynamic OIDC Config Link
        builder.Services.ConfigureOptions<PrismOidcConfiguration>();

        // 5. Authentication & Cookie Setup - only if Vault URI is set
        var vaultUri = builder.Config["Prism:VaultUri"];
        bool isAuthEnabled = !string.IsNullOrEmpty(vaultUri);


        builder.Services.AddAuthentication(options =>
            {
                // ONLY hijack the defaults if the vault is configured
                if (isAuthEnabled)
                {
                    options.DefaultAuthenticateScheme = "PrismMemberCookie";
                    options.DefaultSignInScheme = "PrismMemberCookie";
                    options.DefaultChallengeScheme = "PrismEntraID";
                }
            })
            .AddCookie("PrismMemberCookie", options =>
            {
                options.LoginPath = "/auth/login";
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            })
            .AddOpenIdConnect("PrismEntraID", _ => { });

        // 6. Authorization Policy
        builder.Services.Configure<AuthorizationOptions>(options =>
        {
            options.AddPolicy("PrismStrictIsolation", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new PrismTenantRequirement());
            });
        });

        // 7. Management API & Notifications
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartingNotification, PrismMigrationHandler>();
        builder.Services.ConfigureOptions<PrismManagementApiConfiguration>();
    }
}