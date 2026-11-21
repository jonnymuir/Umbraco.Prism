using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Infrastructure.Persistence;

namespace UmbracoPrism.Core.IntegrationTests;

/// <summary>
/// Issues a member session cookie the way the host does: the ticket goes into the server-side
/// session store, and the cookie carries only the reference to it.
/// </summary>
internal static class MemberSessionCookie
{
    private const string SessionIdClaim = "Microsoft.AspNetCore.Authentication.Cookies-SessionId";

    internal sealed record Issued(string Cookie, string SessionKey);

    internal static async Task<Issued> IssueAsync(IServiceProvider services, ClaimsPrincipal principal, DateTimeOffset expires, string? refreshToken = null)
    {
        WaitForPrismTables(services.GetRequiredService<IUmbracoDatabaseFactory>());

        var options = services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get("PrismMemberCookie");
        var properties = new AuthenticationProperties { IssuedUtc = DateTimeOffset.UtcNow, ExpiresUtc = expires, IsPersistent = true };
        if (refreshToken is not null)
        {
            properties.StoreTokens([new AuthenticationToken { Name = "refresh_token", Value = refreshToken }]);
        }

        var key = await options.SessionStore!.StoreAsync(new AuthenticationTicket(principal, properties, "PrismMemberCookie"));
        var reference = new AuthenticationTicket(
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(SessionIdClaim, key)], "PrismMemberCookie")),
            new AuthenticationProperties { IssuedUtc = properties.IssuedUtc, ExpiresUtc = expires, IsPersistent = true },
            "PrismMemberCookie");

        return new Issued($"{options.Cookie.Name}={options.TicketDataFormat.Protect(reference)}", key);
    }

    // The host answers requests before Prism's package migration has created its tables.
    internal static void WaitForPrismTables(IUmbracoDatabaseFactory databaseFactory)
    {
        var deadline = DateTime.UtcNow.AddMinutes(2);
        while (true)
        {
            try
            {
                using var db = databaseFactory.CreateDatabase();
                db.ExecuteScalar<int>("SELECT COUNT(*) FROM prismTenants");
                db.ExecuteScalar<int>("SELECT COUNT(*) FROM prismDeviceCredentials");
                db.ExecuteScalar<int>("SELECT COUNT(*) FROM prismSessions");
                return;
            }
            catch (Exception) when (DateTime.UtcNow < deadline)
            {
                Thread.Sleep(500);
            }
        }
    }
}
