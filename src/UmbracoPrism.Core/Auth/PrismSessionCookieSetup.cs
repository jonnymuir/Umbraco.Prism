using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using UmbracoPrism.Core.Services;
using UmbracoPrism.Core.Sessions;

namespace UmbracoPrism.Core.Auth;

/// <summary>
/// Gives the member cookie an explicit sliding window from <see cref="PrismSessionOptions"/>, keeps
/// the ticket in the server-side <see cref="PrismTicketStore"/> and names the cookie with the
/// <c>__Host-</c> prefix.
/// </summary>
internal sealed class PrismSessionCookieSetup(IOptions<PrismSessionOptions> session, PrismTicketStore ticketStore) : IConfigureNamedOptions<CookieAuthenticationOptions>
{
    public void Configure(string? name, CookieAuthenticationOptions options)
    {
        if (name != PrismAuthenticationSchemes.MemberCookie)
        {
            return;
        }

        options.ExpireTimeSpan = TimeSpan.FromMinutes(session.Value.SlidingExpirationMinutes);
        options.SlidingExpiration = true;
        options.SessionStore = ticketStore;
        options.Cookie.Name = PrismAuthenticationSchemes.MemberCookieName;
        options.Cookie.Path = "/";
        options.Cookie.Domain = null;
    }

    public void Configure(CookieAuthenticationOptions options)
    {
    }
}
