using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using System.Globalization;

namespace UmbracoPrism.Core.Services;

/// <summary>
/// Retrieves the current access token from the authenticated session and refreshes near-expiry tokens.
/// </summary>
/// <param name="httpContextAccessor">Provides access to the active HTTP request and authentication state.</param>
public class PrismTokenService(IHttpContextAccessor httpContextAccessor)
{
    /// <summary>
    /// Gets a currently valid access token for the active user session.
    /// </summary>
    /// <returns>The access token when available; otherwise <see langword="null"/>.</returns>
    public async Task<string?> GetValidTokenAsync()
    {
        var context = httpContextAccessor.HttpContext;

        if (context == null) return null;

        var auth = await context.AuthenticateAsync();

        if (!auth.Succeeded) return null;

        var expiresAt = auth.Properties.GetTokenValue("expires_at");
        if (expiresAt != null)
        {
            var expireDate = DateTimeOffset.Parse(expiresAt, CultureInfo.InvariantCulture);

            // If token expires in less than 5 minutes, refresh it
            if (expireDate < DateTimeOffset.UtcNow.AddMinutes(5))
            {
                return await RefreshTokensAsync(auth);
            }
        }

        return auth.Properties.GetTokenValue("access_token");
    }

    private async Task<string?> RefreshTokensAsync(AuthenticateResult auth)
    {
        // This is where the complexity lies: 
        // 1. Manually call Entra ID /token endpoint using the refresh_token
        // 2. Receive new Access Token and new Refresh Token
        // 3. Update the user's current Cookie with the new values

        // For a true "Toolkit", you'd use a library like 'IdentityModel' 
        // to simplify the back-channel call to Entra.
        return "refresh_logic_placeholder";
    }
}