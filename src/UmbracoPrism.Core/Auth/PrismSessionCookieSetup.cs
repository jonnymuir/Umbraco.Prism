using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Auth;

/// <summary>Gives the member cookie an explicit sliding window from <see cref="PrismSessionOptions"/> instead of the framework default.</summary>
internal sealed class PrismSessionCookieSetup(IOptions<PrismSessionOptions> session) : IConfigureNamedOptions<CookieAuthenticationOptions>
{
    public void Configure(string? name, CookieAuthenticationOptions options)
    {
        if (name != PrismAuthenticationSchemes.MemberCookie)
        {
            return;
        }

        options.ExpireTimeSpan = TimeSpan.FromMinutes(session.Value.SlidingExpirationMinutes);
        options.SlidingExpiration = true;
    }

    public void Configure(CookieAuthenticationOptions options)
    {
    }
}
