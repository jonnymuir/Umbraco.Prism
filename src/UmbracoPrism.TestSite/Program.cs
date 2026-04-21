using UmbracoPrism.TestSite;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
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
// https://{name}-44345.app.github.dev, but Codespaces does not forward that hostname
// in the Host header — Kestrel sees localhost:44345 instead. Override Request.Host so
// the OIDC middleware generates the correct redirect_uri for the Codespace domain.
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

await app.RunAsync();
