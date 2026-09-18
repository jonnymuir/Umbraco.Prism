using Microsoft.AspNetCore.Authentication;
using UmbracoPrism.Core.Extensions;
using UmbracoPrism.TestSite;
using Wayfinder.Engine.Http;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// A real multi-file upload against the file-upload component's per-field default of 10MB (five
// fields on the licence-transfer demo, some with a larger explicit MaxSizeBytes) crashed with an
// unhandled BadHttpRequestException, not a graceful validation error. The actual limit hit isn't
// Kestrel's own default — it's Umbraco.Cms.Core.Configuration.Models.RuntimeSettings.MaxRequestLength
// (default 50MB, appsettings key Umbraco:CMS:Runtime:MaxRequestLength, in KB), which
// UmbracoRequestMiddleware applies to IHttpMaxRequestBodySizeFeature on every Umbraco-routed
// request — set there (appsettings.json) to match the value below. This Kestrel-level ceiling is
// kept too, as a fallback for any endpoint that doesn't go through Umbraco's request pipeline.
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 100 * 1024 * 1024);

// Local secrets override — gitignored. Place Prism:VaultUri and any other
// environment-specific secrets here. See src/UmbracoPrism.TestSite/README.md.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// A signed webhook when nothing else supplies the signing key; the same trusted-loopback-demo
// fallback Wayfinder.Umbraco.ReferenceApp's own Program.cs uses for its NJF_STANDARDS_SIGNING_KEY
// — a fresh random key every run is fine here, this only needs to agree with whatever value
// JugglingLicenceDecisionAutomationSeeder signs the automation's own webhook trigger with, both
// read this exact same config key at runtime.
builder.Configuration["JUGGLING_LICENCE_SIGNING_KEY"] ??=
    Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));

var runtimeLayout = TestSiteRuntimeLayout.Apply(builder);

if (runtimeLayout.IsEnabled)
{
    Console.WriteLine(
        $"PRISM TESTSITE: Using isolated runtime root '{runtimeLayout.RuntimeRoot}' " +
        $"(db: '{runtimeLayout.DatabasePath}', reset: {runtimeLayout.WasReset}).");
}

builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddComposers()
    .Build();

WebApplication app = builder.Build();

// SECURITY: KEYCLOAK_BACKCHANNEL_URL must never be set in production — it bypasses
// TLS certificate validation for OIDC metadata fetches, which is only acceptable
// in controlled development environments. Fail loudly if misconfigured.
if (!app.Environment.IsDevelopment() && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KEYCLOAK_BACKCHANNEL_URL")))
{
    throw new InvalidOperationException("KEYCLOAK_BACKCHANNEL_URL must not be set in non-Development environments.");
}

// In GitHub Codespaces, the browser accesses the app via a public URL like
// https://{token}-44345.{region}.app.github.dev, but Codespaces does not forward that
// hostname in the Host header — Kestrel sees localhost:44345 instead. Override Request.Host
// so the OIDC middleware generates the correct redirect_uri for the Codespace domain.
//
// Derivation priority:
//   1. TESTSITE_PUBLIC_URL (preferred) — set by AppHost via `gh codespace ports`, works
//      with both legacy and new regional Codespaces URL schemes.
//   2. Inbound request Host header — used when TESTSITE_PUBLIC_URL is not set (e.g. local
//      Aspire dev without AppHost, or when running TestSite standalone).
var testSitePublicUrl = Environment.GetEnvironmentVariable("TESTSITE_PUBLIC_URL");
if (testSitePublicUrl is not null)
{
    var publicHost = new HostString(new Uri(testSitePublicUrl).Host);
    app.Use(async (context, next) =>
    {
        if (context.Request.IsHttps)
            context.Request.Host = publicHost;
        await next();
    });
}

await app.BootUmbracoAsync();

// Lightweight health check — responds immediately once Kestrel starts (which only
// happens after BootUmbracoAsync returns). Used by the startup status page probe
// on http://localhost:9250/api/health to reliably detect when the site is ready.
app.Use(async (context, next) =>
{
    if (context.Request.Path == "/api/health")
    {
        context.Response.StatusCode = 200;
        context.Response.ContentType = "text/plain";
        await context.Response.WriteAsync("ok");
        return;
    }
    await next();
});

// Money Modeller is deliberately reachable anonymously on the web (see CLAUDE.md's declarative-
// calculations section — a calculated field's defaultFrom falls back to its plain default when
// member data doesn't resolve, "e.g. an anonymous visitor with no member data" — an intentional
// existing capability, not a gap). On the mobile app that same fallback just reads as broken:
// there's no page furniture explaining why the numbers are generic placeholders, and the whole
// point of showcasing it there is the personalised, signed-in experience. So mobile requests get
// bounced to login first; the web page keeps its existing anonymous-preview behaviour untouched.
//
// Gated on IsPrismMobileRequest (the broad query/cookie/header/UA detection Master.cshtml's own
// isPrismMobileRequest already uses), not the strict UA-only IsNativeMobileRequest — the desktop
// mobile-UA demo toggle exists specifically so implementers can preview real mobile behaviour
// from a browser, and a login gate that only fired inside the compiled app would silently not be
// part of that preview.
//
// Authenticates against "PrismMemberCookie" directly (the same scheme AccountController/
// PrismNotificationController use) rather than reading context.User — this middleware runs
// before app.UseUmbraco()'s own authentication middleware populates it, but
// HttpContext.AuthenticateAsync(scheme) invokes the registered handler directly regardless of
// pipeline position, so it doesn't depend on running after that middleware.
app.Use(async (context, next) =>
{
    if (HttpMethods.IsGet(context.Request.Method)
        && context.Request.Path.StartsWithSegments(TestSiteSeedContract.MoneyModellerPageUrl, StringComparison.OrdinalIgnoreCase)
        && PrismMobileRequestDetection.IsPrismMobileRequest(context))
    {
        var authResult = await context.AuthenticateAsync("PrismMemberCookie");
        if (!authResult.Succeeded)
        {
            var returnUrl = context.Request.Path + context.Request.QueryString;
            context.Response.Redirect("/auth/login?returnUrl=" + Uri.EscapeDataString(returnUrl));
            return;
        }
    }
    await next();
});


app.UseUmbraco()
    .WithMiddleware(u =>
    {
        u.UseBackOffice();
        u.UseWebsite();
    })
    .WithEndpoints(u =>
    {
        u.UseBackOfficeEndpoints();
        u.UseWebsiteEndpoints();
    });

// Resolves the juggling-licence-decision support-system invocation once the Automate automation
// (JugglingLicenceDecisionAutomationSeeder) calls back — same route shape as
// Wayfinder.Umbraco.ReferenceApp's own Program.cs. Mapped directly on `app`, outside the Umbraco
// endpoint groups above: only UseAuthorization() enforcement is pipeline-order-sensitive here, and
// this endpoint is explicitly anonymous (the shared secret, not cookie/OIDC auth, is its guard).
app.MapWebhookSupportSystemCallbacks(
        () => app.Services.GetRequiredService<Wayfinder.Umbraco.Services.UmbracoProcessManagerEngine>(),
        sharedSecret: builder.Configuration["JUGGLING_LICENCE_CALLBACK_SECRET"])
    .AllowAnonymous();

await app.RunAsync();

// Exposed so UmbracoPrism.Core.IntegrationTests can boot this exact host with
// WebApplicationFactory<Program> for the authorization-contract behavioural suite (Layer 2).
public partial class Program;
