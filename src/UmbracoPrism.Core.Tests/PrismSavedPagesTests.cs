using System.IO.Compression;
using System.Security.Claims;
using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using UmbracoPrism.Core.Controllers.Models;
using UmbracoPrism.Core.Controllers.SavedPages;
using UmbracoPrism.Core.Extensions;
using UmbracoPrism.Core.Middleware;
using UmbracoPrism.Core.Persistence;
using UmbracoPrism.Core.Services;
using UmbracoPrism.Core.Services.MobileBundles;

namespace UmbracoPrism.Core.Tests;

/// <summary>
/// Saved pages (Design/mobile-offline-pages.md): an app built with them says so in its user
/// agent, the site labels each page it renders for the app with a content version, and the
/// service worker is served from the site root.
/// </summary>
public class PrismSavedPagesTests
{
    private const string SavedPagesApp = "Mozilla/5.0 (iPhone) PrismMobile PrismNativeTabs PrismSystemSignIn PrismSavedPages";
    private const string AndroidWebView = "Mozilla/5.0 (Linux; Android 14; wv) Chrome/113.0";

    [Fact]
    public async Task ABundleBuiltWithSavedPages_SaysSoInItsUserAgent_AndKeepsIosToItsOwnDomains()
    {
        var files = await BuildAsync(new PrismMobileBundleRequest { SavedPagesEnabled = true }, new PrismTenantSchema { Hostname = "contoso.example" });

        files["capacitor.config.ts"].Should().Contain("PrismNativeTabs PrismSystemSignIn PrismSavedPages");
        files["capacitor.config.ts"].Should().Contain("limitsNavigationsToAppBoundDomains: true");
        files["scripts/bootstrap-ios.sh"].Should().Contain("WKAppBoundDomains")
            .And.Contain("for domain in contoso.example; do");
    }

    [Fact]
    public async Task ABundleBuiltWithoutSavedPages_LeavesIosNavigationAlone()
    {
        var files = await BuildAsync(new PrismMobileBundleRequest(), new PrismTenantSchema { Hostname = "contoso.example" });

        files["capacitor.config.ts"].Should().NotContain("PrismSavedPages");
        files["capacitor.config.ts"].Should().NotContain("limitsNavigationsToAppBoundDomains");
        files["scripts/bootstrap-ios.sh"].Should().NotContain("WKAppBoundDomains");
    }

    [Fact]
    public void TheAppsOwnDomains_AreTheSiteAndTheIdentityProvidersSignInHosts_WithoutPortsOrRepeats()
    {
        var tenant = new PrismTenantSchema
        {
            Name = "Contoso",
            Hostname = "contoso.example",
            OidcAuthority = "https://login.contoso.example:8443/realms/members",
            EntraTenantId = "0000-1111"
        };
        var settings = MobileBundleSettings.Resolve(tenant, new PrismMobileBundleRequest { StartUrl = "https://Contoso.example:44345/" });

        IosProjectFiles.AppBoundDomains(settings).Should().Equal(
            "contoso.example",
            "login.contoso.example",
            "login.microsoftonline.com",
            "0000-1111.ciamlogin.com",
            "0000-1111.b2clogin.com");
    }

    [Fact]
    public void AnAppsWorker_PassesTheAppsUserAgentOn_SoItsRequestsAreRecognisedAsTheApps()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.UserAgent = AndroidWebView;
        context.Request.Headers[PrismMobileRequestDetection.AppUserAgentHeaderName] = SavedPagesApp;

        PrismMobileRequestDetection.SavesPages(context).Should().BeTrue();
        PrismMobileRequestDetection.HasNativeTabs(context).Should().BeTrue();
        PrismMobileRequestDetection.IsNativeMobileRequest(context).Should().BeTrue();
    }

    [Fact]
    public void ABrowser_DoesNotSavePages()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.UserAgent = AndroidWebView;

        PrismMobileRequestDetection.SavesPages(context).Should().BeFalse();
    }

    [Theory]
    [InlineData(false, "visitor")]
    [InlineData(true, "member")]
    public async Task PagesForAnAppWithSavedPages_AreLabelledWithTheContentVersionAndAudience(bool signedIn, string audience)
    {
        var contentVersion = new PrismContentVersion();
        var context = ContextWithResponseStart(SavedPagesApp, signedIn, out var start);

        await new PrismSavedPagesMiddleware(_ => Task.CompletedTask, contentVersion).InvokeAsync(context);
        await start();

        context.Response.Headers[PrismSavedPagesMiddleware.HeaderName].ToString()
            .Should().Be($"{contentVersion.Current}.{audience}");
    }

    [Fact]
    public async Task PagesForABrowser_AreNotLabelled()
    {
        var context = ContextWithResponseStart(AndroidWebView, signedIn: false, out var start);

        await new PrismSavedPagesMiddleware(_ => Task.CompletedTask, new PrismContentVersion()).InvokeAsync(context);
        await start();

        context.Response.Headers.ContainsKey(PrismSavedPagesMiddleware.HeaderName).Should().BeFalse();
    }

    [Fact]
    public void PublishingContent_MovesTheContentVersionOn()
    {
        var contentVersion = new PrismContentVersion();
        var before = contentVersion.Current;

        contentVersion.Advance();

        contentVersion.Current.Should().NotBe(before);
    }

    [Fact]
    public void TheWorker_IsServedFromTheSiteRoot_AndRecheckedOnEveryRegistration()
    {
        var controller = new PrismSavedPagesController { ControllerContext = new() { HttpContext = new DefaultHttpContext() } };

        var result = controller.ServiceWorker().Should().BeOfType<FileContentResult>().Subject;

        result.ContentType.Should().Be("text/javascript");
        System.Text.Encoding.UTF8.GetString(result.FileContents).Should().Contain("self.addEventListener('fetch'");
        controller.Response.Headers.CacheControl.ToString().Should().Be("no-cache");
    }

    private static async Task<Dictionary<string, string>> BuildAsync(PrismMobileBundleRequest request, PrismTenantSchema tenant)
    {
        tenant.Name = "Contoso";
        var bytes = await new MobileBundleService().BuildBundleAsync(tenant, request);
        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        return archive.Entries.ToDictionary(entry => entry.FullName, entry =>
        {
            using var reader = new StreamReader(entry.Open());
            return reader.ReadToEnd();
        });
    }

    /// <summary>A request whose response start can be triggered, which runs the OnStarting callbacks.</summary>
    private static DefaultHttpContext ContextWithResponseStart(string userAgent, bool signedIn, out Func<Task> start)
    {
        var feature = new StartableResponseFeature();
        var context = new DefaultHttpContext();
        context.Features.Set<IHttpResponseFeature>(feature);
        context.Request.Headers.UserAgent = userAgent;
        if (signedIn)
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "member")], "test"));
        }

        start = feature.StartAsync;
        return context;
    }

    private sealed class StartableResponseFeature : HttpResponseFeature
    {
        private readonly List<(Func<object, Task> Callback, object State)> _starting = [];

        public override void OnStarting(Func<object, Task> callback, object state) => _starting.Add((callback, state));

        public async Task StartAsync()
        {
            foreach (var (callback, state) in _starting)
            {
                await callback(state);
            }
        }
    }
}
