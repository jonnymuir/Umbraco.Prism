using FluentAssertions;

namespace UmbracoPrism.Core.Tests;

/// <summary>
/// File-shape guards for the showcase shortcuts that surface service desk admin and editor entry points.
/// These keep the Aspire dashboard and reference member dashboard discoverable without booting the apps.
/// </summary>
public class ServiceDeskShowcaseShortcutTests
{
    private static readonly string RepoRoot =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));

    [Fact]
    public void AppHost_MustAdvertise_SingleServiceBlueprintEditorShortcut_AlongsideAdmin()
    {
        var programPath = Path.Combine(RepoRoot, "src", "UmbracoPrism.AppHost", "Program.cs");
        var content = File.ReadAllText(programPath);

        content.Should().Contain("DisplayText = \"Service Desk\"",
            because: "the Aspire dashboard should keep the service desk admin shortcut");
        content.Should().Contain("DisplayText = \"Service Blueprint Editor\"",
            because: "the Aspire dashboard should give the reference editor a first-class shortcut");
        content.Should().NotContain("DisplayText = \"Service Blueprint Editor Page\"",
            because: "the dashboard cleanup should not advertise a second service-blueprint-editor shortcut that duplicates the main editor entry point");
    }

    [Fact]
    public void MemberDashboard_MustExpose_A_SingleServiceBlueprintEditorShortcut()
    {
        var controllerPath = Path.Combine(
            RepoRoot,
            "src", "UmbracoPrism.Core", "Controllers", "MemberDashboardController.cs");

        var controller = File.ReadAllText(controllerPath);

        controller.Should().Contain("ViewBag.ServiceBlueprintEditorUrl",
            because: "the dashboard controller should explicitly construct the editor shortcut");
        controller.Should().NotContain("ViewBag.ServiceBlueprintEditorPageUrl",
            because: "the dashboard should not carry a duplicate direct-page shortcut once the editor shell is the primary entry point");
        controller.Should().Contain("/service-blueprint-editor",
            because: "the shortcut must point at the renamed editor route, not the retired /workflow-editor path");
    }

    [Fact]
    public void ServiceDeskAdminScreen_MustLink_BackToEditorReferenceSurfaces()
    {
        var programPath = Path.Combine(RepoRoot, "src", "UmbracoPrism.MockBusinessApp", "Program.cs");
        var content = File.ReadAllText(programPath);

        content.Should().Contain("href=\"/service-blueprint-editor\"",
            because: "the service-desk admin surface should point straight back to the reference editor shell");
        content.Should().Contain("href=\"/service-blueprint-editor?serviceBlueprint={Esc(authoredServiceBlueprintKey!)}\"",
            because: "each service blueprint definition should hand off using the authoring route key the editor can actually load");
        content.Should().Contain("Edit service blueprint",
            because: "authors should not have to infer that JSON editing is the only path to the richer editor");
        content.Should().Contain("No editor definition yet",
            because: "service blueprints without an editor source should explain the prerequisite without exposing storage details");
    }
}
