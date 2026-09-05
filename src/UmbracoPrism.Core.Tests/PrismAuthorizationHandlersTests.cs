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
    public async Task PrismTenantHandler_Succeeds_WhenAuthenticatedAndEntraTenantMatches()
    {
        var prismContext = BuildPrismContext(new PrismTenant { EntraTenantId = "tenant-a" });
        var principal = CreateAuthenticatedPrincipal(new Claim("tid", "tenant-a"));

        var handler = new PrismTenantHandler(BuildAccessor(prismContext.Object), new PrismTenantBindingValidator());
        var context = CreateTenantContext(principal);

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task PrismTenantHandler_DoesNotSucceed_WhenAuthenticatedAndEntraTenantMismatches()
    {
        var prismContext = BuildPrismContext(new PrismTenant { EntraTenantId = "tenant-b" });
        var principal = CreateAuthenticatedPrincipal(new Claim("tid", "tenant-a"));

        var handler = new PrismTenantHandler(BuildAccessor(prismContext.Object), new PrismTenantBindingValidator());
        var context = CreateTenantContext(principal);

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task PrismTenantHandler_Succeeds_WhenAuthenticatedAndGenericOidcTenantMatches()
    {
        // Regression coverage for the bug this handler used to have: it compared Entra `tid`
        // claims only, so it could never succeed for a Keycloak/generic-OIDC tenant even when
        // the principal genuinely belonged to it.
        var prismContext = BuildPrismContext(new PrismTenant
        {
            OidcAuthority = "https://keycloak.example/realms/acme-a",
            OidcClientId = "acme-a-client"
        });
        var principal = CreateAuthenticatedPrincipal(
            new Claim("iss", "https://keycloak.example/realms/acme-a"),
            new Claim("aud", "acme-a-client"));

        var handler = new PrismTenantHandler(BuildAccessor(prismContext.Object), new PrismTenantBindingValidator());
        var context = CreateTenantContext(principal);

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task PrismTenantHandler_DoesNotSucceed_WhenGenericOidcTenantMismatches()
    {
        // The cross-tenant replay scenario: a session minted for acme-b's Keycloak realm,
        // presented on a request that resolved to acme-a.
        var prismContext = BuildPrismContext(new PrismTenant
        {
            OidcAuthority = "https://keycloak.example/realms/acme-a",
            OidcClientId = "acme-a-client"
        });
        var principal = CreateAuthenticatedPrincipal(
            new Claim("iss", "https://keycloak.example/realms/acme-b"),
            new Claim("aud", "acme-b-client"));

        var handler = new PrismTenantHandler(BuildAccessor(prismContext.Object), new PrismTenantBindingValidator());
        var context = CreateTenantContext(principal);

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task PrismTenantHandler_DoesNotSucceed_WhenNotAuthenticated()
    {
        var prismContext = BuildPrismContext(new PrismTenant { EntraTenantId = "tenant-a" });
        var principal = CreateUnauthenticatedPrincipal(new Claim("tid", "tenant-a"));

        var handler = new PrismTenantHandler(BuildAccessor(prismContext.Object), new PrismTenantBindingValidator());
        var context = CreateTenantContext(principal);

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task PrismTenantHandler_DoesNotSucceed_WhenCurrentTenantIsMissing()
    {
        var prismContext = BuildPrismContext(currentTenant: null);
        var principal = CreateAuthenticatedPrincipal(new Claim("tid", "tenant-a"));

        var handler = new PrismTenantHandler(BuildAccessor(prismContext.Object), new PrismTenantBindingValidator());
        var context = CreateTenantContext(principal);

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task PrismTenantHandler_DoesNotSucceed_WhenUserTenantIdIsMissing()
    {
        var prismContext = BuildPrismContext(new PrismTenant { EntraTenantId = "tenant-a" });
        var principal = CreateAuthenticatedPrincipal();

        var handler = new PrismTenantHandler(BuildAccessor(prismContext.Object), new PrismTenantBindingValidator());
        var context = CreateTenantContext(principal);

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }

    private static AuthorizationHandlerContext CreateAdminContext() =>
        new([new PrismAdminRequirement()],
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user")], "Test")),
            resource: null);

    private static AuthorizationHandlerContext CreateTenantContext(ClaimsPrincipal principal) =>
        new([new PrismTenantRequirement()], principal, resource: null);

    private static ClaimsPrincipal CreateAuthenticatedPrincipal(params Claim[] claims) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user"), .. claims], "Test"));

    private static ClaimsPrincipal CreateUnauthenticatedPrincipal(params Claim[] claims) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user"), .. claims]));

    private static Mock<IPrismContext> BuildPrismContext(PrismTenant? currentTenant)
    {
        var prismContext = new Mock<IPrismContext>();
        prismContext.SetupGet(x => x.CurrentTenant).Returns(currentTenant);
        return prismContext;
    }

    private static IHttpContextAccessor BuildAccessor(IPrismContext prismContext)
    {
        var services = new ServiceCollection()
            .AddSingleton(prismContext)
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
