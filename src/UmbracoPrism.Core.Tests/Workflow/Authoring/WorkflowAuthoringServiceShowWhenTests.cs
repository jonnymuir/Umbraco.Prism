using System.Text.Json;
using FluentAssertions;
using UmbracoPrism.Shared.Models.Workflow;
using UmbracoPrism.Shared.Models.Workflow.Components;
using UmbracoPrism.WorkflowRuntime.Abstractions;
using UmbracoPrism.WorkflowRuntime.Services;

namespace UmbracoPrism.Core.Tests.Workflow.Authoring;

/// <summary>
/// Covers the validate-time showWhen pass added to <see cref="WorkflowAuthoringService.Validate"/>
/// — previously showWhen expressions were only ever evaluated at render time, where a failure is
/// logged and swallowed (the component just stays visible), so an author had no way to discover a
/// broken showWhen without running the workflow. Also covers service-sourced calculation fields at
/// validate-time: unresolved without mocks is a warning, not a hard failure.
/// </summary>
public class WorkflowAuthoringServiceShowWhenTests
{
    private static readonly WorkflowAuthoringService Service = new(new NotSupportedWorkflowSourceStore());

    [Fact]
    public void Validate_BrokenShowWhenOnWorkflowWithNoCalculationsBlock_IsCaught()
    {
        // No calculations block at all — proves the validate-time pass doesn't inherit the
        // render-time engine's "no calculations block means showWhen is never evaluated" gate.
        var workflow = new WorkflowDefinitionFile
        {
            DefinitionKey = "showwhen-test",
            DisplayName = "ShowWhen Test",
            InitialState = "start",
            States =
            [
                new StepDefinition
                {
                    StateKey = "start",
                    DisplayName = "Start",
                    Components =
                    [
                        new BodyComponent { Content = "hello", ShowWhen = "nosuchfield = 'x'" }
                    ],
                    Routes = []
                }
            ]
        };

        var outcome = Service.Validate(workflow);

        outcome.IsValid.Should().BeFalse();
        outcome.Diagnostics.Should().ContainSingle(d =>
            d.Code == "SHOW_WHEN_EVAL_ERROR"
            && d.Path == "states.start.components[0].showWhen"
            && d.Message.Contains("nosuchfield"));
    }

    [Fact]
    public void Validate_ValidShowWhenReferencingAnInput_NoDiagnostic()
    {
        var workflow = new WorkflowDefinitionFile
        {
            DefinitionKey = "showwhen-ok",
            DisplayName = "ShowWhen OK",
            InitialState = "start",
            States =
            [
                new StepDefinition
                {
                    StateKey = "start",
                    DisplayName = "Start",
                    Components =
                    [
                        new TextInputComponent { FieldKey = "name", Label = "Name", Default = "Ada" },
                        new BodyComponent { Content = "hi", ShowWhen = "name = 'Ada'" }
                    ],
                    Routes = []
                }
            ]
        };

        var outcome = Service.Validate(workflow);

        outcome.IsValid.Should().BeTrue();
        outcome.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Validate_MoneyModeller_WithNoMockServiceInputs_IsValidWithOneServiceFieldWarning()
    {
        var workflow = LoadMoneyModeller();

        var outcome = Service.Validate(workflow);

        outcome.IsValid.Should().BeTrue(
            "an unresolved service field can't be verified statically, but that's a warning, not an error");
        outcome.Diagnostics.Should().ContainSingle(d =>
            d.Code == "CALC_SERVICE_FIELD_UNVERIFIED"
            && d.Path == "calculations.fields.member"
            && d.Severity == WorkflowDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Validate_MoneyModeller_WithMockServiceInputs_EvaluatesCalculationsAndShowWhenCleanly()
    {
        var workflow = LoadMoneyModeller();
        var mockServiceInputs = new Dictionary<string, object?>
        {
            ["member"] = new Dictionary<string, object?>
            {
                ["name"] = "Dr Sarah Mitchell",
                ["active"] = true,
                ["age"] = 47m,
                ["salary"] = 82_000m,
                ["accruedPension"] = 16_400m,
                ["accruedLump"] = 49_200m,
                ["dcPot"] = 48_300m
            }
        };

        var outcome = Service.Validate(workflow, mockServiceInputs);

        outcome.IsValid.Should().BeTrue();
        outcome.Diagnostics.Should().BeEmpty(
            "with the service field resolved, every calculation and showWhen on the real seed should evaluate cleanly");
    }

    private static WorkflowDefinitionFile LoadMoneyModeller()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "src", "UmbracoPrism.MockBusinessApp", "workflow-seeds", "money-modeller.json");
            if (File.Exists(candidate))
            {
                return JsonSerializer.Deserialize<WorkflowDefinitionFile>(
                    File.ReadAllText(candidate),
                    new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        PropertyNameCaseInsensitive = true,
                        AllowOutOfOrderMetadataProperties = true
                    })!;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("money-modeller.json not found walking up from test bin.");
    }

    private sealed class NotSupportedWorkflowSourceStore : IWorkflowSourceStore
    {
        public Task<IReadOnlyList<WorkflowSourceSummary>> ListAsync(CancellationToken ct = default) =>
            throw new NotSupportedException("Validate() does not touch the store.");

        public Task<WorkflowDefinitionFile?> LoadAsync(string definitionKey, CancellationToken ct = default) =>
            throw new NotSupportedException("Validate() does not touch the store.");

        public Task<WorkflowSaveResult> SaveAsync(WorkflowDefinitionFile workflow, int expectedVersion, CancellationToken ct = default) =>
            throw new NotSupportedException("Validate() does not touch the store.");

        public Task<bool> DeleteAsync(string definitionKey, CancellationToken ct = default) =>
            throw new NotSupportedException("Validate() does not touch the store.");
    }
}
