using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Tests;

public class BiometricTokenServiceTests
{
    // ------------------------------------------------------------------ helpers

    private const string ValidKey = "this-is-a-test-signing-key-32chars!!";

    private static BiometricTokenService BuildService(string? signingKey = null, int lifetimeDays = 30)
    {
        var opts = Options.Create(new PrismBiometricOptions
        {
            SigningKey = signingKey ?? ValidKey,
            TokenLifetimeDays = lifetimeDays,
        });
        return new BiometricTokenService(opts);
    }

    private static TimeSpan DefaultLifetime => TimeSpan.FromDays(30);

    private static string BuildExpiredToken()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(ValidKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var past = DateTime.UtcNow.AddDays(-2);
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = BiometricTokenService.Issuer,
            Audience = BiometricTokenService.Audience,
            NotBefore = past,
            IssuedAt = past,
            Expires = past.AddDays(1),
            SigningCredentials = creds,
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(BiometricTokenService.DeviceIdClaim, "dev-expired"),
                new Claim(BiometricTokenService.TenantIdClaim, "tenant-x"),
                new Claim(BiometricTokenService.UserOidClaim, "oid-abc"),
            })
        };
        return handler.WriteToken(handler.CreateToken(descriptor));
    }

    // ------------------------------------------------------------------ IssueToken

    [Fact]
    public void IssueToken_ReturnsValidJwt()
    {
        var svc = BuildService();
        var token = svc.IssueToken("device-1", "tenant-abc", "user-oid-xyz", DefaultLifetime);

        token.Should().NotBeNullOrWhiteSpace();

        var handler = new JwtSecurityTokenHandler();
        handler.CanReadToken(token).Should().BeTrue();

        var jwt = handler.ReadJwtToken(token);
        jwt.Claims.First(c => c.Type == "sub").Value.Should().Be("device-1");
        jwt.Claims.First(c => c.Type == "tid").Value.Should().Be("tenant-abc");
        jwt.Claims.First(c => c.Type == "oid").Value.Should().Be("user-oid-xyz");
        jwt.ValidTo.Should().BeAfter(DateTime.UtcNow);
    }

    // ------------------------------------------------------------------ ValidateToken

    [Fact]
    public void ValidateToken_ValidToken_ReturnsClaims()
    {
        var svc = BuildService();
        var token = svc.IssueToken("dev-42", "tenant-x", "oid-abc", DefaultLifetime);

        var claims = svc.ValidateToken(token);

        claims.DeviceId.Should().Be("dev-42");
        claims.TenantId.Should().Be("tenant-x");
        claims.UserOid.Should().Be("oid-abc");
        claims.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
        claims.IssuedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void ValidateToken_ExpiredToken_Throws()
    {
        var svc = BuildService();
        var expiredToken = BuildExpiredToken();

        var act = () => svc.ValidateToken(expiredToken);

        act.Should().Throw<SecurityTokenException>();
    }

    [Fact]
    public void ValidateToken_TamperedToken_Throws()
    {
        var svc = BuildService();
        var token = svc.IssueToken("dev-tamper", "tenant-x", "oid-abc", DefaultLifetime);

        // Flip the last character of the signature to invalidate it
        var tampered = token[..^1] + (token[^1] == 'A' ? 'B' : 'A');

        var act = () => svc.ValidateToken(tampered);

        act.Should().Throw<SecurityTokenException>();
    }

    [Fact]
    public void ValidateToken_WrongSigningKey_Throws()
    {
        var issuer = BuildService("this-is-a-test-signing-key-32chars!!");
        var validator = BuildService("different-signing-key-also-32-chars!");

        var token = issuer.IssueToken("dev-1", "tenant-1", "oid-1", DefaultLifetime);

        var act = () => validator.ValidateToken(token);

        act.Should().Throw<SecurityTokenException>();
    }

    // ------------------------------------------------------------------ HashToken

    [Fact]
    public void HashToken_SameInput_SameOutput()
    {
        var svc = BuildService();
        var token = svc.IssueToken("dev-hash", "tenant-hash", "oid-hash", DefaultLifetime);

        var hash1 = svc.HashToken(token);
        var hash2 = svc.HashToken(token);

        hash1.Should().Be(hash2);
        hash1.Should().MatchRegex("^[0-9a-f]{64}$"); // 32 bytes = 64 hex chars
    }

    [Fact]
    public void HashToken_DifferentInputs_DifferentOutputs()
    {
        var svc = BuildService();
        var t1 = svc.IssueToken("dev-a", "tenant-a", "oid-a", DefaultLifetime);
        var t2 = svc.IssueToken("dev-b", "tenant-b", "oid-b", DefaultLifetime);

        svc.HashToken(t1).Should().NotBe(svc.HashToken(t2));
    }

    // ------------------------------------------------------------------ constructor guards

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("only-31-characters-in-this-key!")]
    public void Constructor_ShortOrEmptyKey_Throws(string key)
    {
        var act = () => BuildService(signingKey: key);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Prism*BiometricToken*");
    }
}
