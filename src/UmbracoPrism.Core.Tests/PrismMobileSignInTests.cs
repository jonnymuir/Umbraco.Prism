using System.Security.Claims;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Moq;
using UmbracoPrism.Core.Auth;
using UmbracoPrism.Core.Controllers;
using UmbracoPrism.Core.Controllers.Models;
using UmbracoPrism.Core.Models;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Tests;

public class PrismMobileSignInTests
{
    private const string Verifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";
    private const string Fingerprint = "36:49:A1:FF:BB:53:3D:30:53:25:29:D0:27:8B:CE:C0:90:66:8A:DD:02:BF:50:B2:26:49:D6:E0:89:8D:B9:A9";

    private static readonly string Challenge = PrismMobileSignInCodes.ChallengeFor(Verifier);

    private static PrismTenant Tenant(int id = 1, PrismMobileAppLinks? appLinks = null) => new()
    {
        Id = id,
        Hostname = "app.example.com",
        OidcAuthority = "https://id.example.com/realms/app",
        OidcClientId = "app",
        MobileAppLinks = appLinks ?? new PrismMobileAppLinks("ABCDE12345.com.example.app", "com.example.app", [Fingerprint])
    };

    private static ClaimsPrincipal Member() =>
        new(new ClaimsIdentity([new Claim("sub", "member-1"), new Claim("name", "Demo Member")], PrismAuthenticationSchemes.MemberCookie));

    private sealed class TestPrismContext : IPrismContext
    {
        public PrismTenant? CurrentTenant { get; set; }
        public string? LastAuthorizationFailureReason => null;

        public Task<System.Net.Http.Headers.AuthenticationHeaderValue?> GetAuthorizationHeaderAsync(bool forceRefresh = false) =>
            Task.FromResult<System.Net.Http.Headers.AuthenticationHeaderValue?>(null);
    }

    private sealed class RecordingAuthenticationService(ClaimsPrincipal? member) : IAuthenticationService
    {
        public List<string?> SignedOut { get; } = [];
        public (ClaimsPrincipal Principal, AuthenticationProperties? Properties)? SignedIn { get; private set; }

        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme)
        {
            if (member is null)
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var properties = new AuthenticationProperties();
            properties.StoreTokens([new AuthenticationToken { Name = "refresh_token", Value = "provider-refresh-token" }]);
            properties.IssuedUtc = DateTimeOffset.UtcNow.AddHours(-1);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(member, properties, scheme!)));
        }

        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;

        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;

        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties)
        {
            SignedIn = (principal, properties);
            return Task.CompletedTask;
        }

        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
        {
            SignedOut.Add(scheme);
            return Task.CompletedTask;
        }
    }

    private sealed class Harness
    {
        public PrismMobileSignInCodes Codes { get; } = new(
            new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())),
            new EphemeralDataProtectionProvider());

        public PrismMobileSignInController Controller(PrismTenant? tenant, RecordingAuthenticationService? authentication = null) =>
            new(new TestPrismContext { CurrentTenant = tenant }, Codes, NullLogger<PrismMobileSignInController>.Instance)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        RequestServices = new ServiceCollection()
                            .AddSingleton<IAuthenticationService>(authentication ?? new RecordingAuthenticationService(null))
                            .BuildServiceProvider()
                    }
                }
            };

        /// <summary>Runs complete for a member signed in at the provider and returns the code it handed to the app.</summary>
        public async Task<string> CompleteAsync(PrismTenant tenant)
        {
            var result = await Controller(tenant, new RecordingAuthenticationService(Member()))
                .Complete(Challenge, CancellationToken.None);
            var location = result.Should().BeOfType<RedirectResult>().Subject.Url;
            return QueryHelpersCode(location);
        }

        private static string QueryHelpersCode(string location) =>
            Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(new Uri("https://x" + location).Query)["code"]!;
    }

    [Fact]
    public void Start_ChallengesTheProvider_AndReturnsToCompleteWithTheChallenge()
    {
        var result = new Harness().Controller(Tenant()).Start(Challenge, "create");

        var challenge = result.Should().BeOfType<ChallengeResult>().Subject;
        challenge.AuthenticationSchemes.Should().Equal(PrismAuthenticationSchemes.Oidc);
        challenge.Properties!.RedirectUri.Should().Be($"/umbraco/prism/mobile/auth/complete?challenge={Challenge}");
        challenge.Properties.Items["PrismPrompt"].Should().Be("create");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("too-short")]
    [InlineData("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjX=")]
    public void Start_RefusesAMissingOrMalformedChallenge(string? challenge)
    {
        new Harness().Controller(Tenant()).Start(challenge, null).Should().BeOfType<BadRequestResult>();
    }

    [Fact]
    public void Start_IsNotFound_UntilTheSiteVouchesForAnApp()
    {
        new Harness().Controller(Tenant(appLinks: PrismMobileAppLinks.None)).Start(Challenge, null)
            .Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Complete_HandsTheSessionToTheCallback_AndSignsOutOfTheSystemSession()
    {
        var harness = new Harness();
        var authentication = new RecordingAuthenticationService(Member());

        var result = await harness.Controller(Tenant(), authentication).Complete(Challenge, CancellationToken.None);

        result.Should().BeOfType<RedirectResult>().Subject.Url.Should().MatchRegex("^/umbraco/prism/mobile/auth/callback\\?code=[A-Za-z0-9_-]{43}$");
        authentication.SignedOut.Should().Equal(PrismAuthenticationSchemes.MemberCookie);
    }

    [Fact]
    public async Task Complete_RefusesWhenNobodySignedIn()
    {
        var result = await new Harness().Controller(Tenant()).Complete(Challenge, CancellationToken.None);

        result.Should().BeOfType<BadRequestResult>();
    }

    [Fact]
    public void Callback_OpenedOutsideTheApp_TellsTheMemberToReturn_AndIsNeverCachedOrIndexed()
    {
        var controller = new Harness().Controller(Tenant());

        var result = controller.Callback();

        result.Should().BeOfType<ContentResult>().Subject.Content.Should().Contain("Return to the app");
        var headers = controller.Response.Headers;
        headers.CacheControl.ToString().Should().Be("no-store");
        headers["Referrer-Policy"].ToString().Should().Be("no-referrer");
        headers["X-Robots-Tag"].ToString().Should().Be("noindex");
    }

    [Fact]
    public async Task Redeem_WithTheVerifier_SignsTheMemberInWithTheProviderSession()
    {
        var harness = new Harness();
        var code = await harness.CompleteAsync(Tenant());
        var authentication = new RecordingAuthenticationService(null);

        var result = await harness.Controller(Tenant(), authentication).Redeem(code, Verifier, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        var (principal, properties) = authentication.SignedIn!.Value;
        principal.FindFirst("sub")!.Value.Should().Be("member-1");
        principal.HasClaim("prism_auth_method", "system").Should().BeTrue();
        properties!.GetTokenValue("refresh_token").Should().Be("provider-refresh-token");
        properties.IssuedUtc.Should().BeNull("the webview session starts now, not when the provider signed the member in");
    }

    [Fact]
    public async Task Redeem_WithTheWrongVerifier_SignsNobodyIn_AndUsesUpTheCode()
    {
        var harness = new Harness();
        var code = await harness.CompleteAsync(Tenant());
        var wrong = new RecordingAuthenticationService(null);
        var right = new RecordingAuthenticationService(null);

        (await harness.Controller(Tenant(), wrong).Redeem(code, Verifier.Replace('d', 'e'), CancellationToken.None))
            .Should().BeOfType<BadRequestResult>();
        await harness.Controller(Tenant(), right).Redeem(code, Verifier, CancellationToken.None);

        wrong.SignedIn.Should().BeNull();
        right.SignedIn.Should().BeNull("a code is single use, even after a failed attempt");
    }

    [Fact]
    public async Task Redeem_SameCodeTwice_SignsInOnlyOnce()
    {
        var harness = new Harness();
        var code = await harness.CompleteAsync(Tenant());
        var first = new RecordingAuthenticationService(null);
        var second = new RecordingAuthenticationService(null);

        await harness.Controller(Tenant(), first).Redeem(code, Verifier, CancellationToken.None);
        await harness.Controller(Tenant(), second).Redeem(code, Verifier, CancellationToken.None);

        first.SignedIn.Should().NotBeNull();
        second.SignedIn.Should().BeNull();
    }

    [Fact]
    public async Task Redeem_AnotherTenantsCode_SignsNobodyIn()
    {
        var harness = new Harness();
        var code = await harness.CompleteAsync(Tenant(id: 1));
        var authentication = new RecordingAuthenticationService(null);

        await harness.Controller(Tenant(id: 2), authentication).Redeem(code, Verifier, CancellationToken.None);

        authentication.SignedIn.Should().BeNull();
    }

    [Fact]
    public void Codes_ExpireAfterTwoMinutes()
    {
        PrismMobileSignInCodes.Lifetime.Should().Be(TimeSpan.FromMinutes(2));
    }

    [Fact]
    public void ChallengeFor_MatchesTheRfc7636Example()
    {
        PrismMobileSignInCodes.ChallengeFor(Verifier).Should().Be("E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM");
    }

    [Fact]
    public void AppleAppSiteAssociation_VouchesForTheTenantsIosApp_ForSignInCallbacks()
    {
        var result = new PrismAppLinksController(new TestPrismContext { CurrentTenant = Tenant() }).AppleAppSiteAssociation();

        var json = JsonSerializer.Serialize(result.Should().BeOfType<JsonResult>().Subject.Value);
        json.Should().Be("""{"webcredentials":{"apps":["ABCDE12345.com.example.app"]}}""");
    }

    [Fact]
    public void AssetLinks_VouchesForTheTenantsAndroidApp_ByPackageAndCertificate()
    {
        var result = new PrismAppLinksController(new TestPrismContext { CurrentTenant = Tenant() }).AssetLinks();

        var statement = JsonDocument.Parse(JsonSerializer.Serialize(result.Should().BeOfType<JsonResult>().Subject.Value)).RootElement[0];
        statement.GetProperty("relation")[0].GetString().Should().Be("delegate_permission/common.handle_all_urls");
        var target = statement.GetProperty("target");
        target.GetProperty("namespace").GetString().Should().Be("android_app");
        target.GetProperty("package_name").GetString().Should().Be("com.example.app");
        target.GetProperty("sha256_cert_fingerprints")[0].GetString().Should().Be(Fingerprint);
    }

    [Fact]
    public void AppLinkFiles_AreNotFound_ForATenantWithoutApps()
    {
        var controller = new PrismAppLinksController(new TestPrismContext { CurrentTenant = Tenant(appLinks: PrismMobileAppLinks.None) });

        controller.AppleAppSiteAssociation().Should().BeOfType<NotFoundResult>();
        controller.AssetLinks().Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public void AppLinks_FromSavedConfig_PrefixesTheTeam_DefaultsThePackageToTheAppId_AndNormalisesFingerprints()
    {
        var appLinks = PrismMobileAppLinks.FromConfigJson(JsonSerializer.Serialize(new PrismMobileAppConfig
        {
            AppId = "com.example.app",
            AppleTeamId = "ABCDE12345",
            AndroidCertificateFingerprints = [Fingerprint.ToLowerInvariant(), " ", "not-a-fingerprint"]
        }));

        appLinks.AppleAppId.Should().Be("ABCDE12345.com.example.app");
        appLinks.AndroidPackageName.Should().Be("com.example.app");
        appLinks.AndroidCertificateFingerprints.Should().Equal(Fingerprint);
    }

    [Fact]
    public void AppLinks_WithoutATeamOrFingerprint_VouchForNothingOnThatPlatform()
    {
        var appLinks = PrismMobileAppLinks.FromConfigJson("""{"AppId":"com.example.app"}""");

        appLinks.HasIos.Should().BeFalse();
        appLinks.HasAndroid.Should().BeFalse();
    }

    [Theory]
    [InlineData("abc", null, null, "Apple team ID")]
    [InlineData(null, "com.example-app.x", null, "Android package name")]
    [InlineData(null, null, "A8:EE:10:C3:1B:71:C6:D0:05:9F:95:99:26:54:DB:26:AD:03:93:E4", "SHA-256")]
    public void AppLinks_Validate_ExplainsWhatIsWrong(string? teamId, string? package, string? fingerprint, string expected)
    {
        var error = PrismMobileAppLinks.Validate(new PrismMobileAppConfig
        {
            AppleTeamId = teamId,
            AndroidPackageName = package,
            AndroidCertificateFingerprints = fingerprint is null ? null : [fingerprint]
        });

        error.Should().Contain(expected);
    }

    [Fact]
    public void AppLinks_Validate_AcceptsAWellFormedConfig()
    {
        PrismMobileAppLinks.Validate(new PrismMobileAppConfig
        {
            AppleTeamId = "ABCDE12345",
            AndroidPackageName = "com.example.app",
            AndroidCertificateFingerprints = [Fingerprint]
        }).Should().BeNull();
    }

    [Theory]
    [InlineData("/dashboard", true)]
    [InlineData("/auth/login", false)]
    public async Task AnAppThatSignsInNatively_IsSentToTheSignInPage_RatherThanTheProvider(string path, bool redirected)
    {
        var tenant = Tenant();
        var services = new ServiceCollection()
            .AddSingleton<IPrismContext>(new TestPrismContext { CurrentTenant = tenant })
            .BuildServiceProvider();
        var options = new OpenIdConnectOptions();
        options.Events.OnRedirectToIdentityProvider = _ => Task.CompletedTask;
        new PrismOidcConfiguration(
                new HttpContextAccessor { HttpContext = new DefaultHttpContext { RequestServices = services } },
                Mock.Of<IPrismSigningKeyCache>(),
                NullLogger<PrismOidcConfiguration>.Instance)
            .PostConfigure(PrismAuthenticationSchemes.Oidc, options);

        var httpContext = new DefaultHttpContext { RequestServices = services };
        httpContext.Request.Path = path;
        httpContext.Request.Headers.UserAgent = "Mozilla/5.0 PrismMobile PrismNativeTabs PrismSystemSignIn";
        var context = new RedirectContext(
            httpContext,
            new AuthenticationScheme(PrismAuthenticationSchemes.Oidc, null, typeof(OpenIdConnectHandler)),
            options,
            new AuthenticationProperties { RedirectUri = "/dashboard?tab=2" })
        {
            ProtocolMessage = new OpenIdConnectMessage()
        };

        await options.Events.OnRedirectToIdentityProvider(context);

        context.Handled.Should().Be(redirected);
        if (redirected)
        {
            httpContext.Response.Headers.Location.ToString().Should().Be("/auth/login?returnUrl=%2Fdashboard%3Ftab%3D2");
        }
        else
        {
            context.ProtocolMessage.IssuerAddress.Should().StartWith(tenant.OidcAuthority);
        }
    }

    private static readonly PrismTenant AppSigningInTenant = new()
    {
        Hostname = "app.example.com",
        OidcAuthority = "https://id.example.com/realms/app",
        OidcClientId = "app",
        AllowBiometricLogin = true,
        MobileAppLinks = new PrismMobileAppLinks("ABCDE12345.com.example.app", null, [])
    };

    private static AccountController AccountControllerFor(string userAgent)
    {
        var controller = new AccountController(NullLogger<AccountController>.Instance, new TestPrismContext { CurrentTenant = AppSigningInTenant })
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    RequestServices = new ServiceCollection()
                        .AddSingleton<IAuthenticationService>(new RecordingAuthenticationService(null))
                        .AddLogging()
                        .AddDataProtection().Services
                        .AddAntiforgery()
                        .BuildServiceProvider()
                }
            }
        };
        controller.HttpContext.Request.Headers.UserAgent = userAgent;
        return controller;
    }

    private const string IosAppUserAgent = "Mozilla/5.0 (iPhone; CPU iPhone OS 26_0 like Mac OS X) PrismMobile PrismNativeTabs PrismSystemSignIn";

    [Fact]
    public async Task Login_FromAnAppTheSiteVouchesFor_ServesTheAppSignInPage_NotTheProvider()
    {
        var controller = AccountControllerFor(IosAppUserAgent);

        var page = (await controller.Login("/dashboard?tab=\"2\"")).Should().BeOfType<ContentResult>().Subject;

        page.ContentType.Should().Be("text/html");
        page.Content.Should().Contain("prism-system-sign-in.js\"");
        page.Content.Should().Contain("data-prism-antiforgery-header=\"RequestVerificationToken\"");
        page.Content.Should().MatchRegex("data-prism-antiforgery-token=\"[^\"]{20,}\"");
        page.Content.Should().Contain("prism-biometric-autologin.js\" data-prism-on-demand");
        page.Content.Should().NotContain("data-prism-prompt");
        controller.Response.Headers.CacheControl.ToString().Should().Contain("no-store");
    }

    [Fact]
    public async Task Register_FromAnAppTheSiteVouchesFor_AsksTheProviderToCreateAnAccount()
    {
        var controller = AccountControllerFor(IosAppUserAgent);

        var page = (await controller.Register("/")).Should().BeOfType<ContentResult>().Subject;

        page.Content.Should().Contain("data-prism-prompt=\"create\"");
    }

    [Theory]
    [InlineData("Mozilla/5.0 (Linux; Android 16) PrismMobile PrismNativeTabs PrismSystemSignIn")]
    [InlineData("Mozilla/5.0 (iPhone) PrismMobile PrismNativeTabs")]
    [InlineData("Mozilla/5.0 (iPhone) Safari")]
    public async Task Login_FromAnyOtherClient_ChallengesTheProviderAsBefore(string userAgent)
    {
        // The tenant vouches only for its iOS app, so the Android app and older or web clients keep
        // the provider's own sign-in.
        var controller = AccountControllerFor(userAgent);

        (await controller.Login("/")).Should().BeOfType<ChallengeResult>();
    }
}
