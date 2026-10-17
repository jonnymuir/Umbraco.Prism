using System.Reflection;
using AwesomeAssertions;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Web.Common.Controllers;

namespace UmbracoPrism.Core.Tests;

/// <summary>
/// The published package never takes ownership of a host's content model. Installing Prism on an
/// existing site must leave its document types, templates, data types and media as they were, and
/// must not claim any document type alias; all of that belongs to the host (the reference site's
/// own versions live in UmbracoPrism.TestSite).
///
/// Types are discovered by reflection over the package assembly, so a newly added seeder or
/// route-hijacking controller cannot slip through.
/// </summary>
public class ContentOwnershipContractTests
{
    private static readonly Assembly Package = typeof(PrismComposer).Assembly;

    // Services whose only purpose is to define or change the content model and media. Reading
    // content (IContentService, e.g. to show a page's name) is fine; these are not.
    private static readonly Type[] ContentModelServices =
    [
        typeof(IContentTypeService),
        typeof(IMediaTypeService),
        typeof(IMemberTypeService),
        typeof(IDataTypeService),
        typeof(ITemplateService),
        typeof(IMediaService),
    ];

    private static IEnumerable<Type> PackageTypes() =>
        Package.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false });

    [Fact]
    public void NoPackageTypeDependsOnAServiceThatDefinesOrChangesTheContentModel()
    {
        var offenders = PackageTypes()
            .SelectMany(t => t.GetConstructors()
                .SelectMany(c => c.GetParameters())
                .Where(p => ContentModelServices.Contains(p.ParameterType))
                .Select(p => $"{t.FullName} takes {p.ParameterType.Name}"))
            .Distinct()
            .ToList();

        offenders.Should().BeEmpty(
            "Prism must not create or change a host's document types, templates, data types or media; " +
            "site-specific content setup belongs to the host (see UmbracoPrism.TestSite)");
    }

    [Fact]
    public void NoPackageControllerClaimsADocumentTypeByRouteHijacking()
    {
        var hijackers = PackageTypes()
            .Where(t => typeof(RenderController).IsAssignableFrom(t))
            .Select(t => t.FullName)
            .ToList();

        hijackers.Should().BeEmpty(
            "a RenderController takes over every page of the document type its name matches, on " +
            "every site that installs Prism; only the host should decide how its own pages render");
    }
}
