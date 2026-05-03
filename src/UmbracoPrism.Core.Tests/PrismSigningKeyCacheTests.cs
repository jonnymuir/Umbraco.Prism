using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Moq;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Tests;

[Collection(EnvVarSensitiveTestCollection.Name)]
public class PrismSigningKeyCacheTests
{
    [Fact]
    public async Task GetSnapshot_SeparatesRefreshWindowFromHardExpiry()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 3, 28, 12, 0, 0, TimeSpan.Zero));
        var httpClientFactory = new Mock<IHttpClientFactory>();
        var configuration = CreateConfiguration("key-a");
        var cache = new PrismSigningKeyCache(httpClientFactory.Object, clock, (_, _, _) => new StubConfigurationManager(configuration));

        await cache.WarmAsync("tenant-a");

        var fresh = cache.GetSnapshot("tenant-a", "key-a");
        fresh.ShouldRefresh.Should().BeFalse();
        fresh.IsExpired.Should().BeFalse();
        fresh.ContainsRequestedKey.Should().BeTrue();

        clock.Advance(PrismSigningKeyCache.RefreshAfter + TimeSpan.FromMinutes(1));

        var refreshDue = cache.GetSnapshot("tenant-a", "key-a");
        refreshDue.ShouldRefresh.Should().BeTrue();
        refreshDue.IsExpired.Should().BeFalse();

        clock.Advance(PrismSigningKeyCache.HardExpiry - PrismSigningKeyCache.RefreshAfter);

        var expired = cache.GetSnapshot("tenant-a", "key-a");
        expired.ShouldRefresh.Should().BeTrue();
        expired.IsExpired.Should().BeTrue();
    }

    [Fact]
    public async Task WarmAsync_DeduplicatesConcurrentRefreshes_PerTenant()
    {
        var httpClientFactory = new Mock<IHttpClientFactory>();
        var calls = 0;
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cache = new PrismSigningKeyCache(
            httpClientFactory.Object,
            TimeProvider.System,
            (_, _, _) => new DelegateConfigurationManager(async cancel =>
            {
                Interlocked.Increment(ref calls);
                await release.Task.WaitAsync(cancel);
                return CreateConfiguration("shared-key");
            }));

        var warmTasks = Enumerable.Range(0, 8)
            .Select(_ => cache.WarmAsync("tenant-a", forceRefresh: true))
            .ToArray();

        await Task.Delay(50);
        release.TrySetResult();
        await Task.WhenAll(warmTasks);

        calls.Should().Be(1);
        cache.GetSnapshot("tenant-a", "shared-key").ContainsRequestedKey.Should().BeTrue();
    }

    [Fact]
    public async Task WarmAsync_KeepsTenantEntriesIsolated()
    {
        var httpClientFactory = new Mock<IHttpClientFactory>();
        var cache = new PrismSigningKeyCache(
            httpClientFactory.Object,
            TimeProvider.System,
            (_, metadataAddress, _) =>
            {
                var tenantId = metadataAddress.Split('/')[3];
                return new StubConfigurationManager(CreateConfiguration($"{tenantId}-key"));
            });

        await cache.WarmAsync("tenant-a", forceRefresh: true);
        await cache.WarmAsync("tenant-b", forceRefresh: true);

        cache.GetSnapshot("tenant-a", "tenant-a-key").ContainsRequestedKey.Should().BeTrue();
        cache.GetSnapshot("tenant-a", "tenant-b-key").ContainsRequestedKey.Should().BeFalse();
        cache.GetSnapshot("tenant-b", "tenant-b-key").ContainsRequestedKey.Should().BeTrue();
    }

    [Fact]
    public async Task WarmAsync_ThrottlesRepeatedForcedRefreshes_WithinCooldown()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 3, 28, 12, 0, 0, TimeSpan.Zero));
        var httpClientFactory = new Mock<IHttpClientFactory>();
        var calls = 0;
        var cache = new PrismSigningKeyCache(
            httpClientFactory.Object,
            clock,
            (_, _, _) => new DelegateConfigurationManager(_ =>
            {
                Interlocked.Increment(ref calls);
                return Task.FromResult(CreateConfiguration("shared-key"));
            }));

        await cache.WarmAsync("tenant-a", forceRefresh: true);
        await cache.WarmAsync("tenant-a", forceRefresh: true);

        calls.Should().Be(1, because: "forced refreshes inside the cooldown should be dropped");

        clock.Advance(PrismSigningKeyCache.ForcedRefreshCooldown + TimeSpan.FromSeconds(1));

        await cache.WarmAsync("tenant-a", forceRefresh: true);

        calls.Should().Be(2, because: "forced refresh should run again after cooldown");
    }

    [Fact]
    public async Task WarmAsync_AppliesForcedRefreshCooldown_PerTenant()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 3, 28, 12, 0, 0, TimeSpan.Zero));
        var httpClientFactory = new Mock<IHttpClientFactory>();
        var callsByTenant = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        var cache = new PrismSigningKeyCache(
            httpClientFactory.Object,
            clock,
            (_, metadataAddress, _) =>
            {
                var tenantId = metadataAddress.Split('/')[3];
                callsByTenant.TryGetValue(tenantId, out var callCount);
                callsByTenant[tenantId] = callCount + 1;
                return new StubConfigurationManager(CreateConfiguration($"{tenantId}-key"));
            });

        await cache.WarmAsync("tenant-a", forceRefresh: true);
        await cache.WarmAsync("tenant-a", forceRefresh: true);
        await cache.WarmAsync("tenant-b", forceRefresh: true);
        await cache.WarmAsync("tenant-b", forceRefresh: true);

        callsByTenant["tenant-a"].Should().Be(1);
        callsByTenant["tenant-b"].Should().Be(1);

        clock.Advance(PrismSigningKeyCache.ForcedRefreshCooldown + TimeSpan.FromSeconds(1));

        await cache.WarmAsync("tenant-a", forceRefresh: true);
        await cache.WarmAsync("tenant-b", forceRefresh: true);

        callsByTenant["tenant-a"].Should().Be(2);
        callsByTenant["tenant-b"].Should().Be(2);
    }

    [Fact]
    public async Task WarmAsync_WithMetadataAddress_UsesThatAddressDirectly()
    {
        var httpClientFactory = new Mock<IHttpClientFactory>();
        string? capturedMetadataAddress = null;
        bool? capturedRequireHttps = null;
        var cache = new PrismSigningKeyCache(
            httpClientFactory.Object,
            TimeProvider.System,
            (_, metadataAddress, requireHttps) =>
            {
                capturedMetadataAddress = metadataAddress;
                capturedRequireHttps = requireHttps;
                return new StubConfigurationManager(CreateConfiguration("oidc-key"));
            });

        await cache.WarmAsync(
            "http://localhost:8080/realms/prism-dev",
            "http://localhost:8080/realms/prism-dev/.well-known/openid-configuration");

        capturedMetadataAddress.Should().Be("http://localhost:8080/realms/prism-dev/.well-known/openid-configuration");
        capturedRequireHttps.Should().BeFalse("HTTP metadata URL should disable RequireHttps");
        cache.GetSnapshot("http://localhost:8080/realms/prism-dev", "oidc-key").ContainsRequestedKey.Should().BeTrue();
    }

    [Fact]
    public async Task WarmAsync_WithMetadataAddress_RequiresHttps_ForHttpsUrl()
    {
        var httpClientFactory = new Mock<IHttpClientFactory>();
        bool? capturedRequireHttps = null;
        var cache = new PrismSigningKeyCache(
            httpClientFactory.Object,
            TimeProvider.System,
            (_, _, requireHttps) =>
            {
                capturedRequireHttps = requireHttps;
                return new StubConfigurationManager(CreateConfiguration("key"));
            });

        await cache.WarmAsync(
            "https://example.com/realms/demo",
            "https://example.com/realms/demo/.well-known/openid-configuration");

        capturedRequireHttps.Should().BeTrue("HTTPS metadata URL should keep RequireHttps enabled");
    }

    [Fact]
    public async Task WarmAsync_RewritesTransitiveJwksFetch_WhenDiscoveryReturnsPublicHostWithBackchannelPort()
    {
        const string publicAuthority = "https://codespace-8443.app.github.dev/realms/prism-dev";
        const string backchannelBase = "http://localhost:39517";
        const string metadataAddress = "http://localhost:39517/realms/prism-dev/.well-known/openid-configuration";
        const string malformedJwksAddress = "http://codespace-8443.app.github.dev:39517/realms/prism-dev/protocol/openid-connect/certs";
        const string expectedBackchannelJwksAddress = "http://localhost:39517/realms/prism-dev/protocol/openid-connect/certs";

        using var envDev = new TempEnvVar("ASPNETCORE_ENVIRONMENT", "Development");
        using var envBc = new TempEnvVar("KEYCLOAK_BACKCHANNEL_URL", backchannelBase);

        var requestedUris = new List<string>();
        var handler = new StubHttpMessageHandler(request =>
        {
            requestedUris.Add(request.RequestUri!.AbsoluteUri);

            if (string.Equals(request.RequestUri.AbsoluteUri, metadataAddress, StringComparison.OrdinalIgnoreCase))
            {
                var discoveryJson = JsonSerializer.Serialize(new
                {
                    issuer = publicAuthority,
                    jwks_uri = malformedJwksAddress
                });

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(discoveryJson, Encoding.UTF8, "application/json")
                };
            }

            if (string.Equals(request.RequestUri.AbsoluteUri, expectedBackchannelJwksAddress, StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(BuildJwksJson("kid-1"), Encoding.UTF8, "application/json")
                };
            }

            throw new InvalidOperationException($"Unexpected metadata request: {request.RequestUri}");
        });

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory
            .Setup(factory => factory.CreateClient("prism-oidc-metadata"))
            .Returns(new HttpClient(handler));

        var cache = new PrismSigningKeyCache(httpClientFactory.Object);

        await cache.WarmAsync(publicAuthority, metadataAddress, forceRefresh: true, requiredKeyId: "kid-1");

        requestedUris.Should().ContainInOrder(metadataAddress, expectedBackchannelJwksAddress);
        requestedUris.Should().NotContain(malformedJwksAddress,
            "the transitive jwks_uri fetch must be rewritten to the internal backchannel host");
        cache.GetSnapshot(publicAuthority, "kid-1").ContainsRequestedKey.Should().BeTrue();
    }

    private static OpenIdConnectConfiguration CreateConfiguration(string keyId)
    {
        var configuration = new OpenIdConnectConfiguration();
        configuration.SigningKeys.Add(new SymmetricSecurityKey(Encoding.UTF8.GetBytes("0123456789abcdef0123456789abcdef"))
        {
            KeyId = keyId
        });
        return configuration;
    }

    private static string BuildJwksJson(string keyId)
    {
        using var rsa = RSA.Create(2048);
        var parameters = rsa.ExportParameters(includePrivateParameters: false);

        var jsonWebKey = new JsonWebKey
        {
            Kty = "RSA",
            Kid = keyId,
            Use = "sig",
            Alg = SecurityAlgorithms.RsaSha256,
            N = Base64UrlEncoder.Encode(parameters.Modulus),
            E = Base64UrlEncoder.Encode(parameters.Exponent)
        };

        return JsonSerializer.Serialize(new
        {
            keys = new[] { jsonWebKey }
        });
    }

    private sealed class StubConfigurationManager(OpenIdConnectConfiguration configuration) : IConfigurationManager<OpenIdConnectConfiguration>
    {
        public Task<OpenIdConnectConfiguration> GetConfigurationAsync(CancellationToken cancel) =>
            Task.FromResult(configuration);

        public void RequestRefresh()
        {
        }
    }

    private sealed class DelegateConfigurationManager(
        Func<CancellationToken, Task<OpenIdConnectConfiguration>> callback) : IConfigurationManager<OpenIdConnectConfiguration>
    {
        public Task<OpenIdConnectConfiguration> GetConfigurationAsync(CancellationToken cancel) => callback(cancel);

        public void RequestRefresh()
        {
        }
    }

    private sealed class FakeTimeProvider(DateTimeOffset initialUtcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = initialUtcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan by) => _utcNow = _utcNow.Add(by);
    }

    private sealed class TempEnvVar : IDisposable
    {
        private readonly string _name;
        private readonly string? _original;

        public TempEnvVar(string name, string? value)
        {
            _name = name;
            _original = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        public void Dispose() => Environment.SetEnvironmentVariable(_name, _original);
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> factory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(factory(request));
    }
}
