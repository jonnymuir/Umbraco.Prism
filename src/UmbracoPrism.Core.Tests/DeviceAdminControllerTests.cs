using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Umbraco.Cms.Infrastructure.Persistence;
using UmbracoPrism.Core.Controllers;
using UmbracoPrism.Core.Models;
using UmbracoPrism.Core.Persistence;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Tests;

public class DeviceAdminControllerTests
{
    // ------------------------------------------------------------------ helpers

    private static (DeviceAdminController Controller, Mock<IUmbracoDatabase> Db) BuildAdminController(
        PrismTenant? tenant = null,
        PrismDeviceCredentialSchema? existingRecord = null)
    {
        var logger = Mock.Of<ILogger<DeviceAdminController>>();

        var prismContext = new Mock<IPrismContext>();
        prismContext.Setup(c => c.CurrentTenant).Returns(tenant);

        var mockDb = new Mock<IUmbracoDatabase>();
        mockDb.Setup(db => db.FirstOrDefault<PrismDeviceCredentialSchema>(
                It.IsAny<string>(), It.IsAny<object[]>()))
            .Returns(existingRecord!);

        var dbFactory = new Mock<IUmbracoDatabaseFactory>();
        dbFactory.Setup(f => f.CreateDatabase()).Returns(mockDb.Object);

        var controller = new DeviceAdminController(
            dbFactory.Object,
            prismContext.Object,
            logger);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext(),
        };

        return (controller, mockDb);
    }

    // ------------------------------------------------------------------ happy path

    [Fact]
    public void Revoke_HappyPath_SoftDeletesAndReturns204()
    {
        var tenant = new PrismTenant { Id = 42, Name = "TestTenant" };
        var credential = new PrismDeviceCredentialSchema
        {
            Id = 5,
            DeviceId = "device-uuid-1",
            TenantId = "42",
            UserId = "some-user-oid",
            TokenHash = "hash",
            RefreshTokenEnc = "enc",
            RegisteredAt = DateTime.UtcNow.AddDays(-3),
            ExpiresAt = DateTime.UtcNow.AddDays(27),
            RevokedAt = null,
        };

        var (controller, db) = BuildAdminController(tenant: tenant, existingRecord: credential);

        var result = controller.Revoke("device-uuid-1");

        result.Should().BeOfType<NoContentResult>();

        db.Verify(d => d.Update(It.Is<PrismDeviceCredentialSchema>(r =>
            r.Id == 5 &&
            r.RevokedAt != null
        )), Times.Once);
    }

    // ------------------------------------------------------------------ already revoked

    [Fact]
    public void Revoke_AlreadyRevoked_Returns204WithoutUpdate()
    {
        var tenant = new PrismTenant { Id = 42, Name = "TestTenant" };
        var credential = new PrismDeviceCredentialSchema
        {
            Id = 5,
            DeviceId = "device-uuid-1",
            TenantId = "42",
            UserId = "some-user-oid",
            TokenHash = "hash",
            RefreshTokenEnc = "enc",
            RegisteredAt = DateTime.UtcNow.AddDays(-3),
            ExpiresAt = DateTime.UtcNow.AddDays(27),
            RevokedAt = DateTime.UtcNow.AddHours(-1), // already revoked
        };

        var (controller, db) = BuildAdminController(tenant: tenant, existingRecord: credential);

        var result = controller.Revoke("device-uuid-1");

        result.Should().BeOfType<NoContentResult>();

        // Should NOT call Update since already revoked
        db.Verify(d => d.Update(It.IsAny<object>()), Times.Never);
    }

    // ------------------------------------------------------------------ not found / cross-tenant

    [Fact]
    public void Revoke_DeviceNotFound_Returns404()
    {
        var tenant = new PrismTenant { Id = 42, Name = "TestTenant" };

        var (controller, _) = BuildAdminController(tenant: tenant, existingRecord: null);

        var result = controller.Revoke("nonexistent-device");

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public void Revoke_CrossTenant_Returns404()
    {
        // Device belongs to tenant 99 but admin is in tenant 42
        var tenant = new PrismTenant { Id = 42, Name = "TestTenant" };

        // The DB query scopes by TenantId, so a cross-tenant device won't be found
        var (controller, db) = BuildAdminController(tenant: tenant, existingRecord: null);

        // Verify the query includes TenantId scoping
        var result = controller.Revoke("device-in-other-tenant");

        result.Should().BeOfType<NotFoundResult>();

        // Verify FirstOrDefault was called with TenantId = "42" (current tenant)
        db.Verify(d => d.FirstOrDefault<PrismDeviceCredentialSchema>(
            It.IsAny<string>(),
            It.Is<object[]>(args =>
                args.Length >= 2 &&
                args[0].ToString() == "device-in-other-tenant" &&
                args[1].ToString() == "42")),
            Times.Once);
    }

    // ------------------------------------------------------------------ no tenant context

    [Fact]
    public void Revoke_NoTenantContext_Returns400()
    {
        var (controller, _) = BuildAdminController(tenant: null);

        var result = controller.Revoke("device-uuid-1");

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ------------------------------------------------------------------ authorization attributes

    [Fact]
    public void Controller_RequiresPrismAdminsPolicy()
    {
        var authorizeAttributes = typeof(DeviceAdminController)
            .GetCustomAttributes<AuthorizeAttribute>()
            .ToList();

        authorizeAttributes.Should().Contain(a => a.Policy == "PrismAdmins",
            "the controller must require the PrismAdmins policy to restrict access to tenant admins");
    }

    [Fact]
    public void Controller_RequiresPrismMemberCookieScheme()
    {
        var authorizeAttributes = typeof(DeviceAdminController)
            .GetCustomAttributes<AuthorizeAttribute>()
            .ToList();

        authorizeAttributes.Should().Contain(a => a.AuthenticationSchemes == "PrismMemberCookie",
            "the controller must authenticate via PrismMemberCookie");
    }
}
