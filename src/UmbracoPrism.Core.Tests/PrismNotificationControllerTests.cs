using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using UmbracoPrism.Core.Controllers;
using UmbracoPrism.Core.Controllers.Models;
using UmbracoPrism.Core.Models;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Tests;

/// <summary>
/// Unit tests for PrismNotificationController.
/// Tests controller logic only — service layer is mocked.
/// </summary>
public class PrismNotificationControllerTests
{
    // ------------------------------------------------------------------ Helpers

    private static PrismNotificationController BuildController(
        PrismTenant? tenant = null,
        string? userOid = null,
        bool authenticated = true,
        Mock<IPrismNotificationService>? serviceMock = null,
        Mock<INotificationRateLimitService>? rateLimitMock = null)
    {
        var prismContext = new Mock<IPrismContext>();
        prismContext.Setup(c => c.CurrentTenant).Returns(tenant);

        serviceMock ??= new Mock<IPrismNotificationService>();
        
        if (rateLimitMock == null)
        {
            rateLimitMock = new Mock<INotificationRateLimitService>();
            // Default: rate limiting is not active (only when not provided)
            rateLimitMock.Setup(r => r.CheckTokenRegistrationLimit(It.IsAny<string>(), It.IsAny<string>()))
                .Returns((false, 0));
            rateLimitMock.Setup(r => r.CheckSubscriptionLimit(It.IsAny<string>(), It.IsAny<string>()))
                .Returns((false, 0));
        }
        
        var logger = new Mock<ILogger<PrismNotificationController>>();

        var controller = new PrismNotificationController(
            serviceMock.Object,
            prismContext.Object,
            rateLimitMock.Object,
            logger.Object);

        // Set up HttpContext with claims
        var claims = new List<Claim>();
        if (!string.IsNullOrEmpty(userOid))
            claims.Add(new Claim("oid", userOid));

        var identity = new ClaimsIdentity(
            authenticated ? claims : [],
            authenticated ? "PrismMemberCookie" : null);
        var principal = new ClaimsPrincipal(identity);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        return controller;
    }

    // ------------------------------------------------------------------ Device Token Registration

    [Fact]
    public async Task Register_ValidToken_Returns200()
    {
        var tenant = new PrismTenant { Id = 1, Name = "Tenant1" };
        var serviceMock = new Mock<IPrismNotificationService>();

        var controller = BuildController(
            tenant: tenant,
            userOid: "user-oid-123",
            serviceMock: serviceMock);

        var request = new PrismPushRegisterRequest { PushToken = "fcm-token-abc" };

        var result = await controller.RegisterToken(request);

        result.Should().BeOfType<OkResult>();

        serviceMock.Verify(s => s.RegisterDeviceTokenAsync(
            "user-oid-123", "1", "fcm-token-abc", default), Times.Once);
    }

    [Fact]
    public async Task Register_MissingToken_Returns400()
    {
        var tenant = new PrismTenant { Id = 1, Name = "Tenant1" };
        var controller = BuildController(tenant: tenant, userOid: "user-oid-123");

        var request = new PrismPushRegisterRequest { PushToken = "" };

        var result = await controller.RegisterToken(request);

        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task Register_NullRequest_Returns400()
    {
        var tenant = new PrismTenant { Id = 1, Name = "Tenant1" };
        var controller = BuildController(tenant: tenant, userOid: "user-oid-123");

        var result = await controller.RegisterToken(null!);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Register_NoUserOid_Returns401()
    {
        var tenant = new PrismTenant { Id = 1, Name = "Tenant1" };
        var controller = BuildController(tenant: tenant, userOid: null);

        var request = new PrismPushRegisterRequest { PushToken = "fcm-token-abc" };

        var result = await controller.RegisterToken(request);

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Register_NoTenant_Returns401()
    {
        var controller = BuildController(tenant: null, userOid: "user-oid-123");

        var request = new PrismPushRegisterRequest { PushToken = "fcm-token-abc" };

        var result = await controller.RegisterToken(request);

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Unregister_AuthenticatedUser_Returns200()
    {
        var tenant = new PrismTenant { Id = 2, Name = "Tenant2" };
        var serviceMock = new Mock<IPrismNotificationService>();

        var controller = BuildController(
            tenant: tenant,
            userOid: "user-oid-456",
            serviceMock: serviceMock);

        var result = await controller.UnregisterToken();

        result.Should().BeOfType<OkResult>();

        serviceMock.Verify(s => s.UnregisterDeviceTokenAsync(
            "user-oid-456", "2", default), Times.Once);
    }

    [Fact]
    public async Task Unregister_NoUserOid_Returns401()
    {
        var tenant = new PrismTenant { Id = 2, Name = "Tenant2" };
        var controller = BuildController(tenant: tenant, userOid: null);

        var result = await controller.UnregisterToken();

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    // ------------------------------------------------------------------ Genre Subscriptions

    [Fact]
    public async Task Subscribe_ValidGenre_Returns200()
    {
        var tenant = new PrismTenant { Id = 3, Name = "Tenant3" };
        var serviceMock = new Mock<IPrismNotificationService>();

        var controller = BuildController(
            tenant: tenant,
            userOid: "user-oid-789",
            serviceMock: serviceMock);

        var request = new PrismSubscribeRequest { Genre = "news" };

        var result = await controller.Subscribe(request);

        result.Should().BeOfType<OkResult>();

        serviceMock.Verify(s => s.SubscribeToGenreAsync(
            "user-oid-789", "3", "news", default), Times.Once);
    }

    [Fact]
    public async Task Subscribe_MissingGenre_Returns400()
    {
        var tenant = new PrismTenant { Id = 3, Name = "Tenant3" };
        var controller = BuildController(tenant: tenant, userOid: "user-oid-789");

        var request = new PrismSubscribeRequest { Genre = "" };

        var result = await controller.Subscribe(request);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Subscribe_NullRequest_Returns400()
    {
        var tenant = new PrismTenant { Id = 3, Name = "Tenant3" };
        var controller = BuildController(tenant: tenant, userOid: "user-oid-789");

        var result = await controller.Subscribe(null!);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Subscribe_NoTenant_Returns401()
    {
        var controller = BuildController(tenant: null, userOid: "user-oid-789");

        var request = new PrismSubscribeRequest { Genre = "alerts" };

        var result = await controller.Subscribe(request);

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Unsubscribe_ValidGenre_Returns200()
    {
        var tenant = new PrismTenant { Id = 4, Name = "Tenant4" };
        var serviceMock = new Mock<IPrismNotificationService>();

        var controller = BuildController(
            tenant: tenant,
            userOid: "user-oid-xyz",
            serviceMock: serviceMock);

        var request = new PrismSubscribeRequest { Genre = "alerts" };

        var result = await controller.Unsubscribe(request);

        result.Should().BeOfType<OkResult>();

        serviceMock.Verify(s => s.UnsubscribeFromGenreAsync(
            "user-oid-xyz", "4", "alerts", default), Times.Once);
    }

    [Fact]
    public async Task Unsubscribe_MissingGenre_Returns400()
    {
        var tenant = new PrismTenant { Id = 4, Name = "Tenant4" };
        var controller = BuildController(tenant: tenant, userOid: "user-oid-xyz");

        var request = new PrismSubscribeRequest { Genre = "" };

        var result = await controller.Unsubscribe(request);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Unsubscribe_NoUserOid_Returns401()
    {
        var tenant = new PrismTenant { Id = 4, Name = "Tenant4" };
        var controller = BuildController(tenant: tenant, userOid: null);

        var request = new PrismSubscribeRequest { Genre = "alerts" };

        var result = await controller.Unsubscribe(request);

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    // ------------------------------------------------------------------ User Identity Resolution

    [Fact]
    public async Task Register_FallbackClaim_ResolvesUserOid()
    {
        // Test alternate claim type for user OID
        var tenant = new PrismTenant { Id = 5, Name = "Tenant5" };
        var serviceMock = new Mock<IPrismNotificationService>();
        var controller = BuildController(tenant: tenant, serviceMock: serviceMock);

        // Add fallback claim type
        var claims = new List<Claim>
        {
            new("http://schemas.microsoft.com/identity/claims/objectidentifier", "fallback-oid")
        };
        var identity = new ClaimsIdentity(claims, "PrismMemberCookie");
        var principal = new ClaimsPrincipal(identity);

        controller.ControllerContext.HttpContext.User = principal;

        var request = new PrismPushRegisterRequest { PushToken = "fcm-token-fallback" };

        var result = await controller.RegisterToken(request);

        result.Should().BeOfType<OkResult>();

        serviceMock.Verify(s => s.RegisterDeviceTokenAsync(
            "fallback-oid", "5", "fcm-token-fallback", default), Times.Once);
    }

    // ------------------------------------------------------------------ Rate Limiting

    [Fact]
    public async Task Register_RateLimited_Returns429()
    {
        var tenant = new PrismTenant { Id = 1, Name = "Tenant1" };
        var rateLimitMock = new Mock<INotificationRateLimitService>();
        
        // Override default to return rate-limited
        rateLimitMock.Setup(r => r.CheckTokenRegistrationLimit(It.IsAny<string>(), It.IsAny<string>()))
            .Returns((true, 3600)); // Limited, retry after 1 hour

        var controller = BuildController(
            tenant: tenant,
            userOid: "user-oid-123",
            rateLimitMock: rateLimitMock);

        var request = new PrismPushRegisterRequest { PushToken = "fcm-token-abc" };

        var result = await controller.RegisterToken(request);

        var statusCodeResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusCodeResult.StatusCode.Should().Be(429);

        // Verify Retry-After header is set
        controller.Response.Headers["Retry-After"].ToString().Should().Be("3600");
    }

    [Fact]
    public async Task Register_TokenTooLong_Returns400()
    {
        var tenant = new PrismTenant { Id = 1, Name = "Tenant1" };
        var controller = BuildController(tenant: tenant, userOid: "user-oid-123");

        var longToken = new string('a', 501); // Exceeds 500 character limit
        var request = new PrismPushRegisterRequest { PushToken = longToken };

        var result = await controller.RegisterToken(request);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Subscribe_RateLimited_Returns429()
    {
        var tenant = new PrismTenant { Id = 3, Name = "Tenant3" };
        var rateLimitMock = new Mock<INotificationRateLimitService>();
        
        // Override default to return rate-limited
        rateLimitMock.Setup(r => r.CheckSubscriptionLimit(It.IsAny<string>(), It.IsAny<string>()))
            .Returns((true, 1800)); // Limited, retry after 30 minutes

        var controller = BuildController(
            tenant: tenant,
            userOid: "user-oid-789",
            rateLimitMock: rateLimitMock);

        var request = new PrismSubscribeRequest { Genre = "news" };

        var result = await controller.Subscribe(request);

        var statusCodeResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusCodeResult.StatusCode.Should().Be(429);

        // Verify Retry-After header is set
        controller.Response.Headers["Retry-After"].ToString().Should().Be("1800");
    }
}
