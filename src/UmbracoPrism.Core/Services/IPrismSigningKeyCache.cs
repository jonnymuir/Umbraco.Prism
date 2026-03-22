using Microsoft.IdentityModel.Tokens;

namespace UmbracoPrism.Core.Services;

public interface IPrismSigningKeyCache
{
    Task WarmAsync(string entraTenantId, CancellationToken cancellationToken = default);
    IEnumerable<SecurityKey> GetSigningKeys(string entraTenantId);
}
