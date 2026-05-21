using FluentAssertions;

namespace UmbracoPrism.Core.Tests;

/// <summary>
/// File-shape guards for the showcase shortcuts that surface workflow admin and editor entry points.
/// These keep the Aspire dashboard and reference member dashboard discoverable without booting the apps.
/// </summary>
public class WorkflowShowcaseShortcutTests
{
    private static readonly string RepoRoot =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));

    [Fact]
    public void AppHost_MustAdvertise_SingleWorkflowEditorShortcut_AlongsideAdmin()
    {
        var programPath = Path.Combine(RepoRoot, "src", "UmbracoPrism.AppHost", "Program.cs");
        var content = File.ReadAllText(programPath);

        content.Should().Contain("DisplayText = \"Workflow Admin\"",
            because: "the Aspire dashboard should keep the workflow admin shortcut");
        content.Should().Contain("DisplayText = \"Workflow Editor\"",
            because: "the Aspire dashboard should give the reference editor a first-class shortcut");
        content.Should().NotContain("DisplayText = \"Workflow Editor Page\"",
            because: "the dashboard cleanup should not advertise a second workflow-editor shortcut that duplicates the main editor entry point");
    }

    [Fact]
    public void MemberDashboard_MustExpose_A_SingleWorkflowEditorShortcut()
    {
        var controllerPath = Path.Combine(
            RepoRoot,
            "src", "UmbracoPrism.Core", "Controllers", "MemberDashboardController.cs");
        var viewPath = Path.Combine(
            RepoRoot,
            "src", "UmbracoPrism.TestSite", "Views", "memberDashboard.cshtml");

        var controller = File.ReadAllText(controllerPath);
        var view = File.ReadAllText(viewPath);

        controller.Should().Contain("ViewBag.WorkflowEditorUrl",
            because: "the dashboard controller should explicitly construct the editor shortcut");
        controller.Should().NotContain("ViewBag.WorkflowEditorPageUrl",
            because: "the dashboard should not carry a duplicate direct-page shortcut once the editor shell is the primary entry point");
        view.Should().Contain("<h3>Workflow Editor</h3>",
            because: "the member dashboard should present the editor as a first-class showcase surface");
        view.Should().NotContain("Direct Page",
            because: "cleanup should remove the duplicate dashboard shortcut and keep the editor CTA clear");
    }

    [Fact]
    public void WorkflowAdminScreen_MustLink_BackToEditorReferenceSurfaces()
    {
        var programPath = Path.Combine(RepoRoot, "src", "UmbracoPrism.MockBusinessApp", "Program.cs");
        var content = File.ReadAllText(programPath);

        content.Should().Contain("href=\"/workflow-editor\"",
            because: "the workflow admin surface should point straight back to the reference editor shell");
        content.Should().Contain("href=\"/workflow-editor?workflow={Esc(authoredWorkflowKey!)}\"",
            because: "each workflow definition should hand off using the authoring route key the editor can actually load");
        content.Should().Contain("Edit workflow",
            because: "authors should not have to infer that JSON editing is the only path to the richer editor");
        content.Should().Contain("No editor definition yet",
            because: "workflows without an editor source should explain the prerequisite without exposing storage details");
    }
}
