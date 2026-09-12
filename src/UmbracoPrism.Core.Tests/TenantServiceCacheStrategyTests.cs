using System.Collections.Concurrent;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Infrastructure.Persistence;
using UmbracoPrism.Core.Persistence;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Tests;

public class TenantServiceCacheStrategyTests
{
    [Fact]
    public async Task GetByDomainAsync_TracksMissThenHit_ForSameTenantDomain()
    {
        var db = new Mock<IUmbracoDatabase>();
        db.Setup(x => x.FirstOrDefault<PrismTenantSchema>(It.IsAny<string>(), It.IsAny<object[]>()))
            .Returns(new PrismTenantSchema
            {
                Id = 1,
                Name = "Tenant A",
                Hostname = "tenant-a.example.com",
                EntraTenantId = "entra-a",
                EntraClientId = "client-a",
                SecretKeyName = "secret-a"
            });

        var dbFactory = new Mock<IUmbracoDatabaseFactory>();
        dbFactory.Setup(x => x.CreateDatabase()).Returns(db.Object);

        var service = CreateTenantService(dbFactory.Object);

        var first = await service.GetByDomainAsync("tenant-a.example.com");
        var second = await service.GetByDomainAsync("tenant-a.example.com");

        first.Should().NotBeNull();
        second.Should().NotBeNull();

        var metrics = service.GetCacheMetrics();
        metrics.Misses.Should().Be(1);
        metrics.Hits.Should().Be(1);
        metrics.DatabaseLoads.Should().Be(1);

        dbFactory.Verify(x => x.CreateDatabase(), Times.Once);
    }

    [Fact]
    public void InvalidateDomains_HandlesHighTenantVolumeAndDeduplicates()
    {
        var dbFactory = new Mock<IUmbracoDatabaseFactory>();
        var service = CreateTenantService(dbFactory.Object);

        var distinctHosts = Enumerable.Range(1, 2000)
            .Select(i => $"tenant-{i}.example.com")
            .ToArray();

        var withDuplicates = distinctHosts
            .Concat(distinctHosts.Take(500))
            .Concat(new[] { "TENANT-1.EXAMPLE.COM", "tenant-2.example.com", "  tenant-3.example.com  " })
            .ToArray();

        service.InvalidateDomains(withDuplicates, "high-tenant-stress");

        var metrics = service.GetCacheMetrics();
        metrics.Invalidations.Should().Be(distinctHosts.Length);
    }

    [Fact]
    public async Task InvalidateDomain_ForcesFreshLoadOnNextLookup()
    {
        var firstLoad = new PrismTenantSchema
        {
            Id = 1,
            Name = "Tenant A",
            Hostname = "tenant-a.example.com",
            EntraTenantId = "entra-a",
            EntraClientId = "client-a",
            SecretKeyName = "secret-a",
            BrandingOverrides = "{\"--prism-primary\":\" #111111 \",\"--prism-radius\":\" 8px \"}",
            MobileBrandingOverrides = "{\"--prism-primary\":\" #222222 \"}"
        };

        var secondLoad = new PrismTenantSchema
        {
            Id = 1,
            Name = "Tenant A Updated",
            Hostname = "tenant-a.example.com",
            EntraTenantId = "entra-a",
            EntraClientId = "client-a",
            SecretKeyName = "secret-a",
            BrandingOverrides = "{\"--prism-primary\":\" #333333 \",\"--prism-radius\":\" 12px \"}",
            MobileBrandingOverrides = "{\"--prism-primary\":\" #444444 \"}"
        };

        var db = new Mock<IUmbracoDatabase>();
        db.SetupSequence(x => x.FirstOrDefault<PrismTenantSchema>(It.IsAny<string>(), It.IsAny<object[]>()))
            .Returns(firstLoad)
            .Returns(secondLoad);

        var dbFactory = new Mock<IUmbracoDatabaseFactory>();
        dbFactory.Setup(x => x.CreateDatabase()).Returns(db.Object);

        var service = CreateTenantService(dbFactory.Object);

        var cached = await service.GetByDomainAsync("tenant-a.example.com");
        service.InvalidateDomain("tenant-a.example.com", "tenant-update");
        var refreshed = await service.GetByDomainAsync("tenant-a.example.com");

        cached!.Name.Should().Be("Tenant A");
        cached.BrandingCssDeclarations.Should().Be("--prism-primary:#111111;--prism-radius:8px;");
        cached.MobileBrandingCssDeclarations.Should().Be("--prism-primary:#222222;");

        refreshed!.Name.Should().Be("Tenant A Updated");
        refreshed.BrandingCssDeclarations.Should().Be("--prism-primary:#333333;--prism-radius:12px;");
        refreshed.MobileBrandingCssDeclarations.Should().Be("--prism-primary:#444444;");

        var metrics = service.GetCacheMetrics();
        metrics.Misses.Should().Be(2);
        metrics.Invalidations.Should().Be(1);
        metrics.DatabaseLoads.Should().Be(2);
        dbFactory.Verify(x => x.CreateDatabase(), Times.Exactly(2));
    }

    [Fact]
    public async Task GetByDomainAsync_IgnoresBlankBrandingKeys_WhenBuildingCssDeclarations()
    {
        var db = new Mock<IUmbracoDatabase>();
        db.Setup(x => x.FirstOrDefault<PrismTenantSchema>(It.IsAny<string>(), It.IsAny<object[]>()))
            .Returns(new PrismTenantSchema
            {
                Id = 7,
                Name = "Tenant B",
                Hostname = "tenant-b.example.com",
                EntraTenantId = "entra-b",
                EntraClientId = "client-b",
                SecretKeyName = "secret-b",
                BrandingOverrides = "{\"--prism-primary\":\" #0055ff \",\"\":\"red\",\"--prism-empty\":\" \"}",
                MobileBrandingOverrides = "{\"--prism-mobile\":\" #112233 \"}"
            });

        var dbFactory = new Mock<IUmbracoDatabaseFactory>();
        dbFactory.Setup(x => x.CreateDatabase()).Returns(db.Object);

        var service = CreateTenantService(dbFactory.Object);

        var tenant = await service.GetByDomainAsync("tenant-b.example.com");

        tenant.Should().NotBeNull();
        tenant!.BrandingCssDeclarations.Should().Be("--prism-primary:#0055ff;");
        tenant.MobileBrandingCssDeclarations.Should().Be("--prism-mobile:#112233;");
    }

    [Fact]
    public async Task GetByDomainAsync_DropsBrandingOverride_ContainingStyleTagBreakout()
    {
        // SECURITY: BrandingCssDeclarations is rendered unescaped as the body of a real
        // text/css response (PrismBrandingAssetsController.BrandingCss). Serving it as its own
        // resource — rather than splicing it inline into a <style> tag on every page, as it
        // used to be — already closes off the classic "</style>" breakout into surrounding
        // HTML/script. This validation is defence-in-depth on top of that: a malformed or
        // malicious value here would still corrupt the tenant's live stylesheet (e.g. via a raw
        // CSS comment breakout affecting rules that follow), so it must never reach the
        // rendered declaration string regardless of delivery mechanism.
        var db = new Mock<IUmbracoDatabase>();
        db.Setup(x => x.FirstOrDefault<PrismTenantSchema>(It.IsAny<string>(), It.IsAny<object[]>()))
            .Returns(new PrismTenantSchema
            {
                Id = 8,
                Name = "Tenant C",
                Hostname = "tenant-c.example.com",
                EntraTenantId = "entra-c",
                EntraClientId = "client-c",
                SecretKeyName = "secret-c",
                BrandingOverrides = "{\"--prism-primary\":\"red}</style><script>alert(1)</script>\",\"--prism-radius\":\"8px\"}"
            });

        var dbFactory = new Mock<IUmbracoDatabaseFactory>();
        dbFactory.Setup(x => x.CreateDatabase()).Returns(db.Object);

        var service = CreateTenantService(dbFactory.Object);

        var tenant = await service.GetByDomainAsync("tenant-c.example.com");

        tenant.Should().NotBeNull();
        tenant!.BrandingCssDeclarations.Should().NotContain("</style>").And.NotContain("<script>");
        tenant.BrandingCssDeclarations.Should().Be("--prism-radius:8px;",
            "because the malicious pair must be dropped entirely, not merely truncated or encoded");
    }

    [Fact]
    public async Task InvalidateDomains_AllowsConcurrentTenantRenameRefresh_WithoutServingStaleHostname()
    {
        var schemasByHost = new ConcurrentDictionary<string, PrismTenantSchema>(StringComparer.OrdinalIgnoreCase);
        schemasByHost["old.example.com"] = new PrismTenantSchema
        {
            Id = 9,
            Name = "Tenant Rename",
            Hostname = "old.example.com",
            EntraTenantId = "entra-a",
            EntraClientId = "client-a",
            SecretKeyName = "secret-a",
            BrandingOverrides = "{\"--prism-primary\":\"#111111\"}"
        };

        var db = new Mock<IUmbracoDatabase>();
        db.Setup(x => x.FirstOrDefault<PrismTenantSchema>(It.IsAny<string>(), It.IsAny<object[]>()))
            .Returns((string _, object[] args) =>
            {
                var domain = args[0].ToString()!;
                return schemasByHost.TryGetValue(domain, out var schema)
                    ? CloneSchema(schema)
                    : null;
            });

        var dbFactory = new Mock<IUmbracoDatabaseFactory>();
        dbFactory.Setup(x => x.CreateDatabase()).Returns(db.Object);

        var service = CreateTenantService(dbFactory.Object);

        var cached = await service.GetByDomainAsync("old.example.com");
        cached.Should().NotBeNull();
        cached!.Hostname.Should().Be("old.example.com");

        schemasByHost.TryRemove("old.example.com", out _);
        schemasByHost["new.example.com"] = new PrismTenantSchema
        {
            Id = 9,
            Name = "Tenant Rename Updated",
            Hostname = "new.example.com",
            EntraTenantId = "entra-a",
            EntraClientId = "client-a",
            SecretKeyName = "secret-a",
            BrandingOverrides = "{\"--prism-primary\":\"#222222\"}",
            MobileBrandingOverrides = "{\"--prism-primary\":\"#333333\"}"
        };

        await Task.WhenAll(
            Enumerable.Range(0, 32)
                .Select(_ => Task.Run(() =>
                    service.InvalidateDomains(
                        ["old.example.com", "new.example.com", " OLD.EXAMPLE.COM "],
                        "tenant-update"))));

        var staleHost = await service.GetByDomainAsync("old.example.com");
        var refreshedHost = await service.GetByDomainAsync("new.example.com");

        staleHost.Should().BeNull();
        refreshedHost.Should().NotBeNull();
        refreshedHost!.Hostname.Should().Be("new.example.com");
        refreshedHost.Name.Should().Be("Tenant Rename Updated");
        refreshedHost.BrandingCssDeclarations.Should().Be("--prism-primary:#222222;");
        refreshedHost.MobileBrandingCssDeclarations.Should().Be("--prism-primary:#333333;");
    }

    [Fact]
    public async Task GetByDomainAsync_ReturnsCoherentTenantSnapshots_DuringConcurrentBrandingUpdateRace()
    {
        var currentSchema = CloneSchema(new PrismTenantSchema
        {
            Id = 11,
            Name = "Tenant A",
            Hostname = "tenant-a.example.com",
            EntraTenantId = "entra-a",
            EntraClientId = "client-a",
            SecretKeyName = "secret-a",
            BrandingOverrides = "{\"--prism-primary\":\"#111111\"}",
            MobileBrandingOverrides = "{\"--prism-primary\":\"#aaaaaa\"}"
        });

        var db = new Mock<IUmbracoDatabase>();
        db.Setup(x => x.FirstOrDefault<PrismTenantSchema>(It.IsAny<string>(), It.IsAny<object[]>()))
            .Returns(() => CloneSchema(currentSchema));

        var dbFactory = new Mock<IUmbracoDatabaseFactory>();
        dbFactory.Setup(x => x.CreateDatabase()).Returns(db.Object);

        var service = CreateTenantService(dbFactory.Object);

        var initial = await service.GetByDomainAsync("tenant-a.example.com");
        initial.Should().NotBeNull();

        currentSchema = CloneSchema(new PrismTenantSchema
        {
            Id = 11,
            Name = "Tenant A Updated",
            Hostname = "tenant-a.example.com",
            EntraTenantId = "entra-a",
            EntraClientId = "client-a",
            SecretKeyName = "secret-a",
            BrandingOverrides = "{\"--prism-primary\":\"#222222\"}",
            MobileBrandingOverrides = "{\"--prism-primary\":\"#bbbbbb\"}"
        });

        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = Enumerable.Range(0, 40)
            .Select(index => Task.Run(async () =>
            {
                await start.Task;

                if (index % 3 == 0)
                {
                    service.InvalidateDomain("tenant-a.example.com", "branding-update-race");
                }

                var tenant = await service.GetByDomainAsync("tenant-a.example.com");
                return new
                {
                    tenant!.Name,
                    tenant.Hostname,
                    tenant.BrandingCssDeclarations,
                    tenant.MobileBrandingCssDeclarations
                };
            }))
            .ToArray();

        start.TrySetResult();
        var snapshots = await Task.WhenAll(tasks);

        snapshots.Should().OnlyContain(snapshot =>
            (snapshot.Name == "Tenant A"
                && snapshot.Hostname == "tenant-a.example.com"
                && snapshot.BrandingCssDeclarations == "--prism-primary:#111111;"
                && snapshot.MobileBrandingCssDeclarations == "--prism-primary:#aaaaaa;")
            || (snapshot.Name == "Tenant A Updated"
                && snapshot.Hostname == "tenant-a.example.com"
                && snapshot.BrandingCssDeclarations == "--prism-primary:#222222;"
                && snapshot.MobileBrandingCssDeclarations == "--prism-primary:#bbbbbb;"));

        service.InvalidateDomain("tenant-a.example.com", "post-race-refresh");
        var refreshed = await service.GetByDomainAsync("tenant-a.example.com");

        refreshed.Should().NotBeNull();
        refreshed!.Name.Should().Be("Tenant A Updated");
        refreshed.BrandingCssDeclarations.Should().Be("--prism-primary:#222222;");
        refreshed.MobileBrandingCssDeclarations.Should().Be("--prism-primary:#bbbbbb;");
    }

    [Fact]
    public async Task GetByDomainAsync_TreatsADatabaseFailureDuringLookupAsAnUnresolvedTenant()
    {
        // Regression for a real production/CI symptom: PrismTenantMiddleware calls this for
        // EVERY request, before any controller runs. A cold-boot race (a request reaching this
        // middleware before Umbraco's own migration gate has finished creating PrismTenants — see
        // PrismMigrationPlan) previously surfaced as an unhandled "no such table: PrismTenants"
        // exception here, 500-ing the whole request. This must never crash the request — it
        // should behave exactly like an unrecognized host.
        var db = new Mock<IUmbracoDatabase>();
        db.Setup(x => x.FirstOrDefault<PrismTenantSchema>(It.IsAny<string>(), It.IsAny<object[]>()))
            .Throws(new InvalidOperationException("no such table: PrismTenants"));

        var dbFactory = new Mock<IUmbracoDatabaseFactory>();
        dbFactory.Setup(x => x.CreateDatabase()).Returns(db.Object);

        var service = CreateTenantService(dbFactory.Object);

        var tenant = await service.GetByDomainAsync("tenant-a.example.com");

        tenant.Should().BeNull();
    }

    [Fact]
    public async Task GetByDomainAsync_DoesNotCacheADatabaseFailure_SoTheNextLookupRetries()
    {
        var db = new Mock<IUmbracoDatabase>();
        db.SetupSequence(x => x.FirstOrDefault<PrismTenantSchema>(It.IsAny<string>(), It.IsAny<object[]>()))
            .Throws(new InvalidOperationException("no such table: PrismTenants"))
            .Returns(new PrismTenantSchema
            {
                Id = 1,
                Name = "Tenant A",
                Hostname = "tenant-a.example.com",
                EntraTenantId = "entra-a",
                EntraClientId = "client-a",
                SecretKeyName = "secret-a"
            });

        var dbFactory = new Mock<IUmbracoDatabaseFactory>();
        dbFactory.Setup(x => x.CreateDatabase()).Returns(db.Object);

        var service = CreateTenantService(dbFactory.Object);

        var duringOutage = await service.GetByDomainAsync("tenant-a.example.com");
        var afterRecovery = await service.GetByDomainAsync("tenant-a.example.com");

        duringOutage.Should().BeNull("the table didn't exist yet");
        afterRecovery.Should().NotBeNull("the failed lookup must not have been cached as a permanent negative result");
        afterRecovery!.Name.Should().Be("Tenant A");
    }

    private static PrismTenantSchema CloneSchema(PrismTenantSchema schema) =>
        new()
        {
            Id = schema.Id,
            Name = schema.Name,
            Hostname = schema.Hostname,
            EntraTenantId = schema.EntraTenantId,
            EntraClientId = schema.EntraClientId,
            SecretKeyName = schema.SecretKeyName,
            BrandingOverrides = schema.BrandingOverrides,
            MobileBrandingOverrides = schema.MobileBrandingOverrides
        };

    private static TenantService CreateTenantService(IUmbracoDatabaseFactory databaseFactory)
    {
        var runtimeCache = new ObjectCacheAppCache();
        var requestCache = new Mock<IRequestCache>();
        var isolatedCaches = new IsolatedCaches(_ => new ObjectCacheAppCache());
        var appCaches = new AppCaches(runtimeCache, requestCache.Object, isolatedCaches);

        return new TenantService(databaseFactory, appCaches, Mock.Of<ITenantTokenResolver>(), NullLogger<TenantService>.Instance);
    }
}
