using System.Text.Json;
using FluentAssertions;

namespace UmbracoPrism.Core.Tests;

/// <summary>
/// File-shape assertions for the PrismWorkflowEditor App_Plugins package.
/// No Umbraco runtime is started — these are pure file-system / JSON checks.
/// </summary>
public class WorkflowEditorManifestTests
{
    private static readonly string RepoRoot =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));

    private static string ManifestPath =>
        Path.Combine(
            RepoRoot,
            "src", "UmbracoPrism.TestSite", "App_Plugins", "PrismWorkflowEditor", "umbraco-package.json");

    [Fact]
    public void UmbracoPackageJson_MustExist()
    {
        File.Exists(ManifestPath).Should().BeTrue(
            because: $"the PrismWorkflowEditor package manifest must be present at {ManifestPath}");
    }

    [Fact]
    public void UmbracoPackageJson_MustParseAsValidJson()
    {
        File.Exists(ManifestPath).Should().BeTrue();

        var raw = File.ReadAllText(ManifestPath);
        var act = () => JsonDocument.Parse(raw);
        act.Should().NotThrow("umbraco-package.json must be valid JSON");
    }

    [Fact]
    public void UmbracoPackageJson_MustDeclare_WorkflowEditorSectionAlias()
    {
        File.Exists(ManifestPath).Should().BeTrue();

        using var document = JsonDocument.Parse(File.ReadAllText(ManifestPath));
        var root = document.RootElement;

        root.TryGetProperty("extensions", out var extensions).Should().BeTrue(
            because: "the manifest must have an 'extensions' array");

        extensions.ValueKind.Should().Be(JsonValueKind.Array,
            because: "'extensions' must be a JSON array");

        var sectionAliases = extensions
            .EnumerateArray()
            .Where(ext =>
                ext.TryGetProperty("type", out var type) &&
                type.GetString() == "section")
            .Select(ext =>
                ext.TryGetProperty("alias", out var alias) ? alias.GetString() : null)
            .Where(alias => alias is not null)
            .ToList();

        sectionAliases.Should().Contain("Umb.Section.PrismWorkflowEditor",
            because: "the package must declare the PrismWorkflowEditor section extension");
    }

    [Fact]
    public void UmbracoPackageJson_MustDeclare_Dashboard_With_HostElement()
    {
        File.Exists(ManifestPath).Should().BeTrue();

        using var document = JsonDocument.Parse(File.ReadAllText(ManifestPath));
        var root = document.RootElement;
        root.TryGetProperty("extensions", out var extensions);

        var dashboardElement = extensions
            .EnumerateArray()
            .Where(ext =>
                ext.TryGetProperty("type", out var type) &&
                type.GetString() == "dashboard")
            .Select(ext =>
                ext.TryGetProperty("elementName", out var el) ? el.GetString() : null)
            .FirstOrDefault();

        dashboardElement.Should().Be("prism-workflow-editor-host",
            because: "the dashboard extension must reference the prism-workflow-editor-host element");
    }

    [Fact]
    public void HostElement_JS_MustExist()
    {
        var jsPath = Path.Combine(
            RepoRoot,
            "src", "UmbracoPrism.TestSite", "App_Plugins", "PrismWorkflowEditor",
            "web-components", "prism-workflow-editor-host.js");

        File.Exists(jsPath).Should().BeTrue(
            because: "the prism-workflow-editor-host Lit element script must be present");
    }
}
