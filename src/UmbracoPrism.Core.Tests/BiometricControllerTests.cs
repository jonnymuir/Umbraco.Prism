using System.Security.Claims;
using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using UmbracoPrism.Core.Controllers;
using UmbracoPrism.Core.Controllers.Models;
using UmbracoPrism.Core.Models;
using UmbracoPrism.Core.Persistence;
using UmbracoPrism.Core.Services;
using Umbraco.Cms.Infrastructure.Persistence;

namespace UmbracoPrism.Core.Tests;

public class BiometricControllerTests
{
    // ------------------------------------------------------------------ helpers

    private const string ValidSigningKey = "this-is-a-test-signing-key-32chars!!";
    private static readonly string ValidEncryptionKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private static BiometricTokenService BuildTokenService()
    {
        var opts = Options.Create(new PrismBiometricOptions
        {
            SigningKey = ValidSigningKey,
            TokenLifetimeDays = 30,
        });
        return new BiometricTokenService(opts);
    }

    private static RefreshTokenEncryptionService BuildEncryptionService(string? key = null)
    {
        var opts = Options.Create(new PrismBiometricOptions
        {
            EncryptionKey = key ?? ValidEncryptionKey,
        });
        return new RefreshTokenEncryptionService(opts);
    }

    private static IOptions<PrismBiometricOptions> BuildBiometricOptions(int lifetimeDays = 30) =>
        Options.Create(new PrismBiometricOptions
        {
            SigningKey = ValidSigningKey,
            EncryptionKey = ValidEncryptionKey,
            TokenLifetimeDays = lifetimeDays,
        });

    /// <summary>
    /// Builds a controller with configurable mocks. Returns the controller and the
    /// mock database for verifying persisted records.
    /// </summary>
    private static (BiometricController Controller, Mock<IUmbracoDatabase> Db) BuildController(
        PrismTenant? tenant = null,
        string? userOid = null,
        string? refreshToken = null,
        bool authenticated = true,
        int lifetimeDays = 30,
        PrismDeviceCredentialSchema? existingRecord = null)
    {
        var tokenService = BuildTokenService();
        var encryptionService = BuildEncryptionService();
        var biometricOptions = BuildBiometricOptions(lifetimeDays);
        var logger = Mock.Of<ILogger<BiometricController>>();

        var prismContext = new Mock<IPrismContext>();
        prismContext.Setup(c => c.CurrentTenant).Returns(tenant);

        var mockDb = new Mock<IUmbracoDatabase>();
        mockDb.Setup(db => db.FirstOrDefault<PrismDeviceCredentialSchema>(
                It.IsAny<string>(), It.IsAny<object[]>()))
            .Returns(existingRecord!);

        var dbFactory = new Mock<IUmbracoDatabaseFactory>();
        dbFactory.Setup(f => f.CreateDatabase()).Returns(mockDb.Object);

        var controller = new BiometricController(
            dbFactory.Object,
            tokenService,
            encryptionService,
            prismContext.Object,
            biometricOptions,
            logger);

        // Set up HttpContext with claims and authentication
        var claims = new List<Claim>();
        if (!string.IsNullOrEmpty(userOid))
            claims.Add(new Claim("oid", userOid));

        var identity = new ClaimsIdentity(
            authenticated ? claims : [],
            authenticated ? "PrismMemberCookie" : null);
        var principal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext { User = principal };

        // Mock IAuthenticationService on the HttpContext
        var authService = new Mock<IAuthenticationService>();
        var authProps = new AuthenticationProperties();
        if (!string.IsNullOrEmpty(refreshToken))
        {
            authProps.StoreTokens([
                new AuthenticationToken { Name = "refresh_token", Value = refreshToken },
                new AuthenticationToken { Name = "access_token", Value = "test-access-token" },
            ]);
        }

        var authResult = authenticated
            ? AuthenticateResult.Success(new AuthenticationTicket(principal, authProps, "PrismMemberCookie"))
            : AuthenticateResult.Fail("Not authenticated");

        authService
            .Setup(s => s.AuthenticateAsync(httpContext, "PrismMemberCookie"))
            .ReturnsAsync(authResult);

        httpContext.RequestServices = new MockServiceProvider(authService.Object);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext,
        };

        return (controller, mockDb);
    }

    // ------------------------------------------------------------------ happy path

    [Fact]
    public async Task Register_HappyPath_ReturnsTokenAndPersistsRecord()
    {
        var tenant = new PrismTenant { Id = 42, Name = "TestTenant" };
        var (controller, db) = BuildController(
            tenant: tenant,
            userOid: "user-oid-123",
            refreshToken: "entra-refresh-token-abc");

        var request = new BiometricRegistrationRequest
        {
            DeviceId = "device-uuid-1",
            DeviceName = "iPhone 15",
            Platform = "ios",
        };

        var result = await controller.Register(request);

        // Should return 200 with token response
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<BiometricRegistrationResponse>().Subject;

        response.BiometricToken.Should().NotBeNullOrWhiteSpace();
        response.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
        response.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddDays(30), TimeSpan.FromMinutes(5));

        // Verify Insert was called with a properly populated record
        db.Verify(d => d.Insert(It.Is<PrismDeviceCredentialSchema>(r =>
            r.TenantId == "42" &&
            r.UserId == "user-oid-123" &&
            r.DeviceId == "device-uuid-1" &&
            r.DeviceName == "iPhone 15" &&
            r.Platform == "ios" &&
            r.TokenHash.Length == 64 &&
            !string.IsNullOrWhiteSpace(r.RefreshTokenEnc) &&
            r.RefreshTokenEnc != "entra-refresh-token-abc" &&
            r.FailedAttempts == 0 &&
            r.RevokedAt == null
        )), Times.Once);
    }

    [Fact]
    public async Task Register_HappyPath_EncryptedRefreshTokenIsDecryptable()
    {
        var tenant = new PrismTenant { Id = 1, Name = "T" };
        PrismDeviceCredentialSchema? capturedRecord = null;

        var (controller, db) = BuildController(
            tenant: tenant,
            userOid: "oid-1",
            refreshToken: "secret-refresh-token");

        db.Setup(d => d.Insert(It.IsAny<PrismDeviceCredentialSchema>()))
            .Callback<object>(r => capturedRecord = r as PrismDeviceCredentialSchema)
            .Returns(new object());

        var request = new BiometricRegistrationRequest { DeviceId = "dev-1" };
        await controller.Register(request);

        capturedRecord.Should().NotBeNull();
        var encService = BuildEncryptionService();
        var decrypted = encService.Decrypt(capturedRecord!.RefreshTokenEnc);
        decrypted.Should().Be("secret-refresh-token");
    }

    // ------------------------------------------------------------------ upsert

    [Fact]
    public async Task Register_DuplicateDevice_UpsertsExistingRecord()
    {
        var tenant = new PrismTenant { Id = 42, Name = "TestTenant" };
        var existingRecord = new PrismDeviceCredentialSchema
        {
            Id = 99,
            DeviceId = "device-uuid-1",
            TenantId = "42",
            UserId = "user-oid-123",
            TokenHash = "old-hash",
            RefreshTokenEnc = "old-encrypted-token",
            RegisteredAt = DateTime.UtcNow.AddDays(-10),
            ExpiresAt = DateTime.UtcNow.AddDays(20),
        };

        var (controller, db) = BuildController(
            tenant: tenant,
            userOid: "user-oid-123",
            refreshToken: "new-refresh-token",
            existingRecord: existingRecord);

        var request = new BiometricRegistrationRequest
        {
            DeviceId = "device-uuid-1",
            Platform = "android",
        };

        var result = await controller.Register(request);

        // Should return 200 with new token
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<BiometricRegistrationResponse>().Subject;
        response.BiometricToken.Should().NotBeNullOrWhiteSpace();

        // Should have called Update (not Insert)
        db.Verify(d => d.Insert(It.IsAny<object>()), Times.Never);
        db.Verify(d => d.Update(It.Is<PrismDeviceCredentialSchema>(r =>
            r.Id == 99 &&
            r.TokenHash != "old-hash" &&
            r.RefreshTokenEnc != "old-encrypted-token" &&
            r.RevokedAt == null &&
            r.FailedAttempts == 0 &&
            r.Platform == "android"
        )), Times.Once);
    }

    // ------------------------------------------------------------------ auth failures

    [Fact]
    public async Task Register_UnauthenticatedSession_Returns401()
    {
        var tenant = new PrismTenant { Id = 1, Name = "T" };
        var (controller, _) = BuildController(
            tenant: tenant,
            authenticated: false);

        var request = new BiometricRegistrationRequest { DeviceId = "dev-1" };

        var result = await controller.Register(request);

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Register_MissingUserOid_Returns401()
    {
        var tenant = new PrismTenant { Id = 1, Name = "T" };
        var (controller, _) = BuildController(
            tenant: tenant,
            userOid: null,
            refreshToken: "some-token",
            authenticated: true);

        var request = new BiometricRegistrationRequest { DeviceId = "dev-1" };

        var result = await controller.Register(request);

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    // ------------------------------------------------------------------ missing tenant

    [Fact]
    public async Task Register_NoTenantContext_Returns400()
    {
        var (controller, _) = BuildController(
            tenant: null,
            userOid: "user-1",
            refreshToken: "rt-1");

        var request = new BiometricRegistrationRequest { DeviceId = "dev-1" };

        var result = await controller.Register(request);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ------------------------------------------------------------------ missing refresh token

    [Fact]
    public async Task Register_NoRefreshTokenInSession_Returns400()
    {
        var tenant = new PrismTenant { Id = 1, Name = "T" };
        var (controller, _) = BuildController(
            tenant: tenant,
            userOid: "user-1",
            refreshToken: null);

        var request = new BiometricRegistrationRequest { DeviceId = "dev-1" };

        var result = await controller.Register(request);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ------------------------------------------------------------------ encryption tests

    [Fact]
    public void RefreshTokenEncryption_RoundTrip_Success()
    {
        var service = BuildEncryptionService();
        var original = "some-entra-refresh-token-value-that-is-long";

        var encrypted = service.Encrypt(original);
        var decrypted = service.Decrypt(encrypted);

        decrypted.Should().Be(original);
        encrypted.Should().NotBe(original);
    }

    [Fact]
    public void RefreshTokenEncryption_DifferentNonce_EachTime()
    {
        var service = BuildEncryptionService();
        var plaintext = "same-token";

        var enc1 = service.Encrypt(plaintext);
        var enc2 = service.Encrypt(plaintext);

        // Same plaintext should produce different ciphertexts (unique nonce)
        enc1.Should().NotBe(enc2);

        // Both should decrypt to the same value
        service.Decrypt(enc1).Should().Be(plaintext);
        service.Decrypt(enc2).Should().Be(plaintext);
    }

    [Fact]
    public void RefreshTokenEncryption_WrongKey_ThrowsCryptographicException()
    {
        var service1 = BuildEncryptionService();
        var service2 = BuildEncryptionService(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));

        var encrypted = service1.Encrypt("secret-token");

        var act = () => service2.Decrypt(encrypted);
        act.Should().Throw<CryptographicException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("dG9vLXNob3J0")] // valid base64 but not 32 bytes
    public void RefreshTokenEncryption_InvalidKey_ThrowsOnConstruction(string badKey)
    {
        var act = () => BuildEncryptionService(badKey);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void RefreshTokenEncryption_NonBase64Key_ThrowsOnConstruction()
    {
        var act = () => BuildEncryptionService("not-valid-base64-!!!!");
        act.Should().Throw<InvalidOperationException>();
    }

    // ------------------------------------------------------------------ mock helpers

    /// <summary>
    /// Minimal IServiceProvider to supply IAuthenticationService for HttpContext.AuthenticateAsync.
    /// </summary>
    private class MockServiceProvider(IAuthenticationService authService) : IServiceProvider
    {
        public object? GetService(Type serviceType) =>
            serviceType == typeof(IAuthenticationService) ? authService : null;
    }
}
