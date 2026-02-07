using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using UmbracoPrism.Core.Middleware;
using UmbracoPrism.Core.Models;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Tests;

public class PrismTenantMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_SetsCurrentTenant_WhenFound()
    {
        var tenant = new PrismTenant { Id = 1, Name = "Example", Hostname = "example.com" };
        var tenantService = new Mock<ITenantService>();
        tenantService.Setup(s => s.GetByDomainAsync("example.com")).ReturnsAsync(tenant);

        var prismContext = new Mock<IPrismContext>();
        var logger = new Mock<ILogger<PrismTenantMiddleware>>();

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Host = new HostString("example.com");

        var nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new PrismTenantMiddleware(next, logger.Object);
        await middleware.InvokeAsync(httpContext, tenantService.Object, prismContext.Object);

        prismContext.VerifySet(p => p.CurrentTenant = tenant, Times.Once);
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_LogsWarning_WhenTenantNotFound()
    {
        var tenantService = new Mock<ITenantService>();
        tenantService.Setup(s => s.GetByDomainAsync("unknown.com")).ReturnsAsync((PrismTenant?)null);

        var prismContext = new Mock<IPrismContext>();
        var logger = new Mock<ILogger<PrismTenantMiddleware>>();

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Host = new HostString("unknown.com");

        RequestDelegate next = _ => Task.CompletedTask;

        var middleware = new PrismTenantMiddleware(next, logger.Object);
        await middleware.InvokeAsync(httpContext, tenantService.Object, prismContext.Object);

        prismContext.VerifySet(p => p.CurrentTenant = It.IsAny<PrismTenant>(), Times.Never);
        logger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Unknown tenant domain")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
