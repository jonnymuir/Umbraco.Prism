using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Tests;

/// <summary>Discovery stub that advertises Keycloak-style endpoints under the requested authority.</summary>
internal sealed class TestOidcDiscovery : IPrismOidcDiscovery
{
    public static readonly TestOidcDiscovery Keycloak = new();

    public Task<PrismOidcEndpoints> GetAsync(string authority, CancellationToken cancellationToken = default)
    {
        var root = authority.TrimEnd('/');
        return Task.FromResult(new PrismOidcEndpoints(
            root,
            $"{root}/protocol/openid-connect/auth",
            $"{root}/protocol/openid-connect/token",
            $"{root}/protocol/openid-connect/logout"));
    }
}
