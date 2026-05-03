using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.PublishedContent;
using UmbracoPrism.Core.Models;
using UmbracoPrism.TestSite.Controllers;

namespace UmbracoPrism.Core.Tests;

[Collection(EnvVarSensitiveTestCollection.Name)]
public class DashboardLocalEndpointsValidationTests : IDisposable
{
    private readonly string? _savedBusinessAppBackchannelUrl =
        Environment.GetEnvironmentVariable("BUSINESSAPP_BACKCHANNEL_URL");

    private static string RepoRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("BUSINESSAPP_BACKCHANNEL_URL", _savedBusinessAppBackchannelUrl);
    }

    [Fact]
    public async Task DownstreamDemo_UsesConfiguredHttpsBusinessAppUrl_OnSuccess()
    {
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"tenant":"Prism Demo","assignedRole":"Reviewer"}""",
                    Encoding.UTF8,
                    "application/json")
            });

        var controller = BuildController(
            handler,
            new Dictionary<string, string?>
            {
                ["PrismBusinessApp:WorkflowApiBaseUrl"] = "https://localhost:7245"
            },
            authHeader: new AuthenticationHeaderValue("Bearer", "token"),
            isDevelopment: true);

        var result = await controller.Get();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(ok.Value));
        var root = doc.RootElement;

        root.GetProperty("statusCode").GetInt32().Should().Be(200);
        root.GetProperty("statusText").GetString().Should().Be("OK");
        root.GetProperty("url").GetString().Should().Be("https://localhost:7245/api/backoffice/me");
        root.GetProperty("contentType").GetString().Should().Be("application/json");
        root.GetProperty("body").GetString().Should().Contain("\"tenant\": \"Prism Demo\"");
    }

    [Fact]
    public async Task DownstreamDemo_ReturnsFriendlyNetworkError_WhenBusinessAppIsUnavailable()
    {
        var handler = new StubHttpMessageHandler(_ => throw new HttpRequestException("Connection refused"));

        var controller = BuildController(
            handler,
            new Dictionary<string, string?>
            {
                ["PrismBusinessApp:WorkflowApiBaseUrl"] = "https://localhost:7245"
            },
            authHeader: new AuthenticationHeaderValue("Bearer", "token"),
            isDevelopment: true);

        var result = await controller.Get();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(ok.Value));
        var root = doc.RootElement;

        root.GetProperty("statusCode").GetInt32().Should().Be(0);
        root.GetProperty("statusText").GetString().Should().Be("Network Error");
        root.GetProperty("url").GetString().Should().Be("https://localhost:7245/api/backoffice/me");
        root.GetProperty("body").GetString().Should().Contain("Could not reach the service");
        root.GetProperty("body").GetString().Should().Contain("dotnet run --project src/UmbracoPrism.MockBusinessApp");
    }

    [Fact]
    public async Task DownstreamDemo_PrefersBusinessAppBackchannelUrl_WhenConfigured()
    {
        using var backchannel = new TempEnvVar("BUSINESSAPP_BACKCHANNEL_URL", "http://localhost:5163");
        Uri? capturedRequestUri = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            capturedRequestUri = request.RequestUri;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            };
        });

        var controller = BuildController(
            handler,
            new Dictionary<string, string?>
            {
                ["PrismBusinessApp:WorkflowApiBaseUrl"] = "https://codespace-7245.app.github.dev"
            },
            authHeader: new AuthenticationHeaderValue("Bearer", "token"),
            isDevelopment: true);

        var result = await controller.Get();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(ok.Value));
        var root = doc.RootElement;

        capturedRequestUri.Should().Be(new Uri("http://localhost:5163/api/backoffice/me"));
        root.GetProperty("url").GetString().Should().Be("http://localhost:5163/api/backoffice/me");
    }

    [Fact]
    public async Task DownstreamDemo_Blocks_WhenNotInDevelopmentAndNotExplicitlyEnabled()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));

        var controller = BuildController(
            handler,
            new Dictionary<string, string?>
            {
                ["PrismBusinessApp:WorkflowApiBaseUrl"] = "https://localhost:7245"
            },
            authHeader: new AuthenticationHeaderValue("Bearer", "token"),
            isDevelopment: false);

        var result = await controller.Get();

        var statusCode = result.Should().BeOfType<ObjectResult>().Subject;
        statusCode.StatusCode.Should().Be(403);
        var body = JsonSerializer.Serialize(statusCode.Value);
        body.Should().Contain("Downstream demo is disabled in this environment");
    }

    [Fact]
    public async Task DownstreamDemo_AllowsWhenExplicitlyEnabledInProduction()
    {
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            });

        var controller = BuildController(
            handler,
            new Dictionary<string, string?>
            {
                ["PrismBusinessApp:WorkflowApiBaseUrl"] = "https://localhost:7245",
                ["Prism:EnableDownstreamDemo"] = "true"
            },
            authHeader: new AuthenticationHeaderValue("Bearer", "token"),
            isDevelopment: false);

        var result = await controller.Get();

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task DownstreamDemo_BlocksArbitraryUrls_WhenNotInAllowlist()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));

        var controller = BuildController(
            handler,
            new Dictionary<string, string?>
            {
                ["PrismBusinessApp:WorkflowApiBaseUrl"] = "https://localhost:7245"
            },
            authHeader: new AuthenticationHeaderValue("Bearer", "token"),
            isDevelopment: true);

        var result = await controller.Get(url: "https://evil.com/steal-token");

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var body = JsonSerializer.Serialize(badRequest.Value);
        body.Should().Contain("not in the allowlist");
    }

    [Fact]
    public async Task DownstreamDemo_AllowsConfiguredBusinessAppUrl()
    {
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            });

        var controller = BuildController(
            handler,
            new Dictionary<string, string?>
            {
                ["PrismBusinessApp:WorkflowApiBaseUrl"] = "https://localhost:7245"
            },
            authHeader: new AuthenticationHeaderValue("Bearer", "token"),
            isDevelopment: true);

        var result = await controller.Get(url: "https://localhost:7245/api/backoffice/me");

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task DownstreamDemo_AllowsUrlsInConfiguredAllowlist()
    {
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            });

        var controller = BuildController(
            handler,
            new Dictionary<string, string?>
            {
                ["PrismBusinessApp:WorkflowApiBaseUrl"] = "https://localhost:7245",
                ["Prism:DownstreamDemo:AllowedUrls:0"] = "https://staging.example.com"
            },
            authHeader: new AuthenticationHeaderValue("Bearer", "token"),
            isDevelopment: true);

        var result = await controller.Get(url: "https://staging.example.com/api/test");

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public void KeycloakProxy_LaunchSettings_AdvertiseLocalHttpsPort()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            RepoRoot,
            "src",
            "UmbracoPrism.KeycloakProxy",
            "Properties",
            "launchSettings.json")));

        doc.RootElement.GetProperty("profiles")
            .GetProperty("https")
            .GetProperty("applicationUrl")
            .GetString()
            .Should()
            .Be("https://localhost:8443");
    }

    [Fact]
    public void AppHost_PinsProxyAndBusinessAppLaunchProfiles_ForAspireEndpointVisibility()
    {
        var program = File.ReadAllText(Path.Combine(
            RepoRoot,
            "src",
            "UmbracoPrism.AppHost",
            "Program.cs"));

        program.Should().Contain("AddProject(\"keycloak-proxy\", \"../UmbracoPrism.KeycloakProxy/UmbracoPrism.KeycloakProxy.csproj\", launchProfileName: \"https\")");
        program.Should().Contain(".WithEnvironment(\"ReverseProxy__Clusters__keycloak-cluster__Destinations__keycloak__Address\", keycloak.GetEndpoint(\"http\"))");
        program.Should().Contain("AddProject(\"businessapp\", \"../UmbracoPrism.MockBusinessApp/UmbracoPrism.MockBusinessApp.csproj\", launchProfileName: \"https\")");
    }

    [Fact]
    public void AppHost_UsesRealmDiscoveryHealthCheck_ForKeycloakReadiness()
    {
        var program = File.ReadAllText(Path.Combine(
            RepoRoot,
            "src",
            "UmbracoPrism.AppHost",
            "Program.cs"));

        program.Should().Contain(".WithHttpHealthCheck(\"/realms/prism-dev/.well-known/openid-configuration\")");
        program.Should().NotContain(".WithHttpHealthCheck(\"/health/ready\")");
    }

    [Fact]
    public void AppHost_ConfiguresBusinessAppBackchannel_ForCodespacesServerCalls()
    {
        var program = File.ReadAllText(Path.Combine(
            RepoRoot,
            "src",
            "UmbracoPrism.AppHost",
            "Program.cs"));

        program.Should().Contain(".WithEnvironment(\"BUSINESSAPP_BACKCHANNEL_URL\", \"http://localhost:5163\")");
    }

    [Fact]
    public void AppHost_DoesNotReuseBrokenAspireHttpsBackchannelPattern_ForBusinessApp()
    {
        var program = File.ReadAllText(Path.Combine(
            RepoRoot,
            "src",
            "UmbracoPrism.AppHost",
            "Program.cs"));

        program.Should().NotContain(".WithEnvironment(\"BUSINESSAPP_BACKCHANNEL_URL\", businessApp.GetEndpoint(\"https\"))",
            because: "an earlier Codespaces fix tried the Aspire-discovered HTTPS endpoint and regressed the downstream demo");
    }

    [Fact]
    public void MockBusinessApp_LaunchSettings_AdvertiseLocalHttpBackchannelPort()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            RepoRoot,
            "src",
            "UmbracoPrism.MockBusinessApp",
            "Properties",
            "launchSettings.json")));

        var profiles = doc.RootElement.GetProperty("profiles");
        profiles
            .GetProperty("https")
            .GetProperty("applicationUrl")
            .GetString()
            .Should()
            .Be("https://localhost:7245;http://localhost:5163",
                because: "the explicit backchannel URL must match a real MockBusinessApp listener");
        profiles
            .GetProperty("http")
            .GetProperty("applicationUrl")
            .GetString()
            .Should()
            .Be("http://localhost:5163");
    }

    [Fact]
    public async Task DownstreamDemo_SessionContract_ReportsCookieTokens_AndLogoutHintReadiness()
    {
        var authProperties = new AuthenticationProperties();
        authProperties.StoreTokens(
        [
            new AuthenticationToken { Name = "access_token", Value = "access-token" },
            new AuthenticationToken { Name = "refresh_token", Value = "refresh-token" },
            new AuthenticationToken { Name = "id_token", Value = "id-token" },
            new AuthenticationToken { Name = "expires_at", Value = DateTimeOffset.UtcNow.AddMinutes(10).ToString("o") }
        ]);

        var authTicket = new AuthenticationTicket(
            new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity("PrismMemberCookie")),
            authProperties,
            "PrismMemberCookie");

        var controller = BuildController(
            new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)),
            new Dictionary<string, string?>
            {
                ["PrismBusinessApp:WorkflowApiBaseUrl"] = "https://localhost:7245"
            },
            authHeader: new AuthenticationHeaderValue("Bearer", "access-token"),
            isDevelopment: true,
            authResult: AuthenticateResult.Success(authTicket),
            tenant: new PrismTenant
            {
                Hostname = "localhost",
                OidcAuthority = "https://localhost:8443/realms/prism-dev",
                OidcClientId = "prism-client"
            });

        var result = await controller.GetSessionContract();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(ok.Value));
        var root = doc.RootElement;

        root.GetProperty("tenant").GetProperty("mode").GetString().Should().Be("generic-oidc");
        root.GetProperty("cookie").GetProperty("isAuthenticated").GetBoolean().Should().BeTrue();
        root.GetProperty("cookie").GetProperty("hasAccessToken").GetBoolean().Should().BeTrue();
        root.GetProperty("cookie").GetProperty("hasRefreshToken").GetBoolean().Should().BeTrue();
        root.GetProperty("cookie").GetProperty("hasIdToken").GetBoolean().Should().BeTrue();
        root.GetProperty("downstream").GetProperty("authorizationHeaderReady").GetBoolean().Should().BeTrue();
        root.GetProperty("logout").GetProperty("idTokenHintReady").GetBoolean().Should().BeTrue();
        root.GetProperty("logout").GetProperty("endSessionEndpoint").GetString()
            .Should().Be("https://localhost:8443/realms/prism-dev/protocol/openid-connect/logout");
    }

    [Fact]
    public async Task DownstreamDemo_SessionContract_RemainsObservable_WhenUserIsSignedOut()
    {
        var controller = BuildController(
            new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)),
            new Dictionary<string, string?>
            {
                ["PrismBusinessApp:WorkflowApiBaseUrl"] = "https://localhost:7245"
            },
            authHeader: null,
            isDevelopment: true,
            authResult: AuthenticateResult.NoResult(),
            tenant: new PrismTenant
            {
                Hostname = "localhost",
                OidcAuthority = "https://localhost:8443/realms/prism-dev",
                OidcClientId = "prism-client"
            });

        var result = await controller.GetSessionContract();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(ok.Value));
        var root = doc.RootElement;

        root.GetProperty("cookie").GetProperty("isAuthenticated").GetBoolean().Should().BeFalse();
        root.GetProperty("downstream").GetProperty("authorizationHeaderReady").GetBoolean().Should().BeFalse();
        root.GetProperty("logout").GetProperty("idTokenHintReady").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task DownstreamDemo_ReturnsError_WhenResponseIsHtml()
    {
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "<html><body>Connecting to the forwarded port...</body></html>",
                    Encoding.UTF8,
                    "text/html")
            });

        var controller = BuildController(
            handler,
            new Dictionary<string, string?>
            {
                ["PrismBusinessApp:WorkflowApiBaseUrl"] = "https://localhost:7245"
            },
            authHeader: new AuthenticationHeaderValue("Bearer", "token"),
            isDevelopment: true);

        var result = await controller.Get();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(ok.Value));
        var root = doc.RootElement;

        root.GetProperty("statusCode").GetInt32().Should().Be(0);
        root.GetProperty("statusText").GetString().Should().Be("Invalid Response");
        root.GetProperty("body").GetString().Should().Contain(
            "Expected JSON but received text/html",
            because: "HTML responses from port-forwarding pages must be detected and surfaced as errors");
        root.GetProperty("diagnosticBody").GetString().Should().Be(
            "<html><body>Connecting to the forwarded port...</body></html>",
            because: "the raw HTML should be available as inert diagnostic text for troubleshooting");
        root.GetProperty("diagnosticBodyTruncated").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task DownstreamDemo_DetectsCodespacesPortForwardingPage()
    {
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "<!DOCTYPE html><html><body><h1>Connecting to forwarded port...</h1></body></html>",
                    Encoding.UTF8,
                    "text/html")
            });

        var controller = BuildController(
            handler,
            new Dictionary<string, string?>
            {
                ["PrismBusinessApp:WorkflowApiBaseUrl"] = "https://codespace-7245.uks1.app.github.dev"
            },
            authHeader: new AuthenticationHeaderValue("Bearer", "token"),
            isDevelopment: true);

        var result = await controller.Get();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(ok.Value));
        var root = doc.RootElement;

        root.GetProperty("statusCode").GetInt32().Should().Be(0);
        root.GetProperty("statusText").GetString().Should().Be("Invalid Response");
        root.GetProperty("body").GetString().Should().Contain(
            "GitHub Codespaces port-forwarding proxy",
            because: "Codespaces HTML tunnel pages should be surfaced as proxy/visibility issues, not JSON API bugs");
        root.GetProperty("diagnosticBody").GetString().Should().Contain("<!DOCTYPE html>",
            because: "the HTML diagnostic payload should be preserved as plain text");
    }

    [Fact]
    public async Task DownstreamDemo_RejectsNonJsonContentType()
    {
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "Service temporarily unavailable",
                    Encoding.UTF8,
                    "text/plain")
            });

        var controller = BuildController(
            handler,
            new Dictionary<string, string?>
            {
                ["PrismBusinessApp:WorkflowApiBaseUrl"] = "https://localhost:7245"
            },
            authHeader: new AuthenticationHeaderValue("Bearer", "token"),
            isDevelopment: true);

        var result = await controller.Get();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(ok.Value));
        var root = doc.RootElement;

        root.GetProperty("statusCode").GetInt32().Should().Be(0);
        root.GetProperty("statusText").GetString().Should().Be("Invalid Response");
        root.GetProperty("body").GetString().Should().Contain(
            "Expected JSON but received text/plain",
            because: "non-JSON responses must be detected as errors");
        root.GetProperty("diagnosticBody").GetString().Should().Be("Service temporarily unavailable");
        root.GetProperty("diagnosticBodyTruncated").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task DownstreamDemo_TruncatesLargeDiagnosticBodies()
    {
        var oversizedBody = "<html>" + new string('x', 5000) + "</html>";
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(oversizedBody, Encoding.UTF8, "text/html")
            });

        var controller = BuildController(
            handler,
            new Dictionary<string, string?>
            {
                ["PrismBusinessApp:WorkflowApiBaseUrl"] = "https://localhost:7245"
            },
            authHeader: new AuthenticationHeaderValue("Bearer", "token"),
            isDevelopment: true);

        var result = await controller.Get();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(ok.Value));
        var root = doc.RootElement;

        var diagnosticBody = root.GetProperty("diagnosticBody").GetString();
        diagnosticBody.Should().NotBeNull();
        diagnosticBody!.Length.Should().BeLessThan(4200,
            because: "diagnostic payloads should be capped to avoid dumping unbounded HTML into the dashboard");
        diagnosticBody.Should().EndWith("[Response body truncated for display.]");
        root.GetProperty("diagnosticBodyTruncated").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public void MemberDashboard_RendersDiagnosticBodies_AsTextOnly()
    {
        var viewPath = Path.Combine(RepoRoot, "src", "UmbracoPrism.TestSite", "Views", "memberDashboard.cshtml");
        var content = File.ReadAllText(viewPath);

        content.Should().Contain("payload.diagnosticBody",
            because: "the dashboard should prefer the raw diagnostic body when the downstream API returns non-JSON content");
        content.Should().Contain("elements.body.textContent = model.body;",
            because: "diagnostic HTML must be rendered as inert text, not live markup");
        content.Should().NotContain("elements.body.innerHTML",
            because: "rendering returned HTML as markup would create an XSS sink in the dashboard");
    }

    private static DownstreamDemoController BuildController(
        HttpMessageHandler handler,
        IDictionary<string, string?> configValues,
        AuthenticationHeaderValue? authHeader,
        bool isDevelopment = true,
        AuthenticateResult? authResult = null,
        PrismTenant? tenant = null)
    {
        var client = new HttpClient(handler);
        var clientFactory = new Mock<IHttpClientFactory>();
        clientFactory.Setup(factory => factory.CreateClient("prism-downstream-demo")).Returns(client);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        var prismContext = new Mock<IPrismContext>();
        prismContext.SetupProperty(context => context.CurrentTenant, tenant);
        prismContext.Setup(context => context.GetAuthorizationHeaderAsync(It.IsAny<bool>()))
            .ReturnsAsync(authHeader);

        var environment = new Mock<IWebHostEnvironment>();
        environment.Setup(env => env.EnvironmentName)
            .Returns(isDevelopment ? Environments.Development : Environments.Production);
        var publishedContentQuery = new Mock<IPublishedContentQuery>();
        publishedContentQuery.Setup(query => query.ContentAtRoot())
            .Returns(Array.Empty<IPublishedContent>());

        var controller = new DownstreamDemoController(
            clientFactory.Object, 
            configuration, 
            prismContext.Object,
            publishedContentQuery.Object,
            environment.Object);

        var services = new ServiceCollection()
            .AddSingleton<IAuthenticationService>(new TestAuthenticationService(authResult ?? AuthenticateResult.NoResult()))
            .BuildServiceProvider();

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                RequestServices = services
            }
        };

        return controller;
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> factory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(factory(request));
    }

    private sealed class TempEnvVar : IDisposable
    {
        private readonly string _name;
        private readonly string? _previousValue;

        public TempEnvVar(string name, string? value)
        {
            _name = name;
            _previousValue = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(_name, _previousValue);
        }
    }

    private sealed class TestAuthenticationService(AuthenticateResult result) : IAuthenticationService
    {
        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme)
            => Task.FromResult(result);

        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
            => Task.CompletedTask;

        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
            => Task.CompletedTask;

        public Task SignInAsync(HttpContext context, string? scheme, System.Security.Claims.ClaimsPrincipal principal, AuthenticationProperties? properties)
            => Task.CompletedTask;

        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
            => Task.CompletedTask;
    }
}
