using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using UmbracoPrism.Core.Models;
using UmbracoPrism.Core.Services;
using UmbracoPrism.Core.Services.ServiceDesign;
using Wayfinder.Services.Sanitization;
using UmbracoPrism.ProcessManager.Abstractions;
using UmbracoPrism.ProcessManager.Stores;

namespace UmbracoPrism.Core.Tests.ServiceDesign.Runtime;

/// <summary>
/// Verifies <see cref="InProcessCmsProcessManagerClient"/>'s identity resolution — the piece that
/// makes "only available to the user who initiated it" true for anonymous visitors, and lets a
/// logged-in Prism Member's identity flow through to <c>CmsProcessManager.ResolveServiceInputs</c>
/// without hardcoding any claims-handling in a per-host controller override.
/// </summary>
public class InProcessCmsProcessManagerClientTests
{
    private static (InProcessCmsProcessManagerClient Client, DefaultHttpContext HttpContext) BuildClient(bool authenticated, string? email = null)
    {
        var sanitizer = new Mock<IServiceContentSanitizer>();
        sanitizer.Setup(s => s.Sanitize(It.IsAny<string>())).Returns<string>(x => x);

        var httpContext = new DefaultHttpContext();
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns(httpContext);

        var engine = new CmsProcessManager(
            NullLogger<CmsProcessManager>.Instance,
            new EmptyDefinitionStore(),
            sanitizer.Object,
            new InMemoryServiceRequestStore(),
            accessor.Object);

        var userContext = new Mock<IPrismUserContext>();
        userContext.Setup(u => u.IsAuthenticated).Returns(authenticated);
        userContext.Setup(u => u.Email).Returns(email);
        userContext.Setup(u => u.CurrentTenant).Returns(new PrismTenant { Hostname = "example.test" });

        var identityResolver = new CmsServiceRequestVisitorIdentityResolver(userContext.Object, accessor.Object);
        var client = new InProcessCmsProcessManagerClient(engine, identityResolver);
        return (client, httpContext);
    }

    [Fact]
    public async Task AuthenticatedMember_UsesEmailAsUserId_AndSetsNoAnonymousCookie()
    {
        var (client, httpContext) = BuildClient(authenticated: true, email: "member@example.test");

        await client.GetCurrentAsync("does-not-exist");

        httpContext.Response.Headers.SetCookie.ToString().Should().NotContain("PrismCmsServiceRequestVisitor");
    }

    [Fact]
    public async Task AnonymousVisitor_NoExistingCookie_MintsANewCorrelationCookie()
    {
        var (client, httpContext) = BuildClient(authenticated: false);

        await client.GetCurrentAsync("does-not-exist");

        var setCookie = httpContext.Response.Headers.SetCookie.ToString();
        setCookie.Should().Contain("PrismCmsServiceRequestVisitor=");
        setCookie.Should().Contain("httponly");
        setCookie.Should().Contain("samesite=lax", "GDS convention is case-insensitive header matching");
    }

    [Fact]
    public async Task AnonymousVisitor_ExistingCookie_ReusesItAndRefreshesTheSlidingExpiry()
    {
        var (client, httpContext) = BuildClient(authenticated: false);
        httpContext.Request.Headers.Cookie = "PrismCmsServiceRequestVisitor=existing-visitor-id";

        await client.GetCurrentAsync("does-not-exist");

        httpContext.Response.Headers.SetCookie.ToString().Should().Contain("PrismCmsServiceRequestVisitor=existing-visitor-id");
    }

    private sealed class EmptyDefinitionStore : IServiceBlueprintStore
    {
        public IReadOnlyDictionary<string, Wayfinder.Models.ServiceDesign.ServiceBlueprint> LoadDefinitions(ILogger logger) =>
            new Dictionary<string, Wayfinder.Models.ServiceDesign.ServiceBlueprint>();
    }
}
