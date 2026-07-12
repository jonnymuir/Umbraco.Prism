using FluentAssertions;
using UmbracoPrism.Shared.Models.Workflow;
using UmbracoPrism.Shared.Models.Workflow.Calculations;
using UmbracoPrism.Shared.Models.Workflow.Components;

namespace UmbracoPrism.Core.Tests.Workflow.Authoring;

/// <summary>
/// Validates <see cref="WorkflowDefinitionFile.ValidateDataDisplayBindings"/> — every
/// stat-group item and chart series must bind to a field/series that actually exists.
/// </summary>
public class WorkflowDataDisplayBindingValidationTests
{
    [Fact]
    public void ValidateDataDisplayBindings_StatGroupBoundToCalculatedField_ReturnsNoErrors()
    {
        var workflow = new WorkflowDefinitionFile
        {
            DefinitionKey = "test",
            DisplayName = "Test",
            InitialState = "result",
            States =
            [
                new StepDefinition
                {
                    StateKey = "result",
                    DisplayName = "Result",
                    Components =
                    [
                        new StatGroupComponent
                        {
                            Items = [new StatItemDefinition { Label = "Fee", FieldKey = "fee" }]
                        }
                    ]
                }
            ],
            Calculations = new WorkflowCalculationSet
            {
                Fields = new Dictionary<string, WorkflowCalculationField>
                {
                    ["fee"] = new WorkflowCalculationField { Expr = "40" }
                }
            }
        };

        workflow.ValidateDataDisplayBindings().Should().BeEmpty(
            "the stat-group item binds to a real calculations.fields entry");
    }

    [Fact]
    public void ValidateDataDisplayBindings_StatGroupBoundToCapturedInput_ReturnsNoErrors()
    {
        var workflow = new WorkflowDefinitionFile
        {
            DefinitionKey = "test",
            DisplayName = "Test",
            InitialState = "capture",
            States =
            [
                new StepDefinition
                {
                    StateKey = "capture",
                    DisplayName = "Capture",
                    Components = [new NumberInputComponent { FieldKey = "binCount", Label = "Bins" }]
                },
                new StepDefinition
                {
                    StateKey = "result",
                    DisplayName = "Result",
                    Components =
                    [
                        new StatGroupComponent
                        {
                            Items = [new StatItemDefinition { Label = "Bins", FieldKey = "binCount" }]
                        }
                    ]
                }
            ]
        };

        workflow.ValidateDataDisplayBindings().Should().BeEmpty(
            "the stat-group item binds to a real captured input field");
    }

    [Fact]
    public void ValidateDataDisplayBindings_StatGroupBoundToUndefinedField_ReturnsError()
    {
        var workflow = new WorkflowDefinitionFile
        {
            DefinitionKey = "test",
            DisplayName = "Test",
            InitialState = "result",
            States =
            [
                new StepDefinition
                {
                    StateKey = "result",
                    DisplayName = "Result",
                    Components =
                    [
                        new StatGroupComponent
                        {
                            Items = [new StatItemDefinition { Label = "Fee", FieldKey = "fee" }]
                        }
                    ]
                }
            ]
        };

        var errors = workflow.ValidateDataDisplayBindings();

        errors.Should().ContainSingle(d =>
            d.Code == "DATA_DISPLAY_UNKNOWN_FIELD" &&
            d.Message.Contains("'fee'") &&
            d.Path == "states.result.components[0].items[0].fieldKey");
    }

    [Fact]
    public void ValidateDataDisplayBindings_ChartBoundToUndefinedSeries_ReturnsError()
    {
        var workflow = new WorkflowDefinitionFile
        {
            DefinitionKey = "test",
            DisplayName = "Test",
            InitialState = "result",
            States =
            [
                new StepDefinition
                {
                    StateKey = "result",
                    DisplayName = "Result",
                    Components = [new ChartComponent { Title = "Projection", Series = "projection" }]
                }
            ]
        };

        var errors = workflow.ValidateDataDisplayBindings();

        errors.Should().ContainSingle(d =>
            d.Code == "DATA_DISPLAY_UNKNOWN_FIELD" &&
            d.Message.Contains("'projection'"));
    }

    [Fact]
    public void ValidateDataDisplayBindings_ChartBoundToCalculatedSeries_ReturnsNoErrors()
    {
        var workflow = new WorkflowDefinitionFile
        {
            DefinitionKey = "test",
            DisplayName = "Test",
            InitialState = "result",
            States =
            [
                new StepDefinition
                {
                    StateKey = "result",
                    DisplayName = "Result",
                    Components = [new ChartComponent { Title = "Projection", Series = "projection" }]
                }
            ],
            Calculations = new WorkflowCalculationSet
            {
                Series = new Dictionary<string, WorkflowCalculationSeries>
                {
                    ["projection"] = new WorkflowCalculationSeries()
                }
            }
        };

        workflow.ValidateDataDisplayBindings().Should().BeEmpty(
            "the chart binds to a real calculations.series entry");
    }

    [Fact]
    public void ValidateDataDisplayBindings_PlainTextComponentDisplayingFee_IsNotFlagged()
    {
        // The real gap this validator can't close: a plain text INPUT component reused as a
        // "display" is structurally valid (it's a legitimate input), so it can't be flagged —
        // only stat-group/chart bindings are checked. Documented here so the limitation is explicit.
        var workflow = new WorkflowDefinitionFile
        {
            DefinitionKey = "test",
            DisplayName = "Test",
            InitialState = "result",
            States =
            [
                new StepDefinition
                {
                    StateKey = "result",
                    DisplayName = "Result",
                    Components = [new TextInputComponent { FieldKey = "feeDisplay", Label = "Your fee" }]
                }
            ]
        };

        workflow.ValidateDataDisplayBindings().Should().BeEmpty();
    }

    [Fact]
    public void ValidateDataDisplayBindings_StatGroupWithNoItems_ReturnsError()
    {
        // Reproduces a real agent-authored bug: a stat-group saved with zero items renders
        // nothing at all, but nothing about the shape itself is invalid, so this needs its own check.
        var workflow = new WorkflowDefinitionFile
        {
            DefinitionKey = "test",
            DisplayName = "Test",
            InitialState = "result",
            States =
            [
                new StepDefinition
                {
                    StateKey = "result",
                    DisplayName = "Result",
                    Components = [new StatGroupComponent { Title = "Fee", Items = [] }]
                }
            ]
        };

        workflow.ValidateDataDisplayBindings().Should().ContainSingle(d => d.Code == "DATA_DISPLAY_NO_ITEMS");
    }

    [Fact]
    public void ValidateDataDisplayBindings_ServiceFieldShadowsCapturedInput_ReturnsError()
    {
        // Reproduces a real agent-authored bug: marking the user's own captured input as
        // source: "service" makes it permanently unresolvable at runtime (no host supplies it),
        // even though the value is already automatically in scope under the same fieldKey.
        var workflow = new WorkflowDefinitionFile
        {
            DefinitionKey = "test",
            DisplayName = "Test",
            InitialState = "capture",
            States =
            [
                new StepDefinition
                {
                    StateKey = "capture",
                    DisplayName = "Capture",
                    Components = [new NumberInputComponent { FieldKey = "binCount", Label = "Bins" }]
                }
            ],
            Calculations = new WorkflowCalculationSet
            {
                Fields = new Dictionary<string, WorkflowCalculationField>
                {
                    ["binCount"] = new WorkflowCalculationField { Source = "service" }
                }
            }
        };

        var errors = workflow.ValidateDataDisplayBindings();

        errors.Should().ContainSingle(d =>
            d.Code == "CALC_FIELD_SHADOWS_INPUT" && d.Path == "calculations.fields.binCount");
    }

    [Fact]
    public void ValidateDataDisplayBindings_ServiceFieldNotShadowingAnyInput_ReturnsNoErrors()
    {
        // money-modeller's own pattern: "member" is genuinely service-sourced (an external
        // lookup), not a captured input anywhere in the workflow, so it's not flagged.
        var workflow = new WorkflowDefinitionFile
        {
            DefinitionKey = "test",
            DisplayName = "Test",
            InitialState = "result",
            States = [new StepDefinition { StateKey = "result", DisplayName = "Result" }],
            Calculations = new WorkflowCalculationSet
            {
                Fields = new Dictionary<string, WorkflowCalculationField>
                {
                    ["member"] = new WorkflowCalculationField { Source = "service" }
                }
            }
        };

        workflow.ValidateDataDisplayBindings().Should().BeEmpty();
    }
}
