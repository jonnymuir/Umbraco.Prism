using System.Net;
using AwesomeAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace UmbracoPrism.Core.Tests;

/// <summary>
/// Boots the real Mock Business App as a deployed environment would run it and checks the properties
/// an implementor copying it relies on: nothing is reachable without a valid token, and nothing can be
/// added open by accident. These are written against the whole route table, not a list of routes, so a
/// route added later is covered without anyone remembering to add a test.
/// </summary>
// The app refuses to start in a non-Development environment while KEYCLOAK_BACKCHANNEL_URL is set, and other
// tests set it process-wide, so this must not run alongside them.
[Collection(EnvVarSensitiveTestCollection.Name)]
public sealed class MockBusinessAppHostSecurityTests : IClassFixture<MockBusinessAppHostSecurityTests.ProductionHost>
{
    public sealed class ProductionHost : WebApplicationFactory<MockBusinessAppEntryPoint>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Production");

        public HttpClient HttpsClient() => CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });
    }

    private readonly ProductionHost _host;

    public MockBusinessAppHostSecurityTests(ProductionHost host) => _host = host;

    private IReadOnlyList<RouteEndpoint> Routes() =>
        _host.Services.GetServices<EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .ToList();

    [Fact]
    public void TheHostExposesRoutes_SoTheChecksBelowAreNotVacuous()
    {
        Routes().Should().NotBeEmpty();
    }

    [Fact]
    public void NoRouteIsAnonymous_InADeployedEnvironment()
    {
        var anonymous = Routes()
            .Where(route => route.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .Select(route => route.RoutePattern.RawText);

        anonymous.Should().BeEmpty(
            "anonymous access has to be an explicit, reviewed exception, and the only one (the Development diagnostics route) is not mapped here");
    }

    [Fact]
    public async Task ARouteWithNoPolicyOfItsOwn_StillDemandsAnAuthenticatedCaller()
    {
        var fallback = await _host.Services.GetRequiredService<IAuthorizationPolicyProvider>().GetFallbackPolicyAsync();

        fallback.Should().NotBeNull("deny by default means a forgotten RequireAuthorization() fails closed");
        fallback!.Requirements.Should().ContainSingle(r => r is Microsoft.AspNetCore.Authorization.Infrastructure.DenyAnonymousAuthorizationRequirement);
    }

    [Fact]
    public async Task EveryRoute_RefusesACallerWithNoToken()
    {
        using var client = _host.HttpsClient();

        foreach (var route in Routes())
        {
            var methods = route.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? ["GET"];
            var path = Concrete(route.RoutePattern);

            foreach (var method in methods)
            {
                using var request = new HttpRequestMessage(new HttpMethod(method), path);
                using var response = await client.SendAsync(request);

                response.StatusCode.Should().Be(HttpStatusCode.Unauthorized, $"{method} {route.RoutePattern.RawText} must not answer without a valid token");
            }
        }
    }

    [Fact]
    public async Task EveryRoute_RefusesAForgedToken()
    {
        // alg=none with the right issuer and audience: validation, not a lookup, has to reject it.
        var forged = "eyJhbGciOiJub25lIiwidHlwIjoiSldUIn0."
            + Convert.ToBase64String("{\"iss\":\"https://localhost:8443/realms/prism-dev\",\"aud\":\"prism-business-app\",\"preferred_username\":\"demo@prism.local\",\"exp\":4102444800}"u8.ToArray())
                .TrimEnd('=').Replace('+', '-').Replace('/', '_')
            + ".";
        using var client = _host.HttpsClient();

        foreach (var route in Routes())
        {
            var method = route.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods.First() ?? "GET";
            using var request = new HttpRequestMessage(new HttpMethod(method), Concrete(route.RoutePattern));
            request.Headers.Authorization = new("Bearer", forged);
            using var response = await client.SendAsync(request);

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized, route.RoutePattern.RawText);
        }
    }

    [Fact]
    public async Task APlainHttpRequest_IsRefusedOutsideDevelopment_NotRedirected()
    {
        // A token sent over HTTP is already exposed, so redirecting to HTTPS would be too late.
        using var client = _host.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://localhost"),
            AllowAutoRedirect = false,
        });

        using var response = await client.GetAsync("/api/backoffice/me");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Headers.Location.Should().BeNull();
    }

    [Fact]
    public async Task TheDevelopmentDiagnosticsRoute_IsNotMappedAndNotServed_InADeployedEnvironment()
    {
        Routes().Select(route => route.RoutePattern.RawText).Should().NotContain("/debug/auth");

        using var client = _host.HttpsClient();
        using var response = await client.GetAsync("/debug/auth");

        // An unmatched path gets the deny-by-default 401 rather than a 404, so it also reveals nothing about which paths exist.
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ResponsesAreNotCacheableAndNotSniffable()
    {
        using var client = _host.HttpsClient();

        using var response = await client.GetAsync("/api/backoffice/me");

        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        response.Headers.GetValues("X-Content-Type-Options").Should().Contain("nosniff");
    }

    // A request path for a route pattern: every {parameter} becomes a placeholder segment.
    private static string Concrete(RoutePattern pattern) =>
        "/" + string.Join('/', pattern.PathSegments.Select(segment =>
            string.Concat(segment.Parts.Select(part => part switch
            {
                RoutePatternLiteralPart literal => literal.Content,
                _ => "x",
            }))));
}
