using System.Text.RegularExpressions;
using FluentAssertions;

namespace UmbracoPrism.Core.Tests;

/// <summary>
/// Guards against Umbraco's ModelsBuilder silently regenerating stub views for document types
/// whose route-hijacking controllers pass a custom ViewModel — not the raw published model.
///
/// When a route-hijacking controller (e.g. WorkflowPageController) returns a typed ViewModel,
/// any Razor view for that document type must declare @inherits with that ViewModel type (or a
/// compatible base), not with the auto-generated Umbraco published model. Umbraco's
/// ContentModelBinder enforces this at render time and throws a ModelBindingException if they
/// don't match. The Core project owns the canonical views and embeds them as resources; the
/// TestSite must not override them with stub files.
/// </summary>
public class TestSiteViewModelBindingTests
{
    private static readonly string RepoRoot =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));

    private static string TestSiteViewsPath =>
        Path.Combine(RepoRoot, "src", "UmbracoPrism.TestSite", "Views");

    /// <summary>
    /// Document type aliases whose canonical views are owned by the Core project. The TestSite
    /// must not have local override views for these, because any auto-generated stub will
    /// inherit the raw published model and break the route-hijacking controller's ViewModel.
    /// </summary>
    private static readonly string[] CoreOwnedDocumentTypeAliases =
    [
        "workflowPage",
        "workflowHub",
    ];

    [Theory]
    [InlineData("workflowPage")]
    [InlineData("workflowHub")]
    public void TestSite_MustNotOverride_CoreOwnedViews(string documentTypeAlias)
    {
        var viewPath = Path.Combine(TestSiteViewsPath, $"{documentTypeAlias}.cshtml");

        File.Exists(viewPath).Should().BeFalse(
            because: $"the canonical view for '{documentTypeAlias}' is embedded in UmbracoPrism.Core " +
                     $"and the TestSite must not override it. " +
                     $"Umbraco's ModelsBuilder can regenerate a stub view that inherits the raw published model " +
                     $"(ContentModels.{char.ToUpper(documentTypeAlias[0]) + documentTypeAlias[1..]}), " +
                     $"which causes a ModelBindingException at runtime when the route-hijacking controller " +
                     $"returns its typed ViewModel. Delete '{viewPath}' to restore the Core fallback.");
    }

    [Theory]
    [InlineData("workflowPage", "ContentModels.WorkflowPage")]
    [InlineData("workflowHub", "ContentModels.WorkflowHub")]
    public void TestSite_IfViewExists_MustNotInheritRawPublishedModel(string documentTypeAlias, string forbiddenType)
    {
        var viewPath = Path.Combine(TestSiteViewsPath, $"{documentTypeAlias}.cshtml");

        if (!File.Exists(viewPath))
            return; // covered by the existence test above; no override = no problem

        var content = File.ReadAllText(viewPath);
        var inheritsMatch = Regex.Match(content, @"@inherits\s+(\S+)");

        inheritsMatch.Success.Should().BeTrue(because: $"{viewPath} must have an @inherits directive");

        inheritsMatch.Groups[1].Value.Should().NotContain(forbiddenType,
            because: $"'{documentTypeAlias}.cshtml' in the TestSite inherits the raw Umbraco published model " +
                     $"'{forbiddenType}', which is incompatible with the ViewModel returned by the " +
                     $"route-hijacking controller. Update @inherits to the correct ViewModel type, or delete " +
                     $"the file to fall back to the Core embedded view.");
    }
}
