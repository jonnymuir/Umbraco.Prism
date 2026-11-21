using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Infrastructure.Persistence;
using UmbracoPrism.Core.Models;
using UmbracoPrism.Core.Persistence;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Tests;

public class PrismPageAccessResolverTests
{
    [Fact]
    public void Resolve_ReturnsPublic_ForAContentNodeWithNoPolicy()
    {
        var resolver = CreateResolver(CreateDbFactory([]));

        var result = resolver.Resolve(Guid.NewGuid(), tenant: null, isAuthenticated: false);

        result.Should().Be(PrismPageAccessResult.Public);
    }

    [Fact]
    public void Resolve_ReturnsRequiresSignIn_WhenPolicyRequiresSignInAndVisitorIsAnonymous()
    {
        var contentKey = Guid.NewGuid();
        var resolver = CreateResolver(CreateDbFactory(
        [
            new PrismPageAccessPolicySchema { ContentKey = contentKey, RequiresSignIn = true }
        ]));

        var result = resolver.Resolve(contentKey, tenant: null, isAuthenticated: false);

        result.Should().Be(PrismPageAccessResult.RequiresSignIn);
    }

    [Fact]
    public void Resolve_ReturnsPublic_WhenPolicyRequiresSignInAndVisitorIsAuthenticated()
    {
        var contentKey = Guid.NewGuid();
        var resolver = CreateResolver(CreateDbFactory(
        [
            new PrismPageAccessPolicySchema { ContentKey = contentKey, RequiresSignIn = true }
        ]));

        var result = resolver.Resolve(contentKey, tenant: null, isAuthenticated: true);

        result.Should().Be(PrismPageAccessResult.Public);
    }

    [Fact]
    public void Resolve_ReturnsUnavailable_WhenTenantIsNotInTheAllowList()
    {
        var contentKey = Guid.NewGuid();
        var resolver = CreateResolver(CreateDbFactory(
        [
            new PrismPageAccessPolicySchema { ContentKey = contentKey, TenantAllowListJson = "[\"North\",\"South\"]" }
        ]));

        var result = resolver.Resolve(contentKey, tenant: new PrismTenant { Id = 3, Name = "East" }, isAuthenticated: true);

        result.Should().Be(PrismPageAccessResult.Unavailable);
    }

    [Fact]
    public void Resolve_ReturnsUnavailable_WhenAllowListIsRestrictedAndTenantIsNull()
    {
        var contentKey = Guid.NewGuid();
        var resolver = CreateResolver(CreateDbFactory(
        [
            new PrismPageAccessPolicySchema { ContentKey = contentKey, TenantAllowListJson = "[\"North\"]" }
        ]));

        var result = resolver.Resolve(contentKey, tenant: null, isAuthenticated: true);

        result.Should().Be(PrismPageAccessResult.Unavailable);
    }

    [Fact]
    public void Resolve_ReturnsPublic_WhenTenantIsInTheAllowList()
    {
        var contentKey = Guid.NewGuid();
        var resolver = CreateResolver(CreateDbFactory(
        [
            new PrismPageAccessPolicySchema { ContentKey = contentKey, TenantAllowListJson = "[\"North\",\"South\"]" }
        ]));

        var result = resolver.Resolve(contentKey, tenant: new PrismTenant { Id = 2, Name = "South" }, isAuthenticated: true);

        result.Should().Be(PrismPageAccessResult.Public);
    }

    [Fact]
    public void Resolve_MatchesTenantNameCaseInsensitively()
    {
        var contentKey = Guid.NewGuid();
        var resolver = CreateResolver(CreateDbFactory(
        [
            new PrismPageAccessPolicySchema { ContentKey = contentKey, TenantAllowListJson = "[\"North\"]" }
        ]));

        var result = resolver.Resolve(contentKey, tenant: new PrismTenant { Id = 1, Name = "NORTH" }, isAuthenticated: true);

        result.Should().Be(PrismPageAccessResult.Public);
    }

    [Fact]
    public void Resolve_PrefersUnavailableOverRequiresSignIn_WhenBothAxesAreViolated()
    {
        // Unavailable must win — a page a tenant isn't entitled to should 404, never reveal
        // that it would otherwise require sign-in.
        var contentKey = Guid.NewGuid();
        var resolver = CreateResolver(CreateDbFactory(
        [
            new PrismPageAccessPolicySchema { ContentKey = contentKey, RequiresSignIn = true, TenantAllowListJson = "[\"North\"]" }
        ]));

        var result = resolver.Resolve(contentKey, tenant: new PrismTenant { Id = 99, Name = "East" }, isAuthenticated: false);

        result.Should().Be(PrismPageAccessResult.Unavailable);
    }

    [Fact]
    public void Resolve_ServesFromCache_UntilInvalidated()
    {
        var contentKey = Guid.NewGuid();
        var dbFactory = CreateDbFactory(
        [
            new PrismPageAccessPolicySchema { ContentKey = contentKey, RequiresSignIn = true }
        ]);
        var resolver = CreateResolver(dbFactory);

        resolver.Resolve(contentKey, null, false);
        resolver.Resolve(contentKey, null, false);

        dbFactory.Verify(x => x.CreateDatabase(), Times.Once,
            "the whole policy set is cached after the first load");

        resolver.Invalidate("test");
        resolver.Resolve(contentKey, null, false);

        dbFactory.Verify(x => x.CreateDatabase(), Times.Exactly(2),
            "invalidation must force a fresh load on the next resolve");
    }

    [Fact]
    public void Resolve_FailsClosedOnADatabaseFailure_AndDoesNotCacheTheFailure()
    {
        // When the policy table cannot be read we cannot know which pages are protected, so the
        // page is denied rather than served. The failure is not cached, so a cold-boot race
        // against the table's migration clears itself on the next request.
        var db = new Mock<IUmbracoDatabase>();
        db.SetupSequence(x => x.Fetch<PrismPageAccessPolicySchema>())
            .Throws(new InvalidOperationException("no such table: prismPageAccessPolicies"))
            .Returns([]);

        var dbFactory = new Mock<IUmbracoDatabaseFactory>();
        dbFactory.Setup(x => x.CreateDatabase()).Returns(db.Object);

        var resolver = CreateResolver(dbFactory);

        var duringOutage = resolver.Resolve(Guid.NewGuid(), null, false);
        var afterRecovery = resolver.Resolve(Guid.NewGuid(), null, false);

        duringOutage.Should().Be(PrismPageAccessResult.Unavailable, "a protected page must never be served when the policy lookup fails");
        afterRecovery.Should().Be(PrismPageAccessResult.Public);
        dbFactory.Verify(x => x.CreateDatabase(), Times.Exactly(2),
            "the failed load must not have been cached, so the next resolve retries");
    }

    private static Mock<IUmbracoDatabaseFactory> CreateDbFactory(PrismPageAccessPolicySchema[] rows)
    {
        var db = new Mock<IUmbracoDatabase>();
        db.Setup(x => x.Fetch<PrismPageAccessPolicySchema>()).Returns(rows.ToList());

        var dbFactory = new Mock<IUmbracoDatabaseFactory>();
        dbFactory.Setup(x => x.CreateDatabase()).Returns(db.Object);
        return dbFactory;
    }

    private static PrismPageAccessResolver CreateResolver(Mock<IUmbracoDatabaseFactory> dbFactory)
    {
        var runtimeCache = new ObjectCacheAppCache();
        var requestCache = new Mock<IRequestCache>();
        var isolatedCaches = new IsolatedCaches(_ => new ObjectCacheAppCache());
        var appCaches = new AppCaches(runtimeCache, requestCache.Object, isolatedCaches);

        return new PrismPageAccessResolver(dbFactory.Object, appCaches, NullLogger<PrismPageAccessResolver>.Instance);
    }
}
