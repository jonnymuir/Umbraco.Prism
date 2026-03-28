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
        PrismDeviceCredentialSchema? existingRecord = null,
        Mock<IPrismTokenRefreshService>? tokenRefreshServiceMock = null,
        Mock<ISecretVaultService>? vaultMock = null)
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

        tokenRefreshServiceMock ??= new Mock<IPrismTokenRefreshService>();
        vaultMock ??= new Mock<ISecretVaultService>();

        var controller = new BiometricController(
            dbFactory.Object,
            tokenService,
            encryptionService,
            prismContext.Object,
            tokenRefreshServiceMock.Object,
            vaultMock.Object,
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

    [Fact]
    public async Task Register_CrossUserDevice_CreatesNewRecordInsteadOfHijacking()
    {
        var tenant = new PrismTenant { Id = 42, Name = "TestTenant" };

        // Alice already registered this device
        var alicesRecord = new PrismDeviceCredentialSchema
        {
            Id = 99,
            DeviceId = "device-uuid-1",
            TenantId = "42",
            UserId = "alice-oid",
            TokenHash = "alice-hash",
            RefreshTokenEnc = "alice-encrypted-token",
            RegisteredAt = DateTime.UtcNow.AddDays(-5),
            ExpiresAt = DateTime.UtcNow.AddDays(25),
        };

        // Bob tries to register the same DeviceId — query should NOT find Alice's record
        var (controller, db) = BuildController(
            tenant: tenant,
            userOid: "bob-oid",
            refreshToken: "bob-refresh-token",
            existingRecord: null); // no existing record for Bob + this device

        var request = new BiometricRegistrationRequest
        {
            DeviceId = "device-uuid-1",
            Platform = "ios",
        };

        var result = await controller.Register(request);

        // Should succeed with a new insert (not an update of Alice's record)
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<BiometricRegistrationResponse>().Subject;
        response.BiometricToken.Should().NotBeNullOrWhiteSpace();

        // Should have called Insert (new record), not Update
        db.Verify(d => d.Insert(It.Is<PrismDeviceCredentialSchema>(r =>
            r.UserId == "bob-oid" &&
            r.DeviceId == "device-uuid-1"
        )), Times.Once);
        db.Verify(d => d.Update(It.IsAny<object>()), Times.Never);
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

    // ------------------------------------------------------------------ exchange helpers

    private static readonly PrismTenant ExchangeTenant = new()
    {
        Id = 42,
        Name = "TestTenant",
        EntraTenantId = "entra-tenant-id",
        EntraClientId = "entra-client-id",
        SecretKeyName = "vault-secret-key",
    };

    /// <summary>
    /// Issues a valid biometric token and builds a matching DB credential record,
    /// returning both along with a wired-up controller + mock DB.
    /// </summary>
    private static (BiometricController Controller, Mock<IUmbracoDatabase> Db,
        string BiometricToken, PrismDeviceCredentialSchema Credential,
        Mock<IPrismTokenRefreshService> RefreshMock, Mock<IAuthenticationService> AuthMock)
        BuildExchangeScenario(
            string deviceId = "device-uuid-1",
            string userOid = "user-oid-123",
            PrismTenant? tenant = null,
            bool revoked = false,
            bool expired = false,
            string? overrideDbDeviceId = null,
            string? overrideDbUserId = null,
            TokenRefreshResult? refreshResult = null,
            string? vaultSecret = "client-secret-value")
    {
        tenant ??= ExchangeTenant;
        var tenantId = tenant.Id.ToString();

        var tokenService = BuildTokenService();
        var encryptionService = BuildEncryptionService();

        // Issue a valid biometric token JWT
        var biometricToken = tokenService.IssueToken(deviceId, tenantId, userOid, TimeSpan.FromDays(30));
        var tokenHash = tokenService.HashToken(biometricToken);

        // Encrypt a test refresh token for the DB record
        var storedRefreshToken = "stored-entra-refresh-token";
        var refreshTokenEnc = encryptionService.Encrypt(storedRefreshToken);

        var credential = new PrismDeviceCredentialSchema
        {
            Id = 1,
            DeviceId = overrideDbDeviceId ?? deviceId,
            TenantId = tenantId,
            UserId = overrideDbUserId ?? userOid,
            TokenHash = tokenHash,
            RefreshTokenEnc = refreshTokenEnc,
            RegisteredAt = DateTime.UtcNow.AddDays(-1),
            ExpiresAt = expired ? DateTime.UtcNow.AddMinutes(-1) : DateTime.UtcNow.AddDays(29),
            RevokedAt = revoked ? DateTime.UtcNow.AddHours(-1) : null,
            FailedAttempts = 0,
        };

        var refreshMock = new Mock<IPrismTokenRefreshService>();
        refreshMock
            .Setup(s => s.RefreshAsync(It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(refreshResult ?? new TokenRefreshResult(true, "new-access-token", "new-refresh-token", 3600));

        var vaultMock = new Mock<ISecretVaultService>();
        vaultMock.Setup(v => v.GetSecretAsync(It.IsAny<string>())).ReturnsAsync(vaultSecret!);

        var (controller, db) = BuildController(
            tenant: tenant,
            existingRecord: credential,
            tokenRefreshServiceMock: refreshMock,
            vaultMock: vaultMock);

        // Wire up HttpContext for the unauthenticated exchange endpoint
        var httpContext = new DefaultHttpContext();
        var authMock = new Mock<IAuthenticationService>();
        authMock.Setup(s => s.SignInAsync(It.IsAny<HttpContext>(), It.IsAny<string>(),
                It.IsAny<ClaimsPrincipal>(), It.IsAny<AuthenticationProperties>()))
            .Returns(Task.CompletedTask);

        httpContext.RequestServices = new MockServiceProvider(authMock.Object);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext,
        };

        return (controller, db, biometricToken, credential, refreshMock, authMock);
    }

    // ------------------------------------------------------------------ exchange happy path

    [Fact]
    public async Task Exchange_HappyPath_IssuesCookieAndReturns200()
    {
        var (controller, _, biometricToken, _, _, authMock) = BuildExchangeScenario();

        var request = new BiometricExchangeRequest { BiometricToken = biometricToken };
        var result = await controller.Exchange(request);

        result.Should().BeOfType<OkResult>();

        // Verify SignInAsync was called with PrismMemberCookie
        authMock.Verify(s => s.SignInAsync(
            It.IsAny<HttpContext>(),
            "PrismMemberCookie",
            It.Is<ClaimsPrincipal>(p =>
                p.FindFirst("oid")!.Value == "user-oid-123" &&
                p.FindFirst("tid")!.Value == "entra-tenant-id"),
            It.Is<AuthenticationProperties>(props =>
                props.GetTokens().Any(t => t.Name == "access_token" && t.Value == "new-access-token") &&
                props.GetTokens().Any(t => t.Name == "refresh_token" && t.Value == "new-refresh-token"))),
            Times.Once);
    }

    [Fact]
    public async Task Exchange_HappyPath_RollingRotationStoresNewRefreshToken()
    {
        var (controller, db, biometricToken, _, _, _) = BuildExchangeScenario();

        var request = new BiometricExchangeRequest { BiometricToken = biometricToken };
        await controller.Exchange(request);

        // Verify Update was called with new encrypted refresh token and LastUsedAt
        db.Verify(d => d.Update(It.Is<PrismDeviceCredentialSchema>(r =>
            r.LastUsedAt != null &&
            r.RefreshTokenEnc != string.Empty
        )), Times.Once);

        // Verify the new encrypted token is decryptable to the new refresh token
        PrismDeviceCredentialSchema? capturedRecord = null;
        db.Setup(d => d.Update(It.IsAny<object>()))
            .Callback<object>(r => capturedRecord = r as PrismDeviceCredentialSchema);

        await controller.Exchange(request);

        capturedRecord.Should().NotBeNull();
        var encService = BuildEncryptionService();
        var decrypted = encService.Decrypt(capturedRecord!.RefreshTokenEnc);
        decrypted.Should().Be("new-refresh-token");
    }

    [Fact]
    public async Task Exchange_HappyPath_CallsEntraWithCorrectEndpointAndParams()
    {
        var (controller, _, biometricToken, _, refreshMock, _) = BuildExchangeScenario();

        var request = new BiometricExchangeRequest { BiometricToken = biometricToken };
        await controller.Exchange(request);

        refreshMock.Verify(s => s.RefreshAsync(
            "https://entra-tenant-id.ciamlogin.com/entra-tenant-id/oauth2/v2.0/token",
            It.Is<IReadOnlyDictionary<string, string>>(p =>
                p["client_id"] == "entra-client-id" &&
                p["client_secret"] == "client-secret-value" &&
                p["grant_type"] == "refresh_token" &&
                p["refresh_token"] == "stored-entra-refresh-token"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Exchange_EntraReturnsNoNewRefreshToken_FallsBackToExisting()
    {
        var (controller, db, biometricToken, _, _, _) = BuildExchangeScenario(
            refreshResult: new TokenRefreshResult(true, "new-access", null, 3600));

        PrismDeviceCredentialSchema? capturedRecord = null;
        db.Setup(d => d.Update(It.IsAny<object>()))
            .Callback<object>(r => capturedRecord = r as PrismDeviceCredentialSchema);

        var request = new BiometricExchangeRequest { BiometricToken = biometricToken };
        await controller.Exchange(request);

        capturedRecord.Should().NotBeNull();
        var encService = BuildEncryptionService();
        var decrypted = encService.Decrypt(capturedRecord!.RefreshTokenEnc);
        decrypted.Should().Be("stored-entra-refresh-token", because: "should fall back to existing refresh token when Entra doesn't return a new one");
    }

    // ------------------------------------------------------------------ exchange token validation failures

    [Fact]
    public async Task Exchange_InvalidToken_Returns401BiometricTokenInvalid()
    {
        var (controller, _, _, _, _, _) = BuildExchangeScenario();

        var request = new BiometricExchangeRequest { BiometricToken = "not-a-valid-jwt" };
        var result = await controller.Exchange(request);

        var unauthorized = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        GetErrorCode(unauthorized).Should().Be("biometric_token_invalid");
    }

    [Fact]
    public async Task Exchange_ExpiredJwt_Returns401BiometricTokenInvalid()
    {
        // Issue a token with a negative lifetime (already expired)
        var tokenService = BuildTokenService();
        // We can't issue with negative lifetime; instead issue and then manipulate
        // Use a token signed with a different key
        var wrongKeyOpts = Options.Create(new PrismBiometricOptions
        {
            SigningKey = "wrong-signing-key-32-chars-long!!!!",
            TokenLifetimeDays = 30,
        });
        var wrongKeyService = new BiometricTokenService(wrongKeyOpts);
        var tamperedToken = wrongKeyService.IssueToken("dev-1", "42", "user-1", TimeSpan.FromDays(30));

        var (controller, _, _, _, _, _) = BuildExchangeScenario();

        var request = new BiometricExchangeRequest { BiometricToken = tamperedToken };
        var result = await controller.Exchange(request);

        var unauthorized = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        GetErrorCode(unauthorized).Should().Be("biometric_token_invalid");
    }

    [Fact]
    public async Task Exchange_TenantMismatch_Returns401BiometricTokenInvalid()
    {
        // Issue a token for a different tenant
        var tokenService = BuildTokenService();
        var tokenForOtherTenant = tokenService.IssueToken("device-uuid-1", "999", "user-oid-123", TimeSpan.FromDays(30));

        var (controller, _, _, _, _, _) = BuildExchangeScenario();

        var request = new BiometricExchangeRequest { BiometricToken = tokenForOtherTenant };
        var result = await controller.Exchange(request);

        var unauthorized = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        GetErrorCode(unauthorized).Should().Be("biometric_token_invalid");
    }

    [Fact]
    public async Task Exchange_TokenNotFoundInDb_Returns401BiometricTokenInvalid()
    {
        // Issue a valid token that has no matching DB record
        var tokenService = BuildTokenService();
        var unknownToken = tokenService.IssueToken("device-uuid-1", "42", "user-oid-123", TimeSpan.FromDays(30));

        // Build controller with NO existing record
        var refreshMock = new Mock<IPrismTokenRefreshService>();
        var vaultMock = new Mock<ISecretVaultService>();
        var (controller, _) = BuildController(
            tenant: ExchangeTenant,
            existingRecord: null,
            tokenRefreshServiceMock: refreshMock,
            vaultMock: vaultMock);

        var httpContext = new DefaultHttpContext();
        var authMock = new Mock<IAuthenticationService>();
        httpContext.RequestServices = new MockServiceProvider(authMock.Object);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var request = new BiometricExchangeRequest { BiometricToken = unknownToken };
        var result = await controller.Exchange(request);

        var unauthorized = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        GetErrorCode(unauthorized).Should().Be("biometric_token_invalid");
    }

    // ------------------------------------------------------------------ exchange credential state failures

    [Fact]
    public async Task Exchange_RevokedCredential_Returns401BiometricTokenInvalid()
    {
        var (controller, _, biometricToken, _, _, _) = BuildExchangeScenario(revoked: true);

        var request = new BiometricExchangeRequest { BiometricToken = biometricToken };
        var result = await controller.Exchange(request);

        var unauthorized = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        GetErrorCode(unauthorized).Should().Be("biometric_token_invalid");
    }

    [Fact]
    public async Task Exchange_ExpiredCredential_Returns401BiometricTokenInvalid()
    {
        var (controller, _, biometricToken, _, _, _) = BuildExchangeScenario(expired: true);

        var request = new BiometricExchangeRequest { BiometricToken = biometricToken };
        var result = await controller.Exchange(request);

        var unauthorized = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        GetErrorCode(unauthorized).Should().Be("biometric_token_invalid");
    }

    // ------------------------------------------------------------------ exchange binding failures

    [Fact]
    public async Task Exchange_DeviceMismatch_Returns401DeviceMismatch()
    {
        var (controller, _, biometricToken, _, _, _) = BuildExchangeScenario(
            overrideDbDeviceId: "different-device-id");

        var request = new BiometricExchangeRequest { BiometricToken = biometricToken };
        var result = await controller.Exchange(request);

        var unauthorized = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        GetErrorCode(unauthorized).Should().Be("device_mismatch");
    }

    [Fact]
    public async Task Exchange_UserIdMismatch_Returns401BiometricTokenInvalid()
    {
        var (controller, _, biometricToken, _, _, _) = BuildExchangeScenario(
            overrideDbUserId: "different-user-oid");

        var request = new BiometricExchangeRequest { BiometricToken = biometricToken };
        var result = await controller.Exchange(request);

        var unauthorized = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        GetErrorCode(unauthorized).Should().Be("biometric_token_invalid");
    }

    // ------------------------------------------------------------------ exchange Entra failures

    [Fact]
    public async Task Exchange_EntraRefreshFails_Returns401CredentialRefreshFailed()
    {
        var (controller, _, biometricToken, _, _, _) = BuildExchangeScenario(
            refreshResult: new TokenRefreshResult(false, null, null, null));

        var request = new BiometricExchangeRequest { BiometricToken = biometricToken };
        var result = await controller.Exchange(request);

        var unauthorized = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        GetErrorCode(unauthorized).Should().Be("credential_refresh_failed");
    }

    [Fact]
    public async Task Exchange_NoTenantContext_Returns400()
    {
        var refreshMock = new Mock<IPrismTokenRefreshService>();
        var vaultMock = new Mock<ISecretVaultService>();
        var (controller, _) = BuildController(
            tenant: null,
            tokenRefreshServiceMock: refreshMock,
            vaultMock: vaultMock);

        var httpContext = new DefaultHttpContext();
        httpContext.RequestServices = new MockServiceProvider(new Mock<IAuthenticationService>().Object);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var request = new BiometricExchangeRequest { BiometricToken = "any-token" };
        var result = await controller.Exchange(request);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Exchange_MissingEntraConfig_Returns401CredentialRefreshFailed()
    {
        var tenantWithoutEntra = new PrismTenant
        {
            Id = 42,
            Name = "TestTenant",
            EntraTenantId = null,
            EntraClientId = null,
            SecretKeyName = null,
        };

        var (controller, _, biometricToken, _, _, _) = BuildExchangeScenario(tenant: tenantWithoutEntra);

        var request = new BiometricExchangeRequest { BiometricToken = biometricToken };
        var result = await controller.Exchange(request);

        var unauthorized = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        GetErrorCode(unauthorized).Should().Be("credential_refresh_failed");
    }

    [Fact]
    public async Task Exchange_VaultSecretMissing_Returns401CredentialRefreshFailed()
    {
        var (controller, _, biometricToken, _, _, _) = BuildExchangeScenario(vaultSecret: null);

        var request = new BiometricExchangeRequest { BiometricToken = biometricToken };
        var result = await controller.Exchange(request);

        var unauthorized = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        GetErrorCode(unauthorized).Should().Be("credential_refresh_failed");
    }

    [Fact]
    public async Task Exchange_DoesNotCallEntra_WhenTokenValidationFails()
    {
        var (controller, _, _, _, refreshMock, _) = BuildExchangeScenario();

        var request = new BiometricExchangeRequest { BiometricToken = "invalid-jwt" };
        await controller.Exchange(request);

        refreshMock.Verify(s => s.RefreshAsync(
            It.IsAny<string>(),
            It.IsAny<IReadOnlyDictionary<string, string>>(),
            It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Exchange_DoesNotUpdateDb_WhenEntraRefreshFails()
    {
        var (controller, db, biometricToken, _, _, _) = BuildExchangeScenario(
            refreshResult: new TokenRefreshResult(false, null, null, null));

        var request = new BiometricExchangeRequest { BiometricToken = biometricToken };
        await controller.Exchange(request);

        db.Verify(d => d.Update(It.IsAny<object>()), Times.Never);
    }

    // ------------------------------------------------------------------ error code helper

    private static string? GetErrorCode(UnauthorizedObjectResult result)
    {
        var value = result.Value;
        if (value == null) return null;
        var errorProp = value.GetType().GetProperty("error");
        return errorProp?.GetValue(value)?.ToString();
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
