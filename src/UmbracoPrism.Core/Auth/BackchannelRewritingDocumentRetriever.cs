using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Protocols;

namespace UmbracoPrism.Core.Auth;

/// <summary>
/// Wraps an <see cref="IDocumentRetriever"/> and rewrites any URL whose origin matches
/// <paramref name="publicAuthorityUri"/> to the internal <paramref name="backchannelBaseUri"/> before
/// delegating. Covers both the discovery-document fetch and the transitive <c>jwks_uri</c>
/// fetch made by <see cref="OpenIdConnectConfigurationRetriever"/>.
/// </summary>
internal sealed class BackchannelRewritingDocumentRetriever(
    Uri publicAuthorityUri,
    Uri backchannelBaseUri,
    IDocumentRetriever inner) : IDocumentRetriever
{
    public Task<string> GetDocumentAsync(string address, CancellationToken cancel)
    {
        if (TryRewriteAddress(address, out var rewritten))
        {
                            address = rewritten;
        }

        return inner.GetDocumentAsync(address, cancel);
    }

    private bool TryRewriteAddress(string address, out string rewritten)
    {
        rewritten = address;

        if (!Uri.TryCreate(address, UriKind.Absolute, out var candidateUri) ||
            !string.Equals(candidateUri.Host, publicAuthorityUri.Host, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var builder = new UriBuilder(candidateUri)
        {
            Scheme = backchannelBaseUri.Scheme,
            Host = backchannelBaseUri.Host,
            Port = backchannelBaseUri.IsDefaultPort ? -1 : backchannelBaseUri.Port
        };

        rewritten = builder.Uri.AbsoluteUri;
        return !string.Equals(rewritten, address, StringComparison.OrdinalIgnoreCase);
    }
}
