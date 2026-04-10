using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Moq;
using UmbracoPrism.Core.Extensions;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Tests;

public class PrismAuthExtensionsSecurityTests
{
    [Fact]
    public void IssuerValidator_RejectsIssuerHostMismatch_EvenWhenTenantIdAppearsInPath()
    {
        var options = BuildJwtOptions(new Dictionary<string, string?>
        {
            ["PrismBusinessApp:Tenants:0:EntraTenantId"] = "tenant-a",
            ["PrismBusinessApp:Tenants:0:ClientId"] = "client-a",
            ["PrismBusinessApp:Tenants:0:Code"] = "ta",
            ["PrismBusinessApp:Tenants:0:DisplayName"] = "Tenant A"
        });

        var token = CreateToken("tenant-a");

        var act = () => options.TokenValidationParameters.IssuerValidator!(
            "https://evil.example/tenant-a/v2.0",
            token,
            options.TokenValidationParameters);

        act.Should().Throw<SecurityTokenInvalidIssuerException>();
    }

    [Fact]
    public void AudienceValidator_RejectsAudienceBoundToDifferentConfiguredTenant()
    {
        var options = BuildJwtOptions(new Dictionary<string, string?>
        {
            ["PrismBusinessApp:Tenants:0:EntraTenantId"] = "tenant-a",
            ["PrismBusinessApp:Tenants:0:ClientId"] = "client-a",
            ["PrismBusinessApp:Tenants:0:Code"] = "ta",
            ["PrismBusinessApp:Tenants:0:DisplayName"] = "Tenant A",
            ["PrismBusinessApp:Tenants:1:EntraTenantId"] = "tenant-b",
            ["PrismBusinessApp:Tenants:1:ClientId"] = "client-b",
            ["PrismBusinessApp:Tenants:1:Code"] = "tb",
            ["PrismBusinessApp:Tenants:1:DisplayName"] = "Tenant B"
        });

        var token = CreateToken("tenant-a");

        var accepted = options.TokenValidationParameters.AudienceValidator!(
            ["client-b"],
            token,
            options.TokenValidationParameters);

        accepted.Should().BeFalse();
    }

    [Fact]
    public void AudienceValidator_AcceptsAudienceBoundToSameConfiguredTenant()
    {
        var options = BuildJwtOptions(new Dictionary<string, string?>
        {
            ["PrismBusinessApp:Tenants:0:EntraTenantId"] = "tenant-a",
            ["PrismBusinessApp:Tenants:0:ClientId"] = "client-a",
            ["PrismBusinessApp:Tenants:0:Code"] = "ta",
            ["PrismBusinessApp:Tenants:0:DisplayName"] = "Tenant A",
            ["PrismBusinessApp:Tenants:1:EntraTenantId"] = "tenant-b",
            ["PrismBusinessApp:Tenants:1:ClientId"] = "client-b",
            ["PrismBusinessApp:Tenants:1:Code"] = "tb",
            ["PrismBusinessApp:Tenants:1:DisplayName"] = "Tenant B"
        });

        var token = CreateToken("tenant-a");

        var accepted = options.TokenValidationParameters.AudienceValidator!(
            ["client-a"],
            token,
            options.TokenValidationParameters);

        accepted.Should().BeTrue();
    }

    [Fact]
    public void ResolveSigningKeys_RefreshesMetadata_WhenRequestedKidIsMissingFromCachedConfiguration()
    {
        var existingKey = CreateSigningKey("old-key");
        var cache = new Mock<IPrismSigningKeyCache>();
        cache
            .Setup(c => c.GetSnapshot("tenant-a", "rotated-key"))
            .Returns(new PrismSigningKeyCacheSnapshot([existingKey], true, false, false));

        var keys = PrismAuthExtensions.ResolveSigningKeys(
            "tenant-a",
            "rotated-key",
            [new BackOfficeTenant("tenant-a", "client-a", "ta", "Tenant A")],
            cache.Object)
            .ToArray();

        keys.Should().BeEmpty();
        cache.Verify(c => c.WarmAsync("tenant-a", true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void ResolveSigningKeys_ReturnsEmpty_WhenRequestedKidStillMissingAfterRefresh()
    {
        var cache = new Mock<IPrismSigningKeyCache>();
        cache
            .Setup(c => c.GetSnapshot("tenant-a", "rotated-key"))
            .Returns(new PrismSigningKeyCacheSnapshot([], true, true, false));

        var keys = PrismAuthExtensions.ResolveSigningKeys(
            "tenant-a",
            "rotated-key",
            [new BackOfficeTenant("tenant-a", "client-a", "ta", "Tenant A")],
            cache.Object)
            .ToArray();

        keys.Should().BeEmpty();
        cache.Verify(c => c.WarmAsync("tenant-a", true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void ResolveSigningKeys_TriggersWarmInBackground_WithoutBlockingResolver()
    {
        var warmStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var warmGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cache = new BlockingWarmSigningKeyCache(
            new PrismSigningKeyCacheSnapshot([CreateSigningKey("kid-a")], true, false, true),
            (tenantId, forceRefresh, cancellationToken) =>
            {
                warmStarted.TrySetResult();
                return warmGate.Task;
            });

        var startedAt = DateTimeOffset.UtcNow;
        var keys = PrismAuthExtensions.ResolveSigningKeys(
            "tenant-a",
            "kid-a",
            [new BackOfficeTenant("tenant-a", "client-a", "ta", "Tenant A")],
            cache)
            .ToArray();
        var elapsed = DateTimeOffset.UtcNow - startedAt;

        keys.Should().ContainSingle().Which.KeyId.Should().Be("kid-a");
        warmStarted.Task.IsCompleted.Should().BeTrue();
        elapsed.Should().BeLessThan(TimeSpan.FromMilliseconds(250));

        warmGate.TrySetResult();
    }

    private static JwtBearerOptions BuildJwtOptions(IReadOnlyDictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        var services = new ServiceCollection();
        services.AddPrismAuthentication(configuration);

        var provider = services.BuildServiceProvider();
        var optionsMonitor = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>();
        return optionsMonitor.Get(JwtBearerDefaults.AuthenticationScheme);
    }

    private static JwtSecurityToken CreateToken(string tenantId)
    {
        var claims = new[] { new Claim("tid", tenantId) };
        return new JwtSecurityToken(claims: claims);
    }

    private static SecurityKey CreateSigningKey(string keyId)
    {
        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes("0123456789abcdef0123456789abcdef"))
        {
            KeyId = keyId
        };
    }

    [Fact]
    public void ResolveSigningKeys_PropagatesException_WhenWarmAsyncThrowsDuringColdStart()
    {
        // Arrange — cold cache (IsExpired + no requested key), WarmAsync simulates a network failure
        var cache = new Mock<IPrismSigningKeyCache>();
        cache
            .Setup(c => c.GetSnapshot("tenant-a", "kid-a"))
            .Returns(new PrismSigningKeyCacheSnapshot([], true, true, false));
        cache
            .Setup(c => c.WarmAsync("tenant-a", true, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Network failure"));

        // Act
        var act = () => PrismAuthExtensions.ResolveSigningKeys(
            "tenant-a",
            "kid-a",
            [new BackOfficeTenant("tenant-a", "client-a", "ta", "Tenant A")],
            cache.Object).ToArray();

        // Assert — exception must propagate; signing-key resolution must not return empty silently
        act.Should().Throw<HttpRequestException>().WithMessage("Network failure");
    }

    [Fact]
    public async Task ResolveSigningKeys_DeduplicatesConcurrentColdStartFetches_ForSameTenant()
    {
        // Arrange — gate controls when the underlying fetch completes; warmStarted signals entry
        var warmStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var warmGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var signingKey = CreateSigningKey("kid-a");
        var cache = new ConcurrentWarmSigningKeyCache(
            [signingKey],
            () => { warmStarted.TrySetResult(); return warmGate.Task; });

        var tenants = new[] { new BackOfficeTenant("tenant-a", "client-a", "ta", "Tenant A") };
        const int concurrency = 5;

        // Act — spin up N concurrent cold callers, all blocked on the gate
        var callerTasks = Enumerable.Range(0, concurrency)
            .Select(_ => Task.Run(() =>
                PrismAuthExtensions.ResolveSigningKeys("tenant-a", "kid-a", tenants, cache).ToArray()))
            .ToArray();

        await warmStarted.Task; // deterministic: at least one caller is inside WarmAsync
        warmGate.TrySetResult(); // unblock the fetch

        var results = await Task.WhenAll(callerTasks);

        // Assert — exactly one underlying fetch; every caller received the resolved key
        cache.UnderlyingFetchCount.Should().Be(1);
        foreach (var keys in results)
            keys.Should().ContainSingle().Which.KeyId.Should().Be("kid-a");
    }

    [Fact]
    public void ResolveSigningKeys_MatchesTenantId_CaseInsensitively()
    {
        // Arrange — token tid uses upper-case but configured tenant id is lower-case
        var signingKey = CreateSigningKey("kid-a");
        var cache = new Mock<IPrismSigningKeyCache>();
        cache
            .Setup(c => c.GetSnapshot("TENANT-A", "kid-a"))
            .Returns(new PrismSigningKeyCacheSnapshot([signingKey], false, false, true));

        // Act
        var keys = PrismAuthExtensions.ResolveSigningKeys(
            "TENANT-A",
            "kid-a",
            [new BackOfficeTenant("tenant-a", "client-a", "ta", "Tenant A")],
            cache.Object)
            .ToArray();

        // Assert — tenant is recognised despite casing mismatch; no warm triggered
        keys.Should().ContainSingle().Which.KeyId.Should().Be("kid-a");
        cache.Verify(c => c.WarmAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private sealed class BlockingWarmSigningKeyCache(
        PrismSigningKeyCacheSnapshot snapshot,
        Func<string, bool, CancellationToken, Task> warmAsync) : IPrismSigningKeyCache
    {
        public Task WarmAsync(string entraTenantId, bool forceRefresh = false, CancellationToken cancellationToken = default)
            => warmAsync(entraTenantId, forceRefresh, cancellationToken);

        public PrismSigningKeyCacheSnapshot GetSnapshot(string entraTenantId, string? keyId = null)
            => snapshot;

        public IEnumerable<SecurityKey> GetSigningKeys(string entraTenantId)
            => snapshot.Keys;
    }

    private sealed class ConcurrentWarmSigningKeyCache(
        SecurityKey[] keysAfterWarm,
        Func<Task> warmGate) : IPrismSigningKeyCache
    {
        private readonly SemaphoreSlim _lock = new(1, 1);
        private bool _warmed;

        public int UnderlyingFetchCount { get; private set; }

        public async Task WarmAsync(string entraTenantId, bool forceRefresh = false, CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken);
            try
            {
                if (_warmed) return;
                await warmGate();
                UnderlyingFetchCount++;
                _warmed = true;
            }
            finally
            {
                _lock.Release();
            }
        }

        public PrismSigningKeyCacheSnapshot GetSnapshot(string entraTenantId, string? keyId = null)
        {
            if (!_warmed)
                return new PrismSigningKeyCacheSnapshot([], true, true, false);
            var containsKey = string.IsNullOrWhiteSpace(keyId) || keysAfterWarm.Any(k => k.KeyId == keyId);
            return new PrismSigningKeyCacheSnapshot(keysAfterWarm, false, false, containsKey);
        }

        public IEnumerable<SecurityKey> GetSigningKeys(string entraTenantId) => keysAfterWarm;
    }

}
