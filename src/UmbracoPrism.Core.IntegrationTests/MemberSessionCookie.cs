using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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
        WaitForPrismTables(services);

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

    // The host answers requests before Prism's package migration has created its tables. This
    // looks from a separate read-only connection with a private cache: a reader on Umbraco's own
    // shared-cache connection makes the migration's writes fail at once with "database is locked"
    // instead of waiting, and a failed migration fails the boot.
    internal static void WaitForPrismTables(IServiceProvider services)
    {
        var dsn = services.GetRequiredService<IConfiguration>()["ConnectionStrings:umbracoDbDSN"]!;
        var builder = new SqliteConnectionStringBuilder(dsn) { Cache = SqliteCacheMode.Private, Mode = SqliteOpenMode.ReadOnly, Pooling = false };

        var deadline = DateTime.UtcNow.AddMinutes(2);
        while (true)
        {
            try
            {
                using var connection = new SqliteConnection(builder.ConnectionString);
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND lower(name) IN ('prismtenants', 'prismdevicecredentials', 'prismsessions')";
                if (Convert.ToInt32(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) == 3)
                {
                    return;
                }
            }
            catch (SqliteException) when (DateTime.UtcNow < deadline)
            {
                // The database file is not there yet, or is briefly busy.
            }

            if (DateTime.UtcNow >= deadline)
            {
                throw new TimeoutException("Prism's tables were not created within two minutes of the host starting.");
            }

            Thread.Sleep(500);
        }
    }
}
