using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Umbraco.Cms.Infrastructure.Persistence;
using UmbracoPrism.Core.Controllers;
using UmbracoPrism.Core.Controllers.Models;
using UmbracoPrism.Core.Persistence;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Tests;

public class TenantManagementControllerTests
{
    [Fact]
    public void GetTenants_DoesNotEchoOidcClientSecretReferences()
    {
        var tenants = new List<PrismTenantSchema>
        {
            new()
            {
                Id = 7,
                Name = "Northwind",
                Hostname = "northwind.example",
                OidcAuthority = "https://auth.example.com/realms/northwind",
                OidcClientId = "northwind-portal",
                OidcClientSecretProvider = PrismSecretProviderNames.AzureKeyVault,
                OidcClientSecretReference = "northwind-oidc-secret"
            }
        };

        var (controller, _, _, _) = BuildController(db =>
        {
            db.Setup(database => database.Fetch<PrismTenantSchema>())
                .Returns(tenants);
        });

        var result = controller.GetTenants().Result.Should().BeOfType<OkObjectResult>().Subject;
        var responseTenants = result.Value.Should().BeAssignableTo<IEnumerable<PrismTenantResponse>>().Subject.ToList();

        responseTenants.Should().ContainSingle();
        responseTenants[0].OidcClientSecretProvider.Should().Be(PrismSecretProviderNames.AzureKeyVault);
        responseTenants[0].HasOidcClientSecret.Should().BeTrue();
        responseTenants[0].SecretKeyName.Should().BeNull();
        tenants[0].OidcClientSecretReference.Should().Be("northwind-oidc-secret");
    }

    [Fact]
    public void CreateTenant_RejectsGenericOidcTenants_WithoutSecretProviderReference()
    {
        var request = new PrismTenantRequest
        {
            Name = "Northwind",
            Hostname = "northwind.example",
            OidcAuthority = "https://auth.example.com/realms/northwind",
            OidcClientId = "northwind-portal"
        };

        var (controller, db, tenantService, _) = BuildController();

        var result = controller.CreateTenant(request).Should().BeOfType<BadRequestObjectResult>().Subject;

        result.Value.Should().NotBeNull();
        tenantService.Verify(service => service.InvalidateDomain(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        db.Verify(database => database.Insert(It.IsAny<PrismTenantSchema>()), Times.Never);
    }

    [Fact]
    public void CreateTenant_PersistsKeyVaultSecretReference_AndDoesNotEchoIt()
    {
        PrismTenantSchema? inserted = null;
        var request = new PrismTenantRequest
        {
            Name = "Northwind",
            Hostname = "northwind.example",
            OidcAuthority = "https://auth.example.com/realms/northwind",
            OidcClientId = "northwind-portal",
            SecretKeyName = "northwind-oidc-secret"
        };

        var (controller, db, tenantService, _) = BuildController(database =>
        {
            database.Setup(d => d.Insert(It.IsAny<PrismTenantSchema>()))
                .Callback<object>(record => inserted = record as PrismTenantSchema)
                .Returns(new object());
        });

        var result = controller.CreateTenant(request).Should().BeOfType<OkObjectResult>().Subject;
        var responseTenant = result.Value.Should().BeOfType<PrismTenantResponse>().Subject;

        inserted.Should().NotBeNull();
        inserted!.OidcClientSecretProvider.Should().Be(PrismSecretProviderNames.AzureKeyVault);
        inserted.OidcClientSecretReference.Should().Be("northwind-oidc-secret");
        inserted.SecretKeyName.Should().BeNull();
        responseTenant.OidcClientSecretProvider.Should().Be(PrismSecretProviderNames.AzureKeyVault);
        responseTenant.HasOidcClientSecret.Should().BeTrue();
        responseTenant.SecretKeyName.Should().BeNull();
        tenantService.Verify(service => service.InvalidateDomain("northwind.example", "tenant-create"), Times.Once);
        db.Verify(database => database.Insert(It.IsAny<PrismTenantSchema>()), Times.Once);
    }

    [Fact]
    public void CreateTenant_AlsoAcceptsExplicitKeyVaultProviderReference()
    {
        PrismTenantSchema? inserted = null;
        var request = new PrismTenantRequest
        {
            Name = "Northwind",
            Hostname = "northwind.example",
            OidcAuthority = "https://auth.example.com/realms/northwind",
            OidcClientId = "northwind-portal",
            OidcClientSecretProvider = PrismSecretProviderNames.AzureKeyVault,
            OidcClientSecretReference = "northwind-oidc-secret"
        };

        var (controller, db, tenantService, _) = BuildController(database =>
        {
            database.Setup(d => d.Insert(It.IsAny<PrismTenantSchema>()))
                .Callback<object>(record => inserted = record as PrismTenantSchema)
                .Returns(new object());
        });

        var result = controller.CreateTenant(request).Should().BeOfType<OkObjectResult>().Subject;
        var responseTenant = result.Value.Should().BeOfType<PrismTenantResponse>().Subject;

        inserted.Should().NotBeNull();
        inserted!.OidcClientSecretProvider.Should().Be(PrismSecretProviderNames.AzureKeyVault);
        inserted.OidcClientSecretReference.Should().Be("northwind-oidc-secret");
        responseTenant.HasOidcClientSecret.Should().BeTrue();
        responseTenant.SecretKeyName.Should().BeNull();
        tenantService.Verify(service => service.InvalidateDomain("northwind.example", "tenant-create"), Times.Once);
        db.Verify(database => database.Insert(It.IsAny<PrismTenantSchema>()), Times.Once);
    }

    [Fact]
    public void UpdateTenant_ReplacesExistingSecretReference_WhenEditRequestSuppliesSecretKeyName()
    {
        PrismTenantSchema? updatedRecord = null;
        var existing = new PrismTenantSchema
        {
            Id = 1,
            Name = "Northwind",
            Hostname = "northwind.example",
            OidcAuthority = "https://auth.example.com/realms/northwind",
            OidcClientId = "northwind-portal",
            OidcClientSecretProvider = PrismSecretProviderNames.AzureKeyVault,
            OidcClientSecretReference = "northwind-oidc-secret"
        };
        var request = new PrismTenantRequest
        {
            Name = "Northwind",
            Hostname = "northwind.example",
            OidcAuthority = "https://auth.example.com/realms/northwind",
            OidcClientId = "northwind-portal",
            SecretKeyName = "updated-oidc-secret"
        };

        var (controller, db, tenantService, _) = BuildController(database =>
        {
            database.Setup(d => d.SingleOrDefaultById<PrismTenantSchema>(1))
                .Returns(existing);
            database.Setup(d => d.Update(It.IsAny<object>()))
                .Callback<object>(record => updatedRecord = record as PrismTenantSchema);
        });

        var result = controller.UpdateTenant(1, request).Should().BeOfType<OkObjectResult>().Subject;
        var responseTenant = result.Value.Should().BeOfType<PrismTenantResponse>().Subject;

        updatedRecord.Should().NotBeNull();
        updatedRecord!.OidcClientSecretProvider.Should().Be(PrismSecretProviderNames.AzureKeyVault);
        updatedRecord.OidcClientSecretReference.Should().Be("updated-oidc-secret");
        responseTenant.HasOidcClientSecret.Should().BeTrue();
        responseTenant.SecretKeyName.Should().BeNull();
        tenantService.Verify(service => service.InvalidateDomains(
            It.Is<IEnumerable<string>>(domains => domains.SequenceEqual(new[] { "northwind.example", "northwind.example" })),
            "tenant-update"), Times.Once);
        db.Verify(database => database.Update(It.IsAny<object>()), Times.Once);
    }

    [Fact]
    public void UpdateTenant_PreservesExistingSecretReference_WhenEditRequestOmitsReplacement()
    {
        PrismTenantSchema? updatedRecord = null;
        var existing = new PrismTenantSchema
        {
            Id = 1,
            Name = "Northwind",
            Hostname = "northwind.example",
            OidcAuthority = "https://auth.example.com/realms/northwind",
            OidcClientId = "northwind-portal",
            OidcClientSecretProvider = PrismSecretProviderNames.AzureKeyVault,
            OidcClientSecretReference = "northwind-oidc-secret"
        };
        var request = new PrismTenantRequest
        {
            Name = "Northwind",
            Hostname = "northwind.example",
            OidcAuthority = "https://auth.example.com/realms/northwind",
            OidcClientId = "northwind-portal"
        };

        var (controller, db, tenantService, _) = BuildController(database =>
        {
            database.Setup(d => d.SingleOrDefaultById<PrismTenantSchema>(1))
                .Returns(existing);
            database.Setup(d => d.Update(It.IsAny<object>()))
                .Callback<object>(record => updatedRecord = record as PrismTenantSchema);
        });

        var result = controller.UpdateTenant(1, request).Should().BeOfType<OkObjectResult>().Subject;
        var responseTenant = result.Value.Should().BeOfType<PrismTenantResponse>().Subject;

        updatedRecord.Should().NotBeNull();
        updatedRecord!.OidcClientSecretProvider.Should().Be(PrismSecretProviderNames.AzureKeyVault);
        updatedRecord.OidcClientSecretReference.Should().Be("northwind-oidc-secret");
        responseTenant.HasOidcClientSecret.Should().BeTrue();
        responseTenant.SecretKeyName.Should().BeNull();
        tenantService.Verify(service => service.InvalidateDomains(
            It.Is<IEnumerable<string>>(domains => domains.SequenceEqual(new[] { "northwind.example", "northwind.example" })),
            "tenant-update"), Times.Once);
        db.Verify(database => database.Update(It.IsAny<object>()), Times.Once);
    }

    [Fact]
    public void CreateTenant_RejectsInlineProvider_OnManagementApiPath()
    {
        var request = new PrismTenantRequest
        {
            Name = "Northwind",
            Hostname = "northwind.example",
            OidcAuthority = "https://auth.example.com/realms/northwind",
            OidcClientId = "northwind-portal",
            OidcClientSecretProvider = PrismSecretProviderNames.Inline,
            OidcClientSecretReference = "should-not-be-accepted"
        };

        var (controller, db, _, _) = BuildController();

        controller.CreateTenant(request).Should().BeOfType<BadRequestObjectResult>();
        db.Verify(database => database.Insert(It.IsAny<PrismTenantSchema>()), Times.Never);
    }

    [Fact]
    public void CreateTenant_RejectsVaultSecretName_AlreadyUsedByAnotherTenant()
    {
        // SECURITY: SecretVaultService caches/fetches Key Vault secrets under a key built
        // purely from the secret name (Prism_Secret_{secretName}), with no tenant
        // discriminator. Two tenants sharing a name would silently share one cache entry
        // and one vault secret.
        var existingTenants = new List<PrismTenantSchema>
        {
            new()
            {
                Id = 1,
                Name = "Acme A",
                Hostname = "acme-a.example",
                EntraTenantId = "entra-a",
                EntraClientId = "client-a",
                SecretKeyName = "shared-secret-name"
            }
        };

        var request = new PrismTenantRequest
        {
            Name = "Acme B",
            Hostname = "acme-b.example",
            EntraTenantId = "entra-b",
            EntraClientId = "client-b",
            SecretKeyName = "shared-secret-name"
        };

        var (controller, db, tenantService, _) = BuildController(database =>
        {
            database.Setup(d => d.Fetch<PrismTenantSchema>()).Returns(existingTenants);
        });

        var result = controller.CreateTenant(request).Should().BeOfType<BadRequestObjectResult>().Subject;

        result.Value.Should().NotBeNull();
        db.Verify(database => database.Insert(It.IsAny<PrismTenantSchema>()), Times.Never);
        tenantService.Verify(service => service.InvalidateDomain(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void CreateTenant_RejectsVaultSecretName_AlreadyUsedAsGenericOidcKeyVaultReference()
    {
        // Same collision, the other direction: an Entra tenant's SecretKeyName colliding with
        // a generic-OIDC tenant's Key Vault OidcClientSecretReference — both resolve through
        // the identical cache key, so both directions must be checked.
        var existingTenants = new List<PrismTenantSchema>
        {
            new()
            {
                Id = 1,
                Name = "Northwind",
                Hostname = "northwind.example",
                OidcAuthority = "https://auth.example.com/realms/northwind",
                OidcClientId = "northwind-portal",
                OidcClientSecretProvider = PrismSecretProviderNames.AzureKeyVault,
                OidcClientSecretReference = "shared-secret-name"
            }
        };

        var request = new PrismTenantRequest
        {
            Name = "Acme B",
            Hostname = "acme-b.example",
            EntraTenantId = "entra-b",
            EntraClientId = "client-b",
            SecretKeyName = "shared-secret-name"
        };

        var (controller, db, _, _) = BuildController(database =>
        {
            database.Setup(d => d.Fetch<PrismTenantSchema>()).Returns(existingTenants);
        });

        controller.CreateTenant(request).Should().BeOfType<BadRequestObjectResult>();
        db.Verify(database => database.Insert(It.IsAny<PrismTenantSchema>()), Times.Never);
    }

    [Fact]
    public void UpdateTenant_AllowsKeepingItsOwnExistingVaultSecretName()
    {
        // A tenant re-saving its own unchanged SecretKeyName must not collide with itself.
        var existing = new PrismTenantSchema
        {
            Id = 1,
            Name = "Acme A",
            Hostname = "acme-a.example",
            EntraTenantId = "entra-a",
            EntraClientId = "client-a",
            SecretKeyName = "acme-a-secret"
        };

        var request = new PrismTenantRequest
        {
            Name = "Acme A",
            Hostname = "acme-a.example",
            EntraTenantId = "entra-a",
            EntraClientId = "client-a",
            SecretKeyName = "acme-a-secret"
        };

        var (controller, db, _, _) = BuildController(database =>
        {
            database.Setup(d => d.SingleOrDefaultById<PrismTenantSchema>(1)).Returns(existing);
            database.Setup(d => d.Fetch<PrismTenantSchema>()).Returns([existing]);
        });

        controller.UpdateTenant(1, request).Should().BeOfType<OkObjectResult>();
        db.Verify(database => database.Update(It.IsAny<PrismTenantSchema>()), Times.Once);
    }

    private static (
        TenantManagementController Controller,
        Mock<IUmbracoDatabase> Db,
        Mock<ITenantService> TenantService,
        Mock<IUmbracoDatabaseFactory> DbFactory)
        BuildController(Action<Mock<IUmbracoDatabase>>? configureDb = null)
    {
        var db = new Mock<IUmbracoDatabase>();
        configureDb?.Invoke(db);

        var dbFactory = new Mock<IUmbracoDatabaseFactory>();
        dbFactory.Setup(factory => factory.CreateDatabase()).Returns(db.Object);

        var tenantService = new Mock<ITenantService>();
        var controller = new TenantManagementController(
            dbFactory.Object,
            tenantService.Object,
            Mock.Of<IBrandingService>(),
            Mock.Of<IMobileBundleService>(),
            Mock.Of<IPrismBrandingMetadataService>(),
            Mock.Of<ITenantTokenResolver>());

        return (controller, db, tenantService, dbFactory);
    }
}
