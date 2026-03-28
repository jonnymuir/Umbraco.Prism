using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace UmbracoPrism.Core.Services;

/// <summary>
/// Issues, validates, and hashes BiometricToken JWTs using HMAC-SHA256 signing.
/// The signing key is read from <see cref="PrismBiometricOptions.SigningKey"/>
/// (bind from "Prism:Biometric" in appsettings.json or via environment/Key Vault injection).
/// </summary>
public class BiometricTokenService : IBiometricTokenService
{
    internal const string DeviceIdClaim = "sub";
    internal const string TenantIdClaim = "tid";
    internal const string UserOidClaim = "oid";
    internal const string Issuer = "UmbracoPrism";
    internal const string Audience = "PrismBiometric";

    private readonly SymmetricSecurityKey _signingKey;
    private readonly SigningCredentials _signingCredentials;
    private readonly JwtSecurityTokenHandler _tokenHandler;

    /// <summary>
    /// Initialises the service from bound <see cref="PrismBiometricOptions"/>.
    /// Throws <see cref="InvalidOperationException"/> when <see cref="PrismBiometricOptions.SigningKey"/>
    /// is absent or too short (minimum 32 characters for HMAC-SHA256 security).
    /// </summary>
    public BiometricTokenService(IOptions<PrismBiometricOptions> options)
    {
        var key = options.Value.SigningKey;

        if (string.IsNullOrWhiteSpace(key) || key.Length < 32)
            throw new InvalidOperationException(
                "Prism: BiometricToken signing key must be at least 32 characters. " +
                "Set 'Prism:Biometric:SigningKey' in configuration.");

        _signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        _signingCredentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256);
        _tokenHandler = new JwtSecurityTokenHandler();
    }

    /// <inheritdoc/>
    public string IssueToken(string deviceId, string tenantId, string userOid, TimeSpan lifetime)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(userOid);

        var now = DateTime.UtcNow;
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = now.Add(lifetime),
            SigningCredentials = _signingCredentials,
            Subject = new ClaimsIdentity(
            [
                new Claim(DeviceIdClaim, deviceId),
                new Claim(TenantIdClaim, tenantId),
                new Claim(UserOidClaim, userOid),
            ])
        };

        var token = _tokenHandler.CreateToken(descriptor);
        return _tokenHandler.WriteToken(token);
    }

    /// <inheritdoc/>
    public BiometricTokenClaims ValidateToken(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = Issuer,
            ValidateAudience = true,
            ValidAudience = Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = _signingKey,
            ClockSkew = TimeSpan.Zero,
        };

        var principal = _tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);

        if (validatedToken is not JwtSecurityToken jwt)
            throw new SecurityTokenException("Prism: BiometricToken validation produced an unexpected token type.");

        // Read claims directly from the JWT payload to avoid claim-type mapping
        // differences across platforms and .NET versions (ClaimsPrincipal.FindFirstValue
        // can return empty for short claim names like "sub" on some runtimes).
        return new BiometricTokenClaims
        {
            DeviceId = jwt.Claims.FirstOrDefault(c => c.Type == DeviceIdClaim)?.Value ?? string.Empty,
            TenantId = jwt.Claims.FirstOrDefault(c => c.Type == TenantIdClaim)?.Value ?? string.Empty,
            UserOid = jwt.Claims.FirstOrDefault(c => c.Type == UserOidClaim)?.Value ?? string.Empty,
            IssuedAt = jwt.IssuedAt,
            ExpiresAt = jwt.ValidTo,
        };
    }

    /// <inheritdoc/>
    public string HashToken(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexStringLower(bytes);
    }
}
