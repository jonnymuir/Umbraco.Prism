using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Security;
using UmbracoPrism.Core.Auth;
using UmbracoPrism.Core.Models;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Tests;

public class PrismAuthorizationHandlersTests
{
    [Fact]
    public async Task PrismAdminHandler_DoesNotSucceed_WhenCurrentUserIsNull()
    {
        var securityAccessor = new Mock<IBackOfficeSecurityAccessor>();
        var backOfficeSecurity = new Mock<IBackOfficeSecurity>();
        backOfficeSecurity.SetupGet(x => x.CurrentUser).Returns((IUser?)null);
        securityAccessor.SetupGet(x => x.BackOfficeSecurity).Returns(backOfficeSecurity.Object);

        var handler = new PrismAdminHandler(
            securityAccessor.Object,
            Options.Create(new PrismAdminOptions { GroupAliases = ["admin"] }));

        var context = CreateAdminContext();

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task PrismAdminHandler_DoesNotSucceed_WhenGroupAliasesIsEmpty()
    {
        var securityAccessor = BuildSecurityAccessorWithUserGroups("admin");

        var handler = new PrismAdminHandler(
            securityAccessor.Object,
            Options.Create(new PrismAdminOptions { GroupAliases = [] }));

        var context = CreateAdminContext();

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task PrismAdminHandler_Succeeds_WhenUserHasAllowedGroupAlias()
    {
        var securityAccessor = BuildSecurityAccessorWithUserGroups("Admin");

        var handler = new PrismAdminHandler(
            securityAccessor.Object,
            Options.Create(new PrismAdminOptions { GroupAliases = ["admin"] }));

        var context = CreateAdminContext();

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task PrismAdminHandler_DoesNotSucceed_WhenUserLacksAllowedGroupAlias()
    {
        var securityAccessor = BuildSecurityAccessorWithUserGroups("editor");

        var handler = new PrismAdminHandler(
            securityAccessor.Object,
            Options.Create(new PrismAdminOptions { GroupAliases = ["admin"] }));

        var context = CreateAdminContext();

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task PrismTenantHandler_Succeeds_WhenAuthenticatedAndTenantMatches()
    {
        var userContext = new Mock<IPrismUserContext>();
        userContext.SetupGet(x => x.IsAuthenticated).Returns(true);
        userContext.SetupGet(x => x.EntraTenantId).Returns("tenant-a");
        userContext.SetupGet(x => x.CurrentTenant).Returns(new PrismTenant { EntraTenantId = "tenant-a" });

        var handler = new PrismTenantHandler(BuildHttpContextAccessor(userContext.Object));
        var context = CreateTenantContext();

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task PrismTenantHandler_DoesNotSucceed_WhenAuthenticatedAndTenantMismatches()
    {
        var userContext = new Mock<IPrismUserContext>();
        userContext.SetupGet(x => x.IsAuthenticated).Returns(true);
        userContext.SetupGet(x => x.EntraTenantId).Returns("tenant-a");
        userContext.SetupGet(x => x.CurrentTenant).Returns(new PrismTenant { EntraTenantId = "tenant-b" });

        var handler = new PrismTenantHandler(BuildHttpContextAccessor(userContext.Object));
        var context = CreateTenantContext();

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task PrismTenantHandler_DoesNotSucceed_WhenNotAuthenticated()
    {
        var userContext = new Mock<IPrismUserContext>();
        userContext.SetupGet(x => x.IsAuthenticated).Returns(false);
        userContext.SetupGet(x => x.EntraTenantId).Returns("tenant-a");
        userContext.SetupGet(x => x.CurrentTenant).Returns(new PrismTenant { EntraTenantId = "tenant-a" });

        var handler = new PrismTenantHandler(BuildHttpContextAccessor(userContext.Object));
        var context = CreateTenantContext();

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task PrismTenantHandler_DoesNotSucceed_WhenCurrentTenantIsMissing()
    {
        var userContext = new Mock<IPrismUserContext>();
        userContext.SetupGet(x => x.IsAuthenticated).Returns(true);
        userContext.SetupGet(x => x.EntraTenantId).Returns("tenant-a");
        userContext.SetupGet(x => x.CurrentTenant).Returns((PrismTenant?)null);

        var handler = new PrismTenantHandler(BuildHttpContextAccessor(userContext.Object));
        var context = CreateTenantContext();

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task PrismTenantHandler_DoesNotSucceed_WhenUserTenantIdIsMissing()
    {
        var userContext = new Mock<IPrismUserContext>();
        userContext.SetupGet(x => x.IsAuthenticated).Returns(true);
        userContext.SetupGet(x => x.EntraTenantId).Returns((string?)null);
        userContext.SetupGet(x => x.CurrentTenant).Returns(new PrismTenant { EntraTenantId = "tenant-a" });

        var handler = new PrismTenantHandler(BuildHttpContextAccessor(userContext.Object));
        var context = CreateTenantContext();

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }

    private static AuthorizationHandlerContext CreateAdminContext() =>
        new([new PrismAdminRequirement()],
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user")], "Test")),
            resource: null);

    private static AuthorizationHandlerContext CreateTenantContext() =>
        new([new PrismTenantRequirement()],
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user")], "Test")),
            resource: null);

    private static IHttpContextAccessor BuildHttpContextAccessor(IPrismUserContext userContext)
    {
        var services = new ServiceCollection()
            .AddSingleton(userContext)
            .BuildServiceProvider();

        return new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { RequestServices = services }
        };
    }

    private static Mock<IBackOfficeSecurityAccessor> BuildSecurityAccessorWithUserGroups(params string[] aliases)
    {
        var groups = aliases
            .Select(alias =>
            {
                var group = new Mock<IReadOnlyUserGroup>();
                group.SetupGet(x => x.Alias).Returns(alias);
                return group.Object;
            })
            .ToArray();

        var user = new Mock<IUser>();
        user.SetupGet(x => x.Groups).Returns(groups);

        var backOfficeSecurity = new Mock<IBackOfficeSecurity>();
        backOfficeSecurity.SetupGet(x => x.CurrentUser).Returns(user.Object);

        var securityAccessor = new Mock<IBackOfficeSecurityAccessor>();
        securityAccessor.SetupGet(x => x.BackOfficeSecurity).Returns(backOfficeSecurity.Object);
        return securityAccessor;
    }
}
