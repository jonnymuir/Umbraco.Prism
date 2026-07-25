using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using UmbracoPrism.Core.Models;
using UmbracoPrism.Core.Services;
using UmbracoPrism.Core.Services.Workflow;
using UmbracoPrism.Shared.Services.Sanitization;
using UmbracoPrism.WorkflowRuntime.Abstractions;
using UmbracoPrism.WorkflowRuntime.Stores;

namespace UmbracoPrism.Core.Tests.Workflow.Runtime;

/// <summary>
/// Verifies <see cref="InProcessCmsWorkflowClient"/>'s identity resolution — the piece that
/// makes "only available to the user who initiated it" true for anonymous visitors, and lets a
/// logged-in Prism Member's identity flow through to <c>CmsWorkflowEngine.ResolveServiceInputs</c>
/// without hardcoding any claims-handling in a per-host controller override.
/// </summary>
public class InProcessCmsWorkflowClientTests
{
    private static (InProcessCmsWorkflowClient Client, DefaultHttpContext HttpContext) BuildClient(bool authenticated, string? email = null)
    {
        var sanitizer = new Mock<IWorkflowContentSanitizer>();
        sanitizer.Setup(s => s.Sanitize(It.IsAny<string>())).Returns<string>(x => x);

        var httpContext = new DefaultHttpContext();
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns(httpContext);

        var engine = new CmsWorkflowEngine(
            NullLogger<CmsWorkflowEngine>.Instance,
            new EmptyDefinitionStore(),
            sanitizer.Object,
            new InMemoryWorkflowInstanceStore(),
            accessor.Object);

        var userContext = new Mock<IPrismUserContext>();
        userContext.Setup(u => u.IsAuthenticated).Returns(authenticated);
        userContext.Setup(u => u.Email).Returns(email);
        userContext.Setup(u => u.CurrentTenant).Returns(new PrismTenant { Hostname = "example.test" });

        var identityResolver = new CmsWorkflowVisitorIdentityResolver(userContext.Object, accessor.Object);
        var client = new InProcessCmsWorkflowClient(engine, identityResolver);
        return (client, httpContext);
    }

    [Fact]
    public async Task AuthenticatedMember_UsesEmailAsUserId_AndSetsNoAnonymousCookie()
    {
        var (client, httpContext) = BuildClient(authenticated: true, email: "member@example.test");

        await client.GetCurrentAsync("does-not-exist");

        httpContext.Response.Headers.SetCookie.ToString().Should().NotContain("PrismCmsWorkflowVisitor");
    }

    [Fact]
    public async Task AnonymousVisitor_NoExistingCookie_MintsANewCorrelationCookie()
    {
        var (client, httpContext) = BuildClient(authenticated: false);

        await client.GetCurrentAsync("does-not-exist");

        var setCookie = httpContext.Response.Headers.SetCookie.ToString();
        setCookie.Should().Contain("PrismCmsWorkflowVisitor=");
        setCookie.Should().Contain("httponly");
        setCookie.Should().Contain("samesite=lax", "GDS convention is case-insensitive header matching");
    }

    [Fact]
    public async Task AnonymousVisitor_ExistingCookie_ReusesItAndRefreshesTheSlidingExpiry()
    {
        var (client, httpContext) = BuildClient(authenticated: false);
        httpContext.Request.Headers.Cookie = "PrismCmsWorkflowVisitor=existing-visitor-id";

        await client.GetCurrentAsync("does-not-exist");

        httpContext.Response.Headers.SetCookie.ToString().Should().Contain("PrismCmsWorkflowVisitor=existing-visitor-id");
    }

    private sealed class EmptyDefinitionStore : IWorkflowDefinitionStore
    {
        public IReadOnlyDictionary<string, UmbracoPrism.Shared.Models.Workflow.WorkflowDefinitionFile> LoadDefinitions(ILogger logger) =>
            new Dictionary<string, UmbracoPrism.Shared.Models.Workflow.WorkflowDefinitionFile>();
    }
}
