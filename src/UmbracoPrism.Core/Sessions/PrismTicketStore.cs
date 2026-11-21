using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using UmbracoPrism.Core.Extensions;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Sessions;

/// <summary>
/// Holds the member's authentication ticket, with its access, ID and refresh tokens, on the
/// server. The cookie carries a random key and nothing else; the ticket is protected with Data
/// Protection before it reaches the <see cref="IPrismSessionStorage"/>.
/// </summary>
public sealed class PrismTicketStore(
    IPrismSessionStorage storage,
    IDataProtectionProvider dataProtection,
    IOptions<PrismSessionOptions> session,
    TimeProvider clock) : ITicketStore
{
    private readonly IDataProtector _protector = dataProtection.CreateProtector("UmbracoPrism.SessionTicket");

    public async Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        var key = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        await RenewAsync(key, ticket);
        return key;
    }

    public Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        var expiresAt = ticket.Properties.ExpiresUtc
            ?? clock.GetUtcNow().AddMinutes(session.Value.SlidingExpirationMinutes);
        var data = _protector.Protect(TicketSerializer.Default.Serialize(ticket));
        return storage.SaveAsync(key, data, expiresAt, ticket.Principal.GetSubject(), CancellationToken.None);
    }

    public async Task<AuthenticationTicket?> RetrieveAsync(string key)
    {
        var data = await storage.LoadAsync(key, CancellationToken.None);
        if (data is null)
        {
            return null;
        }

        try
        {
            return TicketSerializer.Default.Deserialize(_protector.Unprotect(data));
        }
        catch (CryptographicException)
        {
            // Protected with keys this host no longer has: the session is gone.
            return null;
        }
    }

    public Task RemoveAsync(string key) => storage.RemoveAsync(key, CancellationToken.None);
}
