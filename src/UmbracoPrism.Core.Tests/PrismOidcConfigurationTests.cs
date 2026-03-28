using FluentAssertions;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Moq;
using UmbracoPrism.Core.Auth;
using UmbracoPrism.Core.Models;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Tests;

public class PrismOidcConfigurationTests
{
    [Fact]
    public async Task PostConfigure_TriggersBackgroundRefreshWithoutBlocking_WhenKidIsMissingFromCache()
    {
        var refreshStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var refreshRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var signingKeyCache = new Mock<IPrismSigningKeyCache>();

        signingKeyCache
            .Setup(cache => cache.GetSnapshot("tenant-a", "new-key"))
            .Returns(new PrismSigningKeyCacheSnapshot([CreateSigningKey("old-key")], true, false, false));

        signingKeyCache
            .Setup(cache => cache.WarmAsync("tenant-a", true, It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                refreshStarted.TrySetResult();
                await refreshRelease.Task;
            });

        var options = ConfigureOptions(signingKeyCache.Object, new PrismTenant
        {
            EntraTenantId = "tenant-a",
            EntraClientId = "client-a"
        });

        var resolvedKeys = options.TokenValidationParameters.IssuerSigningKeyResolver!(
                "token",
                new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(),
                "new-key",
                options.TokenValidationParameters)
            .ToArray();

        resolvedKeys.Should().BeEmpty();
        await refreshStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        signingKeyCache.Verify(cache => cache.WarmAsync("tenant-a", true, It.IsAny<CancellationToken>()), Times.Once);

        refreshRelease.TrySetResult();
    }

    [Fact]
    public async Task PostConfigure_TriggersBackgroundRefreshWithoutBlocking_WhenCacheIsCold()
    {
        var refreshStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var refreshRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var signingKeyCache = new Mock<IPrismSigningKeyCache>();

        signingKeyCache
            .Setup(cache => cache.GetSnapshot("tenant-a", "new-key"))
            .Returns(new PrismSigningKeyCacheSnapshot([], true, true, false));

        signingKeyCache
            .Setup(cache => cache.WarmAsync("tenant-a", true, It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                refreshStarted.TrySetResult();
                await refreshRelease.Task;
            });

        var options = ConfigureOptions(signingKeyCache.Object, new PrismTenant
        {
            EntraTenantId = "tenant-a",
            EntraClientId = "client-a"
        });

        var resolvedKeys = options.TokenValidationParameters.IssuerSigningKeyResolver!(
                "token",
                new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(),
                "new-key",
                options.TokenValidationParameters)
            .ToArray();

        resolvedKeys.Should().BeEmpty();
        await refreshStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        signingKeyCache.Verify(cache => cache.WarmAsync("tenant-a", true, It.IsAny<CancellationToken>()), Times.Once);

        refreshRelease.TrySetResult();
    }

    [Fact]
    public void PostConfigure_DoesNotRefresh_WhenCachedKeyAlreadyMatchesKid()
    {
        var cachedKey = CreateSigningKey("current-key");
        var signingKeyCache = new Mock<IPrismSigningKeyCache>();

        signingKeyCache
            .Setup(cache => cache.GetSnapshot("tenant-a", "current-key"))
            .Returns(new PrismSigningKeyCacheSnapshot([cachedKey], false, false, true));

        var options = ConfigureOptions(signingKeyCache.Object, new PrismTenant
        {
            EntraTenantId = "tenant-a",
            EntraClientId = "client-a"
        });

        var resolvedKeys = options.TokenValidationParameters.IssuerSigningKeyResolver!(
                "token",
                new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(),
                "current-key",
                options.TokenValidationParameters)
            .ToArray();

        resolvedKeys.Should().ContainSingle().Which.KeyId.Should().Be("current-key");
        signingKeyCache.Verify(cache => cache.WarmAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PostConfigure_TriggersBackgroundRefreshWithoutBlocking_WhenCachedKeysAreExpired()
    {
        var refreshStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var refreshRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var signingKeyCache = new Mock<IPrismSigningKeyCache>();

        signingKeyCache
            .Setup(cache => cache.GetSnapshot("tenant-a", "current-key"))
            .Returns(new PrismSigningKeyCacheSnapshot([CreateSigningKey("current-key")], true, true, true));

        signingKeyCache
            .Setup(cache => cache.WarmAsync("tenant-a", true, It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                refreshStarted.TrySetResult();
                await refreshRelease.Task;
            });

        var options = ConfigureOptions(signingKeyCache.Object, new PrismTenant
        {
            EntraTenantId = "tenant-a",
            EntraClientId = "client-a"
        });

        var resolvedKeys = options.TokenValidationParameters.IssuerSigningKeyResolver!(
                "token",
                new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(),
                "current-key",
                options.TokenValidationParameters)
            .ToArray();

        resolvedKeys.Should().BeEmpty();
        await refreshStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        signingKeyCache.Verify(cache => cache.WarmAsync("tenant-a", true, It.IsAny<CancellationToken>()), Times.Once);

        refreshRelease.TrySetResult();
    }

    private static OpenIdConnectOptions ConfigureOptions(IPrismSigningKeyCache signingKeyCache, PrismTenant tenant)
    {
        var prismContext = new TestPrismContext { CurrentTenant = tenant };
        var services = new ServiceCollection()
            .AddSingleton<IPrismContext>(prismContext)
            .BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = services };
        var httpContextAccessor = new HttpContextAccessor { HttpContext = httpContext };
        var configuration = new PrismOidcConfiguration(httpContextAccessor, signingKeyCache);
        var options = new OpenIdConnectOptions();

        configuration.PostConfigure("PrismEntraID", options);

        return options;
    }

    private static SecurityKey CreateSigningKey(string keyId)
    {
        return new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes("0123456789abcdef0123456789abcdef"))
        {
            KeyId = keyId
        };
    }

    private sealed class TestPrismContext : IPrismContext
    {
        public PrismTenant? CurrentTenant { get; set; }

        public Task<System.Net.Http.Headers.AuthenticationHeaderValue?> GetAuthorizationHeaderAsync() =>
            Task.FromResult<System.Net.Http.Headers.AuthenticationHeaderValue?>(null);
    }
}