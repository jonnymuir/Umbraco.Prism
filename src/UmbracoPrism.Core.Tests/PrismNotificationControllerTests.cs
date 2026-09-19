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
        string? userEmail = null,
        bool authenticated = true,
        Mock<IPrismNotificationService>? serviceMock = null,
        Mock<INotificationRateLimitService>? rateLimitMock = null)
    {
        var userContext = new Mock<IPrismUserContext>();
        userContext.Setup(c => c.CurrentTenant).Returns(tenant);
        userContext.Setup(c => c.IsAuthenticated).Returns(authenticated && !string.IsNullOrEmpty(userEmail));
        userContext.Setup(c => c.Email).Returns(userEmail);

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
            userContext.Object,
            rateLimitMock.Object,
            logger.Object);

        var identity = new ClaimsIdentity(
            authenticated ? [] : null,
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
        var tenant = new PrismTenant { Id = 1, Name = "Tenant1", Hostname = "tenant1.example" };
        var serviceMock = new Mock<IPrismNotificationService>();

        var controller = BuildController(
            tenant: tenant,
            userEmail: "member@example.com",
            serviceMock: serviceMock);

        var request = new PrismPushRegisterRequest { PushToken = "fcm-token-abc" };

        var result = await controller.RegisterToken(request);

        result.Should().BeOfType<OkResult>();

        serviceMock.Verify(s => s.RegisterDeviceTokenAsync(
            "member@example.com", "tenant1.example", "fcm-token-abc", default), Times.Once);
    }

    [Fact]
    public async Task Register_MissingToken_Returns400()
    {
        var tenant = new PrismTenant { Id = 1, Name = "Tenant1", Hostname = "tenant1.example" };
        var controller = BuildController(tenant: tenant, userEmail: "member@example.com");

        var request = new PrismPushRegisterRequest { PushToken = "" };

        var result = await controller.RegisterToken(request);

        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task Register_NullRequest_Returns400()
    {
        var tenant = new PrismTenant { Id = 1, Name = "Tenant1", Hostname = "tenant1.example" };
        var controller = BuildController(tenant: tenant, userEmail: "member@example.com");

        var result = await controller.RegisterToken(null!);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Register_NotAuthenticated_Returns401()
    {
        var tenant = new PrismTenant { Id = 1, Name = "Tenant1", Hostname = "tenant1.example" };
        var controller = BuildController(tenant: tenant, userEmail: null, authenticated: false);

        var request = new PrismPushRegisterRequest { PushToken = "fcm-token-abc" };

        var result = await controller.RegisterToken(request);

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Register_NoTenant_Returns401()
    {
        var controller = BuildController(tenant: null, userEmail: "member@example.com");

        var request = new PrismPushRegisterRequest { PushToken = "fcm-token-abc" };

        var result = await controller.RegisterToken(request);

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Unregister_AuthenticatedUser_Returns200()
    {
        var tenant = new PrismTenant { Id = 2, Name = "Tenant2", Hostname = "tenant2.example" };
        var serviceMock = new Mock<IPrismNotificationService>();

        var controller = BuildController(
            tenant: tenant,
            userEmail: "another-member@example.com",
            serviceMock: serviceMock);

        var result = await controller.UnregisterToken();

        result.Should().BeOfType<OkResult>();

        serviceMock.Verify(s => s.UnregisterDeviceTokenAsync(
            "another-member@example.com", "tenant2.example", default), Times.Once);
    }

    [Fact]
    public async Task Unregister_NotAuthenticated_Returns401()
    {
        var tenant = new PrismTenant { Id = 2, Name = "Tenant2", Hostname = "tenant2.example" };
        var controller = BuildController(tenant: tenant, userEmail: null, authenticated: false);

        var result = await controller.UnregisterToken();

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    // ------------------------------------------------------------------ Genre Subscriptions

    [Fact]
    public async Task Subscribe_ValidGenre_Returns200()
    {
        var tenant = new PrismTenant { Id = 3, Name = "Tenant3", Hostname = "tenant3.example" };
        var serviceMock = new Mock<IPrismNotificationService>();

        var controller = BuildController(
            tenant: tenant,
            userEmail: "subscriber@example.com",
            serviceMock: serviceMock);

        var request = new PrismSubscribeRequest { Genre = "news" };

        var result = await controller.Subscribe(request);

        result.Should().BeOfType<OkResult>();

        serviceMock.Verify(s => s.SubscribeToGenreAsync(
            "subscriber@example.com", "tenant3.example", "news", default), Times.Once);
    }

    [Fact]
    public async Task Subscribe_MissingGenre_Returns400()
    {
        var tenant = new PrismTenant { Id = 3, Name = "Tenant3", Hostname = "tenant3.example" };
        var controller = BuildController(tenant: tenant, userEmail: "subscriber@example.com");

        var request = new PrismSubscribeRequest { Genre = "" };

        var result = await controller.Subscribe(request);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Subscribe_NullRequest_Returns400()
    {
        var tenant = new PrismTenant { Id = 3, Name = "Tenant3", Hostname = "tenant3.example" };
        var controller = BuildController(tenant: tenant, userEmail: "subscriber@example.com");

        var result = await controller.Subscribe(null!);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Subscribe_NoTenant_Returns401()
    {
        var controller = BuildController(tenant: null, userEmail: "subscriber@example.com");

        var request = new PrismSubscribeRequest { Genre = "alerts" };

        var result = await controller.Subscribe(request);

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Unsubscribe_ValidGenre_Returns200()
    {
        var tenant = new PrismTenant { Id = 4, Name = "Tenant4", Hostname = "tenant4.example" };
        var serviceMock = new Mock<IPrismNotificationService>();

        var controller = BuildController(
            tenant: tenant,
            userEmail: "unsubscriber@example.com",
            serviceMock: serviceMock);

        var request = new PrismSubscribeRequest { Genre = "alerts" };

        var result = await controller.Unsubscribe(request);

        result.Should().BeOfType<OkResult>();

        serviceMock.Verify(s => s.UnsubscribeFromGenreAsync(
            "unsubscriber@example.com", "tenant4.example", "alerts", default), Times.Once);
    }

    [Fact]
    public async Task Unsubscribe_MissingGenre_Returns400()
    {
        var tenant = new PrismTenant { Id = 4, Name = "Tenant4", Hostname = "tenant4.example" };
        var controller = BuildController(tenant: tenant, userEmail: "unsubscriber@example.com");

        var request = new PrismSubscribeRequest { Genre = "" };

        var result = await controller.Unsubscribe(request);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Unsubscribe_NotAuthenticated_Returns401()
    {
        var tenant = new PrismTenant { Id = 4, Name = "Tenant4", Hostname = "tenant4.example" };
        var controller = BuildController(tenant: tenant, userEmail: null, authenticated: false);

        var request = new PrismSubscribeRequest { Genre = "alerts" };

        var result = await controller.Unsubscribe(request);

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    // ------------------------------------------------------------------ Rate Limiting

    [Fact]
    public async Task Register_RateLimited_Returns429()
    {
        var tenant = new PrismTenant { Id = 1, Name = "Tenant1", Hostname = "tenant1.example" };
        var rateLimitMock = new Mock<INotificationRateLimitService>();

        // Override default to return rate-limited
        rateLimitMock.Setup(r => r.CheckTokenRegistrationLimit(It.IsAny<string>(), It.IsAny<string>()))
            .Returns((true, 3600)); // Limited, retry after 1 hour

        var controller = BuildController(
            tenant: tenant,
            userEmail: "member@example.com",
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
        var tenant = new PrismTenant { Id = 1, Name = "Tenant1", Hostname = "tenant1.example" };
        var controller = BuildController(tenant: tenant, userEmail: "member@example.com");

        var longToken = new string('a', 501); // Exceeds 500 character limit
        var request = new PrismPushRegisterRequest { PushToken = longToken };

        var result = await controller.RegisterToken(request);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Subscribe_RateLimited_Returns429()
    {
        var tenant = new PrismTenant { Id = 3, Name = "Tenant3", Hostname = "tenant3.example" };
        var rateLimitMock = new Mock<INotificationRateLimitService>();

        // Override default to return rate-limited
        rateLimitMock.Setup(r => r.CheckSubscriptionLimit(It.IsAny<string>(), It.IsAny<string>()))
            .Returns((true, 1800)); // Limited, retry after 30 minutes

        var controller = BuildController(
            tenant: tenant,
            userEmail: "subscriber@example.com",
            rateLimitMock: rateLimitMock);

        var request = new PrismSubscribeRequest { Genre = "news" };

        var result = await controller.Subscribe(request);

        var statusCodeResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusCodeResult.StatusCode.Should().Be(429);

        // Verify Retry-After header is set
        controller.Response.Headers["Retry-After"].ToString().Should().Be("1800");
    }
}
