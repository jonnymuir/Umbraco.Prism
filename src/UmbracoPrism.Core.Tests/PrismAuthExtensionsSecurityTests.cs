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
            ["PrismBackOffice:Tenants:0:EntraTenantId"] = "tenant-a",
            ["PrismBackOffice:Tenants:0:ClientId"] = "client-a",
            ["PrismBackOffice:Tenants:0:Code"] = "ta",
            ["PrismBackOffice:Tenants:0:DisplayName"] = "Tenant A"
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
            ["PrismBackOffice:Tenants:0:EntraTenantId"] = "tenant-a",
            ["PrismBackOffice:Tenants:0:ClientId"] = "client-a",
            ["PrismBackOffice:Tenants:0:Code"] = "ta",
            ["PrismBackOffice:Tenants:0:DisplayName"] = "Tenant A",
            ["PrismBackOffice:Tenants:1:EntraTenantId"] = "tenant-b",
            ["PrismBackOffice:Tenants:1:ClientId"] = "client-b",
            ["PrismBackOffice:Tenants:1:Code"] = "tb",
            ["PrismBackOffice:Tenants:1:DisplayName"] = "Tenant B"
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
            ["PrismBackOffice:Tenants:0:EntraTenantId"] = "tenant-a",
            ["PrismBackOffice:Tenants:0:ClientId"] = "client-a",
            ["PrismBackOffice:Tenants:0:Code"] = "ta",
            ["PrismBackOffice:Tenants:0:DisplayName"] = "Tenant A",
            ["PrismBackOffice:Tenants:1:EntraTenantId"] = "tenant-b",
            ["PrismBackOffice:Tenants:1:ClientId"] = "client-b",
            ["PrismBackOffice:Tenants:1:Code"] = "tb",
            ["PrismBackOffice:Tenants:1:DisplayName"] = "Tenant B"
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

}
