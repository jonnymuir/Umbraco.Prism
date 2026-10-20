using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Distributed;

namespace UmbracoPrism.Core.Services;

/// <summary>
/// One-time codes that carry a member's sign-in from the system authentication session to the
/// app's webview. A code is single use, lives for two minutes, and is only redeemed with the
/// verifier whose S256 challenge the app started sign-in with.
/// </summary>
public class PrismMobileSignInCodes(IDistributedCache cache, IDataProtectionProvider dataProtection)
{
    internal static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(2);

    private const string KeyPrefix = "prism:mobile-sign-in:";

    private readonly TicketDataFormat _tickets = new(dataProtection.CreateProtector("UmbracoPrism.MobileSignIn"));

    /// <summary>A sign-in waiting for the app to redeem it.</summary>
    public sealed record PendingSignIn(int TenantId, string Challenge, string ReturnUrl, AuthenticationTicket Ticket);

    private sealed record StoredSignIn(int TenantId, string Challenge, string ReturnUrl, string Ticket);

    public async Task<string> IssueAsync(PendingSignIn signIn, CancellationToken cancellationToken)
    {
        var code = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var stored = new StoredSignIn(signIn.TenantId, signIn.Challenge, signIn.ReturnUrl, _tickets.Protect(signIn.Ticket));

        await cache.SetAsync(
            Key(code),
            JsonSerializer.SerializeToUtf8Bytes(stored),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = Lifetime },
            cancellationToken);

        return code;
    }

    /// <summary>Removes the code and returns its sign-in, or null when the code is unknown, used or expired.</summary>
    public async Task<PendingSignIn?> TakeAsync(string code, CancellationToken cancellationToken)
    {
        var key = Key(code);
        var bytes = await cache.GetAsync(key, cancellationToken);
        if (bytes is null)
        {
            return null;
        }

        await cache.RemoveAsync(key, cancellationToken);

        var stored = JsonSerializer.Deserialize<StoredSignIn>(bytes);
        var ticket = stored is null ? null : _tickets.Unprotect(stored.Ticket);
        return stored is null || ticket is null
            ? null
            : new PendingSignIn(stored.TenantId, stored.Challenge, stored.ReturnUrl, ticket);
    }

    /// <summary>The S256 challenge for <paramref name="verifier"/>, as RFC 7636 defines it.</summary>
    public static string ChallengeFor(string verifier) =>
        WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

    private static string Key(string code) =>
        KeyPrefix + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code)));
}
