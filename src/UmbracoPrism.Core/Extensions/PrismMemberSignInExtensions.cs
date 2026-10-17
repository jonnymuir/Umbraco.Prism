using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;
using UmbracoPrism.Core.Auth;
using UmbracoPrism.Core.Models;

namespace UmbracoPrism.Core.Extensions;

/// <summary>
/// Member sign-in: the tenant-bound member cookie, per-tenant OIDC, and the per-request defaults
/// that keep both confined to tenants with sign-in configured. <see cref="PrismComposer"/> calls
/// this; a host never needs to.
/// </summary>
public static class PrismMemberSignInExtensions
{
    public static IServiceCollection AddPrismMemberSignIn(this IServiceCollection services)
    {
        // No defaults are set here. PrismAuthenticationDefaultsSetup installs per-request
        // selectors after the host's own configuration, so a hostname without a signing-in
        // tenant keeps the host application's authentication exactly as it was.
        var authBuilder = services.AddAuthentication();

        authBuilder.AddMicrosoftIdentityWebApp(identityOptions =>
        {
            // Placeholders satisfy startup validation.
            // Our CredentialProvider and OidcConfiguration will swap these out at runtime.
            identityOptions.Instance = "https://login.microsoftonline.com/";
            identityOptions.TenantId = "common";
            identityOptions.ClientId = "DYNAMIC_PLACEHOLDER";
            identityOptions.CallbackPath = "/signin-oidc";
            identityOptions.SignedOutCallbackPath = "/signout-callback-oidc";

            // Note: We no longer need TEMPORARY_PLACEHOLDER for the secret
            // because the presence of an IClientAssertionProvider tells MSAL to use that instead.
        }, cookieOptions =>
        {
            cookieOptions.Cookie.Name = PrismAuthenticationSchemes.MemberCookie;
            cookieOptions.LoginPath = "/auth/login";
            cookieOptions.Cookie.SameSite = SameSiteMode.Lax;
            cookieOptions.Cookie.SecurePolicy = CookieSecurePolicy.Always;

            // Tenant-binding enforcement, applied automatically to every PrismMemberCookie
            // request, not opt-in per controller. Runs on every request that carries the
            // cookie; PrismTenantMiddleware (registered as a PrePipeline filter, above any
            // Umbraco middleware including this one) has already resolved IPrismContext.CurrentTenant
            // by the time this fires. A principal is rejected, and the request continues as
            // anonymous rather than as the wrong tenant's member, unless the hostname resolves to
            // a tenant that has sign-in configured and the principal's own tenant claims match
            // it. Endpoints that want a hard 403 instead of an anonymous fallback declare the
            // "PrismStrictIsolation" policy explicitly on top of this (see PrismTenantHandler),
            // sharing the same IPrismTenantBindingValidator check rather than reimplementing it.
            cookieOptions.Events.OnValidatePrincipal = context =>
            {
                var validator = context.HttpContext.RequestServices.GetRequiredService<IPrismTenantBindingValidator>();
                var prismContext = context.HttpContext.RequestServices.GetRequiredService<IPrismContext>();
                var tenant = prismContext.CurrentTenant;

                if (tenant is null ||
                    !tenant.HasSignInConfigured() ||
                    context.Principal is null ||
                    !validator.IsBound(context.Principal, tenant))
                {
                    context.RejectPrincipal();
                }

                return Task.CompletedTask;
            };
        }, openIdConnectScheme: PrismAuthenticationSchemes.Oidc, cookieScheme: PrismAuthenticationSchemes.MemberCookie)
        .EnableTokenAcquisitionToCallDownstreamApi()
        .AddInMemoryTokenCaches();

        authBuilder.AddPolicyScheme(PrismAuthenticationSchemes.DefaultAuthenticate, displayName: null,
            options => options.ForwardDefaultSelector = PrismDefaultSchemeSelector.Authenticate);
        authBuilder.AddPolicyScheme(PrismAuthenticationSchemes.DefaultChallenge, displayName: null,
            options => options.ForwardDefaultSelector = PrismDefaultSchemeSelector.Challenge);
        authBuilder.AddScheme<AuthenticationSchemeOptions, PrismNoHostDefaultHandler>(
            PrismAuthenticationSchemes.NoHostDefault, displayName: null, _ => { });

        services.AddSingleton<PrismHostAuthenticationDefaults>();
        services.AddSingleton<IPostConfigureOptions<AuthenticationOptions>, PrismAuthenticationDefaultsSetup>();

        return services;
    }
}
