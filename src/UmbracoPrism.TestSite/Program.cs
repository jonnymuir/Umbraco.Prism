using Microsoft.AspNetCore.Authorization;
using Umbraco.AI.Extensions;
using Umbraco.Cms.Core;
using UmbracoPrism.Core.Extensions;
using UmbracoPrism.TestSite;
using Wayfinder.Engine.Http;
using Wayfinder.Engine.Mcp;
using Wayfinder.Umbraco;
using Wayfinder.Umbraco.Extensions;
using Wayfinder.Umbraco.Mcp;

// Where an AI design partner connects to author blueprints over MCP, signing in through the
// backoffice. Settings, Advanced, Blueprints, "Connect a design partner" shows the setup.
const string McpEndpointPath = "/wayfinder/service-blueprint-authoring/mcp";

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
builder.Configuration["BUTTERFLY_IDENTIFICATION_SIGNING_KEY"] ??=
    Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));

var runtimeLayout = TestSiteRuntimeLayout.Apply(builder);

if (runtimeLayout.IsEnabled)
{
    Console.WriteLine(
        $"PRISM TESTSITE: Using isolated runtime root '{runtimeLayout.RuntimeRoot}' " +
        $"(db: '{runtimeLayout.DatabasePath}', reset: {runtimeLayout.WasReset}).");
}

builder.Services.AddServiceBlueprintAuthoringMcp();

var umbraco = builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddComposers()
    .AddWayfinderUmbracoMcpAuthentication();

// WORKAROUND, not a pattern to copy. The butterfly identification runs on Gemini's free tier,
// where a model such as gemini-flash-latest can take 100 to 200 seconds to answer when it is busy.
// Umbraco.AI's Google provider gives the Google SDK no timeout, so .NET's default of 100 seconds
// cuts those answers off. This swaps in the same provider with a longer timeout. A paid Gemini
// tier answers in seconds and would not need it; remove this once Umbraco.AI.Google lets a
// connection set its own timeout.
umbraco.AIProviders()
    .Exclude<Umbraco.AI.Google.GoogleProvider>()
    .Add<UmbracoPrism.TestSite.FieldRecording.PatientGoogleProvider>();

umbraco.Build();

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

// Outermost, so it sees the MCP endpoint's 401 and adds the hint an MCP client follows to sign in.
app.UseWayfinderUmbracoMcpAuthChallenge(McpEndpointPath);

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

// Money Modeller's own login requirement (previously a page-specific gate here) is now data-
// driven: IPrismPageAccessResolver + PrismPageAccessFilter (UmbracoPrism.Core), enforced
// generically for any content node carrying a Prism page-access policy — see
// WayfinderServicePageSeeder for the seeded policy row backing this page specifically.
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

// Backoffice users with BlueprintsAdmin only. The schemes are explicit because a minimal API
// mapped outside Umbraco's Management API would otherwise challenge the site's default scheme.
app.MapServiceBlueprintAuthoringMcp(McpEndpointPath).RequireAuthorization(new AuthorizeAttribute
{
    Policy = WayfinderUmbracoAuthorizationPolicies.BlueprintsAdmin,
    AuthenticationSchemes = $"{Constants.Security.BackOfficeAuthenticationType},OpenIddict.Validation.AspNetCore",
});
app.MapWayfinderUmbracoMcpOAuthDiscovery(McpEndpointPath);

// The juggling-licence-decision automation runs on this same host and resolves its support-system
// outcome in-process (ResolveSupportSystemOutcomeAction), so nothing here needs the HTTP callback
// route. It is mapped only when a callback secret is configured (a receiver outside this process
// calling back with X-Webhook-Secret): outside Development the route refuses to exist without one,
// and an unauthenticated callback route is never worth having. Mapped directly on `app`, outside
// the Umbraco endpoint groups: only UseAuthorization() enforcement is pipeline-order-sensitive
// here, and this endpoint is explicitly anonymous (the shared secret is its guard, not
// cookie/OIDC auth).
if (builder.Configuration["JUGGLING_LICENCE_CALLBACK_SECRET"] is { Length: > 0 } callbackSecret)
{
    app.MapWebhookSupportSystemCallbacks(
            () => app.Services.GetRequiredService<Wayfinder.Umbraco.Services.UmbracoProcessManagerEngine>(),
            sharedSecret: callbackSecret)
        .AllowAnonymous();
}

await app.RunAsync();

// Exposed so UmbracoPrism.Core.IntegrationTests can boot this exact host with
// WebApplicationFactory<Program> for the authorization-contract behavioural suite (Layer 2).
public partial class Program;
