using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Persistence;
using UmbracoPrism.Core.Controllers;
using UmbracoPrism.Core.Controllers.Models;
using UmbracoPrism.Core.Persistence;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Tests;

public class PageAccessManagementControllerTests
{
    [Fact]
    public void GetPolicies_ReturnsEveryConfiguredPolicy()
    {
        var contentKey = Guid.NewGuid();
        var (controller, _, _) = BuildController(db =>
        {
            db.Setup(database => database.Fetch<PrismPageAccessPolicySchema>())
                .Returns([new PrismPageAccessPolicySchema { Id = 1, ContentKey = contentKey, RequiresSignIn = true }]);
        });

        var result = controller.GetPolicies().Result.Should().BeOfType<OkObjectResult>().Subject;
        var policies = result.Value.Should().BeAssignableTo<IEnumerable<PrismPageAccessPolicyResponse>>().Subject.ToList();

        policies.Should().ContainSingle();
        policies[0].ContentKey.Should().Be(contentKey);
        policies[0].RequiresSignIn.Should().BeTrue();
    }

    [Fact]
    public void CreatePolicy_InsertsAndInvalidatesCache()
    {
        PrismPageAccessPolicySchema? inserted = null;
        var contentKey = Guid.NewGuid();
        var request = new PrismPageAccessPolicyRequest
        {
            ContentKey = contentKey,
            RequiresSignIn = true,
            AllowedTenantNames = ["North", "South"]
        };

        var (controller, db, resolver) = BuildController(database =>
        {
            database.Setup(d => d.Insert(It.IsAny<PrismPageAccessPolicySchema>()))
                .Callback<object>(record => inserted = record as PrismPageAccessPolicySchema)
                .Returns(new object());
        }, documentUrlService => documentUrlService
            .Setup(s => s.GetLegacyRouteFormat(contentKey, null, false))
            .Returns("/guarded-page"));

        var result = controller.CreatePolicy(request).Should().BeOfType<OkObjectResult>().Subject;
        var response = result.Value.Should().BeOfType<PrismPageAccessPolicyResponse>().Subject;

        inserted.Should().NotBeNull();
        inserted!.ContentKey.Should().Be(contentKey);
        inserted.ContentRoute.Should().Be("/guarded-page");
        inserted.TenantAllowListJson.Should().Be("[\"North\",\"South\"]");
        response.AllowedTenantNames.Should().BeEquivalentTo(["North", "South"]);
        response.ContentRoute.Should().Be("/guarded-page");
        resolver.Verify(r => r.Invalidate(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public void CreatePolicy_RejectsASecondPolicyForTheSameContentNode()
    {
        var contentKey = Guid.NewGuid();
        var (controller, db, resolver) = BuildController(database =>
        {
            database.Setup(d => d.Fetch<PrismPageAccessPolicySchema>())
                .Returns([new PrismPageAccessPolicySchema { Id = 1, ContentKey = contentKey }]);
        });

        var result = controller.CreatePolicy(new PrismPageAccessPolicyRequest { ContentKey = contentKey })
            .Should().BeOfType<BadRequestObjectResult>().Subject;

        result.Value.Should().NotBeNull();
        db.Verify(d => d.Insert(It.IsAny<PrismPageAccessPolicySchema>()), Times.Never);
        resolver.Verify(r => r.Invalidate(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void UpdatePolicy_UpdatesAndInvalidatesCache()
    {
        var contentKey = Guid.NewGuid();
        var existing = new PrismPageAccessPolicySchema { Id = 1, ContentKey = contentKey, RequiresSignIn = false };

        var (controller, db, resolver) = BuildController(database =>
        {
            database.Setup(d => d.SingleOrDefaultById<PrismPageAccessPolicySchema>(1)).Returns(existing);
        });

        var result = controller.UpdatePolicy(1, new PrismPageAccessPolicyRequest
        {
            ContentKey = contentKey,
            RequiresSignIn = true
        }).Should().BeOfType<OkObjectResult>().Subject;

        var response = result.Value.Should().BeOfType<PrismPageAccessPolicyResponse>().Subject;
        response.RequiresSignIn.Should().BeTrue();
        db.Verify(d => d.Update(existing), Times.Once);
        resolver.Verify(r => r.Invalidate(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public void UpdatePolicy_ReturnsNotFound_ForAnUnknownId()
    {
        var (controller, _, resolver) = BuildController(database =>
        {
            database.Setup(d => d.SingleOrDefaultById<PrismPageAccessPolicySchema>(99)).Returns((PrismPageAccessPolicySchema?)null);
        });

        controller.UpdatePolicy(99, new PrismPageAccessPolicyRequest()).Should().BeOfType<NotFoundResult>();
        resolver.Verify(r => r.Invalidate(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void DeletePolicy_DeletesAndInvalidatesCache()
    {
        var existing = new PrismPageAccessPolicySchema { Id = 1, ContentKey = Guid.NewGuid() };
        var (controller, db, resolver) = BuildController(database =>
        {
            database.Setup(d => d.SingleOrDefaultById<PrismPageAccessPolicySchema>(1)).Returns(existing);
        });

        controller.DeletePolicy(1).Should().BeOfType<OkResult>();

        db.Verify(d => d.Delete<PrismPageAccessPolicySchema>(1), Times.Once);
        resolver.Verify(r => r.Invalidate(It.IsAny<string>()), Times.Once);
    }

    private static (
        PageAccessManagementController Controller,
        Mock<IUmbracoDatabase> Db,
        Mock<IPrismPageAccessResolver> Resolver)
        BuildController(
            Action<Mock<IUmbracoDatabase>>? configureDb = null,
            Action<Mock<IDocumentUrlService>>? configureDocumentUrlService = null)
    {
        var db = new Mock<IUmbracoDatabase>();
        configureDb?.Invoke(db);

        var dbFactory = new Mock<IUmbracoDatabaseFactory>();
        dbFactory.Setup(factory => factory.CreateDatabase()).Returns(db.Object);

        var resolver = new Mock<IPrismPageAccessResolver>();
        var contentService = new Mock<IContentService>();
        contentService.Setup(c => c.GetById(It.IsAny<Guid>())).Returns((IContent?)null);

        var documentUrlService = new Mock<IDocumentUrlService>();
        configureDocumentUrlService?.Invoke(documentUrlService);

        var controller = new PageAccessManagementController(
            dbFactory.Object, resolver.Object, contentService.Object, documentUrlService.Object);

        return (controller, db, resolver);
    }
}
