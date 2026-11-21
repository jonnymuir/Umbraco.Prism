using System.Security.Claims;
using Azure;
using Azure.Security.KeyVault.Secrets;
using AwesomeAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Umbraco.Cms.Core.Cache;
using UmbracoPrism.Core.Extensions;
using UmbracoPrism.Core.Middleware;
using UmbracoPrism.Core.Models;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Tests;

public class AuthHardeningRestTests
{
    // ------------------------------------------------------------ AUTH-18: unknown hostnames

    [Theory]
    [InlineData(true, StatusCodes.Status421MisdirectedRequest, false)]
    [InlineData(false, StatusCodes.Status200OK, true)]
    public async Task AnUnknownHostname_IsAnswered421_OnlyWhenTheHostIsTenantOnly(bool rejectUnknownHosts, int expectedStatus, bool nextCalled)
    {
        var tenantService = new Mock<ITenantService>();
        tenantService.Setup(s => s.GetByDomainAsync(It.IsAny<string>())).ReturnsAsync((PrismTenant?)null);
        var called = false;
        var middleware = new PrismTenantMiddleware(_ => { called = true; return Task.CompletedTask; }, NullLogger<PrismTenantMiddleware>.Instance);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Host = new HostString("unknown.example");

        await middleware.InvokeAsync(
            httpContext,
            tenantService.Object,
            Mock.Of<IPrismContext>(),
            Mock.Of<IPrismSigningKeyCache>(),
            Options.Create(new PrismConfiguration { RejectUnknownHosts = rejectUnknownHosts }));

        httpContext.Response.StatusCode.Should().Be(expectedStatus);
        called.Should().Be(nextCalled);
    }

    // ------------------------------------------------------------ AUTH-09: stable subject

    [Fact]
    public void AnEntraPrincipal_IsIdentifiedByTenantAndObjectId_NotByEmail()
    {
        var principal = Principal(("tid", "tenant-1"), ("oid", "object-1"), ("preferred_username", "a@example.com"));

        principal.GetSubject().Should().Be("tenant-1:object-1");
    }

    [Fact]
    public void ARenamedEntraAccount_KeepsItsSubject()
    {
        var before = Principal(("tid", "tenant-1"), ("oid", "object-1"), ("preferred_username", "old@example.com"));
        var after = Principal(("tid", "tenant-1"), ("oid", "object-1"), ("preferred_username", "new@example.com"));

        after.GetSubject().Should().Be(before.GetSubject());
    }

    [Fact]
    public void AGenericProviderPrincipal_IsIdentifiedByIssuerAndSub()
    {
        var principal = Principal(("iss", "https://idp.example/realms/a/"), ("sub", "user-7"));

        principal.GetSubject().Should().Be("https://idp.example/realms/a|user-7");
    }

    [Fact]
    public void TheSameSubValue_FromTwoProviders_IsTwoSubjects() =>
        Principal(("iss", "https://idp-a.example"), ("sub", "1")).GetSubject()
            .Should().NotBe(Principal(("iss", "https://idp-b.example"), ("sub", "1")).GetSubject());

    [Fact]
    public void ANamelessPrincipal_HasNoSubject() =>
        Principal(("preferred_username", "a@example.com")).GetSubject().Should().BeNull();

    // ------------------------------------------------------------ AUTH-15: single-use mobile code

    [Fact]
    public async Task AMobileCode_IsRedeemedOnce_EvenWhenRedeemedConcurrently()
    {
        var codes = new PrismMobileSignInCodes(
            new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())),
            new EphemeralDataProtectionProvider());
        var ticket = new AuthenticationTicket(
            new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "x")], "test")),
            new AuthenticationProperties(),
            "PrismMemberCookie");
        var code = await codes.IssueAsync(new PrismMobileSignInCodes.PendingSignIn(1, "challenge", ticket), CancellationToken.None);

        var results = await Task.WhenAll(Enumerable.Range(0, 25).Select(_ => Task.Run(() => codes.TakeAsync(code, CancellationToken.None))));

        results.Count(r => r is not null).Should().Be(1);
    }

    // ------------------------------------------------------------ AUTH-14: vault failures are not cached

    [Fact]
    public async Task ATransientVaultFailure_IsNotServedFromCache_OnceTheBackoffPasses()
    {
        var clock = new MutableClock();
        var client = new Mock<SecretClient>();
        client.SetupSequence(c => c.GetSecretAsync("tenant-secret", It.IsAny<string?>(), It.IsAny<SecretContentType?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(503, "unavailable"))
            .ReturnsAsync(Response.FromValue(SecretModelFactory.KeyVaultSecret(new SecretProperties("tenant-secret"), "the-secret"), Mock.Of<Response>()));
        var service = CreateVault(client.Object, clock);

        (await service.GetSecretAsync("tenant-secret")).Should().BeEmpty();

        clock.Advance(SecretVaultService.FailureBackoff + TimeSpan.FromSeconds(1));
        (await service.GetSecretAsync("tenant-secret")).Should().Be("the-secret", "a transient failure must not stick for the cache lifetime");
    }

    [Fact]
    public async Task AFailedVaultLookup_IsNotRetriedOnEveryRequest_WithinTheBackoff()
    {
        var client = new Mock<SecretClient>();
        client.Setup(c => c.GetSecretAsync("tenant-secret", It.IsAny<string?>(), It.IsAny<SecretContentType?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(503, "unavailable"));
        var service = CreateVault(client.Object, new MutableClock());

        for (var i = 0; i < 10; i++) await service.GetSecretAsync("tenant-secret");

        client.Verify(c => c.GetSecretAsync("tenant-secret", It.IsAny<string?>(), It.IsAny<SecretContentType?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ASuccessfulVaultLookup_IsCached()
    {
        var client = new Mock<SecretClient>();
        client.Setup(c => c.GetSecretAsync("tenant-secret", It.IsAny<string?>(), It.IsAny<SecretContentType?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(SecretModelFactory.KeyVaultSecret(new SecretProperties("tenant-secret"), "the-secret"), Mock.Of<Response>()));
        var service = CreateVault(client.Object, new MutableClock());

        await service.GetSecretAsync("tenant-secret");
        await service.GetSecretAsync("tenant-secret");

        client.Verify(c => c.GetSecretAsync("tenant-secret", It.IsAny<string?>(), It.IsAny<SecretContentType?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static SecretVaultService CreateVault(SecretClient client, TimeProvider clock) =>
        new(
            client,
            new ConfigurationBuilder().Build(),
            new AppCaches(new ObjectCacheAppCache(), Mock.Of<IRequestCache>(), new IsolatedCaches(_ => new ObjectCacheAppCache())),
            NullLogger<SecretVaultService>.Instance,
            clock);

    private static ClaimsPrincipal Principal(params (string Type, string Value)[] claims) =>
        new(new ClaimsIdentity(claims.Select(c => new Claim(c.Type, c.Value)), "test"));

    private sealed class MutableClock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }
}
