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
