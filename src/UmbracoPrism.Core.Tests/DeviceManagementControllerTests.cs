using System.Reflection;
using AwesomeAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Umbraco.Cms.Infrastructure.Persistence;
using UmbracoPrism.Core.Controllers;
using UmbracoPrism.Core.Controllers.Models;
using UmbracoPrism.Core.Persistence;

namespace UmbracoPrism.Core.Tests;

public class DeviceManagementControllerTests
{
    private static PrismDeviceCredentialSchema Credential(string deviceId = "device-1", DateTime? revokedAt = null) => new()
    {
        Id = 5,
        DeviceId = deviceId,
        TenantId = "42",
        UserId = "user-oid",
        DeviceName = "Pixel",
        Platform = "android",
        TokenHash = "secret-hash",
        RefreshTokenEnc = "secret-refresh-token",
        RegisteredAt = DateTime.UtcNow.AddDays(-3),
        ExpiresAt = DateTime.UtcNow.AddDays(27),
        RevokedAt = revokedAt,
    };

    private static (DeviceManagementController Controller, Mock<IUmbracoDatabase> Db) Build(
        bool tenantExists = true,
        PrismDeviceCredentialSchema? single = null,
        List<PrismDeviceCredentialSchema>? list = null)
    {
        var db = new Mock<IUmbracoDatabase>();
        db.Setup(d => d.SingleOrDefaultById<PrismTenantSchema>(42))
            .Returns(tenantExists ? new PrismTenantSchema { Id = 42 } : null!);
        db.Setup(d => d.FirstOrDefault<PrismDeviceCredentialSchema>(It.IsAny<string>(), It.IsAny<object[]>()))
            .Returns(single!);
        db.Setup(d => d.Fetch<PrismDeviceCredentialSchema>(It.IsAny<string>(), It.IsAny<object[]>()))
            .Returns(list ?? []);

        var factory = new Mock<IUmbracoDatabaseFactory>();
        factory.Setup(f => f.CreateDatabase()).Returns(db.Object);

        var controller = new DeviceManagementController(factory.Object, Mock.Of<ILogger<DeviceManagementController>>())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
        return (controller, db);
    }

    [Fact]
    public void Revoke_sets_RevokedAt_and_returns_204()
    {
        var (controller, db) = Build(single: Credential());

        controller.RevokeDevice(42, "device-1").Should().BeOfType<NoContentResult>();

        db.Verify(d => d.Update(It.Is<PrismDeviceCredentialSchema>(r => r.Id == 5 && r.RevokedAt != null)), Times.Once);
    }

    [Fact]
    public void Revoke_of_an_already_revoked_credential_succeeds_without_updating()
    {
        var (controller, db) = Build(single: Credential(revokedAt: DateTime.UtcNow.AddHours(-1)));

        controller.RevokeDevice(42, "device-1").Should().BeOfType<NoContentResult>();

        db.Verify(d => d.Update(It.IsAny<object>()), Times.Never);
    }

    [Fact]
    public void Revoke_of_an_unknown_or_other_tenants_device_is_404()
    {
        var (controller, db) = Build(single: null);

        controller.RevokeDevice(42, "device-in-another-tenant").Should().BeOfType<NotFoundResult>();

        db.Verify(d => d.FirstOrDefault<PrismDeviceCredentialSchema>(
            It.Is<string>(sql => sql.Contains("TenantId")),
            It.Is<object[]>(a => a.Length == 2 && a[0].ToString() == "device-in-another-tenant" && a[1].ToString() == "42")),
            Times.Once);
        db.Verify(d => d.Update(It.IsAny<object>()), Times.Never);
    }

    [Fact]
    public void List_is_404_for_an_unknown_tenant()
    {
        var (controller, db) = Build(tenantExists: false);

        controller.GetDevices(42).Should().BeOfType<NotFoundResult>();

        db.Verify(d => d.Fetch<PrismDeviceCredentialSchema>(It.IsAny<string>(), It.IsAny<object[]>()), Times.Never);
    }

    [Fact]
    public void List_is_scoped_to_the_tenant_and_never_exposes_secrets()
    {
        var (controller, db) = Build(list: [Credential()]);

        var result = controller.GetDevices(42).Should().BeOfType<OkObjectResult>().Subject;

        var device = ((IEnumerable<PrismDeviceResponse>)result.Value!).Single();
        device.DeviceId.Should().Be("device-1");
        device.UserId.Should().Be("user-oid");
        typeof(PrismDeviceResponse).GetProperties().Select(p => p.Name)
            .Should().NotContain(["TokenHash", "RefreshTokenEnc", "PushToken"]);
        db.Verify(d => d.Fetch<PrismDeviceCredentialSchema>(
            It.Is<string>(sql => sql.Contains("TenantId")),
            It.Is<object[]>(a => a.Length == 1 && a[0].ToString() == "42")), Times.Once);
    }

    [Fact]
    public void Controller_requires_backoffice_access_and_the_PrismAdmins_policy()
    {
        var policies = typeof(DeviceManagementController)
            .GetCustomAttributes<AuthorizeAttribute>().Select(a => a.Policy).ToList();

        policies.Should().Contain("PrismAdmins");
        policies.Should().Contain(p => p != null && p.Contains("BackOffice", StringComparison.OrdinalIgnoreCase));
    }
}
