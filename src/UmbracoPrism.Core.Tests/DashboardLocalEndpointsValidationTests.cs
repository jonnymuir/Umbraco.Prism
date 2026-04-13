using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Moq;
using UmbracoPrism.Core.Models;
using UmbracoPrism.TestSite.Controllers;

namespace UmbracoPrism.Core.Tests;

public class DashboardLocalEndpointsValidationTests
{
    private static string RepoRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));

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
        program.Should().Contain("AddProject(\"businessapp\", \"../UmbracoPrism.MockBusinessApp/UmbracoPrism.MockBusinessApp.csproj\", launchProfileName: \"https\")");
    }

    private static DownstreamDemoController BuildController(
        HttpMessageHandler handler,
        IDictionary<string, string?> configValues,
        AuthenticationHeaderValue? authHeader,
        bool isDevelopment = true)
    {
        var client = new HttpClient(handler);
        var clientFactory = new Mock<IHttpClientFactory>();
        clientFactory.Setup(factory => factory.CreateClient("prism-downstream-demo")).Returns(client);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        var prismContext = new Mock<IPrismContext>();
        prismContext.Setup(context => context.GetAuthorizationHeaderAsync())
            .ReturnsAsync(authHeader);

        var environment = new Mock<IWebHostEnvironment>();
        environment.Setup(env => env.EnvironmentName)
            .Returns(isDevelopment ? Environments.Development : Environments.Production);

        return new DownstreamDemoController(
            clientFactory.Object, 
            configuration, 
            prismContext.Object,
            environment.Object);
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> factory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(factory(request));
    }
}
