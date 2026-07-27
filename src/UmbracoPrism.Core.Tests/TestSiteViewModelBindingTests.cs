using System.Text.RegularExpressions;
using FluentAssertions;

namespace UmbracoPrism.Core.Tests;

/// <summary>
/// Guards the reference TestSite service request views so they stay strongly typed and keep
/// consuming the reusable Prism rendering surface from Core.
///
/// When a route-hijacking controller returns a typed ViewModel, the matching Razor view must
/// inherit that ViewModel type (or a compatible base), not the raw published model. The TestSite
/// also acts as the reference Umbraco integration, so its service request views should explicitly
/// use the shared layout and Core service request partials instead of remaining empty stubs.
/// </summary>
public class TestSiteViewModelBindingTests
{
    private static readonly string RepoRoot =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));

    private static string TestSiteViewsPath =>
        Path.Combine(RepoRoot, "src", "UmbracoPrism.TestSite", "Views");

    [Theory]
    [InlineData("stagePage")]
    [InlineData("serviceRequestHub")]
    public void TestSite_ServiceRequestViews_MustExist(string documentTypeAlias)
    {
        var viewPath = Path.Combine(TestSiteViewsPath, $"{documentTypeAlias}.cshtml");

        File.Exists(viewPath).Should().BeTrue(
            because: $"the TestSite should show the reference Umbraco integration for '{documentTypeAlias}' instead of an empty generated stub");
    }

    [Theory]
    [InlineData("stagePage", "StageViewModel", "ContentModels.StagePage")]
    [InlineData("serviceRequestHub", "ServiceRequestHubViewModel", "ContentModels.ServiceRequestHub")]
    public void TestSite_ServiceRequestViews_MustUseTypedViewModels(string documentTypeAlias, string expectedType, string forbiddenType)
    {
        var viewPath = Path.Combine(TestSiteViewsPath, $"{documentTypeAlias}.cshtml");
        File.Exists(viewPath).Should().BeTrue();

        var content = File.ReadAllText(viewPath);
        var inheritsMatch = Regex.Match(content, @"@inherits\s+(\S+)");

        inheritsMatch.Success.Should().BeTrue(because: $"{viewPath} must have an @inherits directive");
        inheritsMatch.Groups[1].Value.Should().Contain(expectedType,
            because: $"{documentTypeAlias}.cshtml should inherit the typed ViewModel returned by its route-hijacking controller");

        inheritsMatch.Groups[1].Value.Should().NotContain(forbiddenType,
            because: $"'{documentTypeAlias}.cshtml' must not inherit the raw Umbraco published model '{forbiddenType}'");
    }

    [Theory]
    [InlineData("stagePage")]
    [InlineData("serviceRequestHub")]
    public void TestSite_ServiceRequestViews_MustUseSharedMasterLayout(string documentTypeAlias)
    {
        var viewPath = Path.Combine(TestSiteViewsPath, $"{documentTypeAlias}.cshtml");
        var content = File.ReadAllText(viewPath);

        content.Should().Contain("Layout = \"~/Views/Shared/Master.cshtml\"",
            because: "service request pages should render inside the authored Umbraco site shell");
    }

    [Fact]
    public void StagePageView_MustCompose_PrismStageShells()
    {
        var viewPath = Path.Combine(TestSiteViewsPath, "stagePage.cshtml");
        var content = File.ReadAllText(viewPath);

        content.Should().Contain("ServiceRequestRenderShellResolver.ResolveShell",
            because: "the TestSite stage page should keep using the shared Core shell selection logic");
        content.Should().Contain("Html.PartialAsync(partialName, Model)",
            because: "the TestSite stage page should render the reusable Core shell partials");
        content.Should().Contain("Html.PartialAsync(\"_ServiceRequestHub-InstancePicker\", Model)",
            because: "prompt-style stages should still offer the instance picker experience");
    }

    [Fact]
    public void ServiceRequestHubView_MustCompose_CoreInstanceListPartial()
    {
        var viewPath = Path.Combine(TestSiteViewsPath, "serviceRequestHub.cshtml");
        var content = File.ReadAllText(viewPath);

        content.Should().Contain("Html.PartialAsync(\"_ServiceRequestHub-InstanceList\", Model.ActiveInstances)",
            because: "the service request hub should show the reusable instance list for active requests");
        content.Should().Contain("Html.PartialAsync(\"_ServiceRequestHub-InstanceList\", Model.CompletedInstances)",
            because: "the service request hub should show the reusable instance list for completed requests");
    }
}
