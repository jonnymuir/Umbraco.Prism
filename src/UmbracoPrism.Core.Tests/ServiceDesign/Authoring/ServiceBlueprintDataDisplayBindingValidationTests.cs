using FluentAssertions;
using UmbracoPrism.Shared.Models.ServiceDesign;
using UmbracoPrism.Shared.Models.ServiceDesign.Calculations;
using UmbracoPrism.Shared.Models.ServiceDesign.Components;

namespace UmbracoPrism.Core.Tests.ServiceDesign.Authoring;

/// <summary>
/// Validates <see cref="ServiceBlueprint.ValidateDataDisplayBindings"/> — every
/// stat-group item and chart series must bind to a field/series that actually exists.
/// </summary>
public class ServiceBlueprintDataDisplayBindingValidationTests
{
    [Fact]
    public void ValidateDataDisplayBindings_StatGroupBoundToCalculatedField_ReturnsNoErrors()
    {
        var workflow = new ServiceBlueprint
        {
            DefinitionKey = "test",
            DisplayName = "Test",
            InitialTouchpoint = "result",
            Touchpoints = [
                new StepDefinition
                {
                    TouchpointKey = "result",
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
            Calculations = new ServiceBlueprintCalculationSet
            {
                Fields = new Dictionary<string, ServiceBlueprintCalculationField>
                {
                    ["fee"] = new ServiceBlueprintCalculationField { Expr = "40" }
                }
            }
        };

        workflow.ValidateDataDisplayBindings().Should().BeEmpty(
            "the stat-group item binds to a real calculations.fields entry");
    }

    [Fact]
    public void ValidateDataDisplayBindings_StatGroupBoundToCapturedInput_ReturnsNoErrors()
    {
        var workflow = new ServiceBlueprint
        {
            DefinitionKey = "test",
            DisplayName = "Test",
            InitialTouchpoint = "capture",
            Touchpoints = [
                new StepDefinition
                {
                    TouchpointKey = "capture",
                    DisplayName = "Capture",
                    Components = [new NumberInputComponent { FieldKey = "binCount", Label = "Bins" }]
                },
                new StepDefinition
                {
                    TouchpointKey = "result",
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
        var workflow = new ServiceBlueprint
        {
            DefinitionKey = "test",
            DisplayName = "Test",
            InitialTouchpoint = "result",
            Touchpoints = [
                new StepDefinition
                {
                    TouchpointKey = "result",
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
        var workflow = new ServiceBlueprint
        {
            DefinitionKey = "test",
            DisplayName = "Test",
            InitialTouchpoint = "result",
            Touchpoints = [
                new StepDefinition
                {
                    TouchpointKey = "result",
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
        var workflow = new ServiceBlueprint
        {
            DefinitionKey = "test",
            DisplayName = "Test",
            InitialTouchpoint = "result",
            Touchpoints = [
                new StepDefinition
                {
                    TouchpointKey = "result",
                    DisplayName = "Result",
                    Components = [new ChartComponent { Title = "Projection", Series = "projection" }]
                }
            ],
            Calculations = new ServiceBlueprintCalculationSet
            {
                Series = new Dictionary<string, ServiceBlueprintCalculationSeries>
                {
                    ["projection"] = new ServiceBlueprintCalculationSeries()
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
        var workflow = new ServiceBlueprint
        {
            DefinitionKey = "test",
            DisplayName = "Test",
            InitialTouchpoint = "result",
            Touchpoints = [
                new StepDefinition
                {
                    TouchpointKey = "result",
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
        var workflow = new ServiceBlueprint
        {
            DefinitionKey = "test",
            DisplayName = "Test",
            InitialTouchpoint = "result",
            Touchpoints = [
                new StepDefinition
                {
                    TouchpointKey = "result",
                    DisplayName = "Result",
                    Components = [new StatGroupComponent { Title = "Fee", Items = [] }]
                }
            ]
        };

        workflow.ValidateDataDisplayBindings().Should().ContainSingle(d => d.Code == "DATA_DISPLAY_NO_ITEMS");
    }

    [Fact]
    public void ValidateDataDisplayBindings_SummaryListWithNoChildren_ReturnsError()
    {
        // Reproduces a real agent-authored bug: an agent asked to make a fee display "richer"
        // added a summary-list component but left its children empty — a calculation with
        // nothing rendering it, exactly the failure mode this whole validator exists to catch,
        // but summary-list wasn't checked at all until this test was added.
        var workflow = new ServiceBlueprint
        {
            DefinitionKey = "test",
            DisplayName = "Test",
            InitialTouchpoint = "result",
            Touchpoints = [
                new StepDefinition
                {
                    TouchpointKey = "result",
                    DisplayName = "Result",
                    Components = [new SummaryListComponent { Title = "Fee", Children = [] }]
                }
            ]
        };

        workflow.ValidateDataDisplayBindings().Should().ContainSingle(d => d.Code == "DATA_DISPLAY_NO_ITEMS");
    }

    [Fact]
    public void ValidateDataDisplayBindings_SummaryListChildBoundToUndefinedField_ReturnsError()
    {
        var workflow = new ServiceBlueprint
        {
            DefinitionKey = "test",
            DisplayName = "Test",
            InitialTouchpoint = "result",
            Touchpoints = [
                new StepDefinition
                {
                    TouchpointKey = "result",
                    DisplayName = "Result",
                    Components =
                    [
                        new SummaryListComponent
                        {
                            Title = "Fee",
                            Children = [new TextInputComponent { FieldKey = "fee", Label = "Fee" }]
                        }
                    ]
                }
            ]
        };

        var errors = workflow.ValidateDataDisplayBindings();

        errors.Should().ContainSingle(d =>
            d.Code == "DATA_DISPLAY_UNKNOWN_FIELD" &&
            d.Message.Contains("'fee'") &&
            d.Path == "states.result.components[0].children[0].fieldKey");
    }

    [Fact]
    public void ValidateDataDisplayBindings_SummaryListChildBoundToCalculatedField_ReturnsNoErrors()
    {
        var workflow = new ServiceBlueprint
        {
            DefinitionKey = "test",
            DisplayName = "Test",
            InitialTouchpoint = "result",
            Touchpoints = [
                new StepDefinition
                {
                    TouchpointKey = "result",
                    DisplayName = "Result",
                    Components =
                    [
                        new SummaryListComponent
                        {
                            Title = "Fee",
                            Children = [new TextInputComponent { FieldKey = "fee", Label = "Fee" }]
                        }
                    ]
                }
            ],
            Calculations = new ServiceBlueprintCalculationSet
            {
                Fields = new Dictionary<string, ServiceBlueprintCalculationField>
                {
                    ["fee"] = new ServiceBlueprintCalculationField { Expr = "40" }
                }
            }
        };

        workflow.ValidateDataDisplayBindings().Should().BeEmpty(
            "the summary-list child binds to a real calculations.fields entry");
    }

    [Fact]
    public void ValidateDataDisplayBindings_SummaryListChangeStateKeyPointsNowhere_ReturnsError()
    {
        var workflow = new ServiceBlueprint
        {
            DefinitionKey = "test",
            DisplayName = "Test",
            InitialTouchpoint = "capture",
            Touchpoints = [
                new StepDefinition { TouchpointKey = "capture", DisplayName = "Capture" },
                new StepDefinition
                {
                    TouchpointKey = "result",
                    DisplayName = "Result",
                    Components =
                    [
                        new SummaryListComponent
                        {
                            Title = "Details",
                            ChangeStateKey = "does-not-exist",
                            Children = [new TextInputComponent { FieldKey = "binCount", Label = "Bins" }]
                        }
                    ]
                }
            ]
        };

        var errors = workflow.ValidateDataDisplayBindings();

        errors.Should().ContainSingle(d =>
            d.Code == "DATA_DISPLAY_UNKNOWN_CHANGE_STATE" &&
            d.Path == "states.result.components[0].changeStateKey");
    }

    [Fact]
    public void ValidateDataDisplayBindings_SummaryListChildChangeStateKeyPointsNowhere_ReturnsError()
    {
        // Reproduces the real design gap this fixes: a summary-list whose rows summarise fields
        // captured on TWO different earlier stages (e.g. bin count on "how-many-bins", address on
        // "property-address") needs each row's own Change link to target the right one — a single
        // component-level ChangeStateKey can't do that.
        var workflow = new ServiceBlueprint
        {
            DefinitionKey = "test",
            DisplayName = "Test",
            InitialTouchpoint = "how-many-bins",
            Touchpoints = [
                new StepDefinition { TouchpointKey = "how-many-bins", DisplayName = "Bins" },
                new StepDefinition { TouchpointKey = "property-address", DisplayName = "Address" },
                new StepDefinition
                {
                    TouchpointKey = "result",
                    DisplayName = "Result",
                    Components =
                    [
                        new SummaryListComponent
                        {
                            Title = "Details",
                            Children =
                            [
                                new NumberInputComponent
                                {
                                    FieldKey = "binCount", Label = "Bins", ChangeStateKey = "how-many-bins"
                                },
                                new TextInputComponent
                                {
                                    FieldKey = "propertyAddress", Label = "Address", ChangeStateKey = "does-not-exist"
                                }
                            ]
                        }
                    ]
                }
            ]
        };

        var errors = workflow.ValidateDataDisplayBindings();

        errors.Should().ContainSingle(d =>
            d.Code == "DATA_DISPLAY_UNKNOWN_CHANGE_STATE" &&
            d.Path == "states.result.components[0].children[1].changeStateKey");
    }

    [Fact]
    public void ValidateDataDisplayBindings_SummaryListChildrenWithDifferentValidChangeStateKeys_ReturnsNoErrors()
    {
        var workflow = new ServiceBlueprint
        {
            DefinitionKey = "test",
            DisplayName = "Test",
            InitialTouchpoint = "how-many-bins",
            Touchpoints = [
                new StepDefinition
                {
                    TouchpointKey = "how-many-bins",
                    DisplayName = "Bins",
                    Components = [new NumberInputComponent { FieldKey = "binCount", Label = "Bins" }]
                },
                new StepDefinition
                {
                    TouchpointKey = "property-address",
                    DisplayName = "Address",
                    Components = [new TextInputComponent { FieldKey = "propertyAddress", Label = "Address" }]
                },
                new StepDefinition
                {
                    TouchpointKey = "result",
                    DisplayName = "Result",
                    Components =
                    [
                        new SummaryListComponent
                        {
                            Title = "Details",
                            Children =
                            [
                                new NumberInputComponent
                                {
                                    FieldKey = "binCount", Label = "Bins", ChangeStateKey = "how-many-bins"
                                },
                                new TextInputComponent
                                {
                                    FieldKey = "propertyAddress", Label = "Address", ChangeStateKey = "property-address"
                                }
                            ]
                        }
                    ]
                }
            ]
        };

        workflow.ValidateDataDisplayBindings().Should().BeEmpty(
            "each row's own changeStateKey points to a real state, and each fieldKey is a real captured input");
    }

    [Fact]
    public void ValidateDataDisplayBindings_ServiceFieldShadowsCapturedInput_ReturnsError()
    {
        // Reproduces a real agent-authored bug: marking the user's own captured input as
        // source: "service" makes it permanently unresolvable at runtime (no host supplies it),
        // even though the value is already automatically in scope under the same fieldKey.
        var workflow = new ServiceBlueprint
        {
            DefinitionKey = "test",
            DisplayName = "Test",
            InitialTouchpoint = "capture",
            Touchpoints = [
                new StepDefinition
                {
                    TouchpointKey = "capture",
                    DisplayName = "Capture",
                    Components = [new NumberInputComponent { FieldKey = "binCount", Label = "Bins" }]
                }
            ],
            Calculations = new ServiceBlueprintCalculationSet
            {
                Fields = new Dictionary<string, ServiceBlueprintCalculationField>
                {
                    ["binCount"] = new ServiceBlueprintCalculationField { Source = "service" }
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
        var workflow = new ServiceBlueprint
        {
            DefinitionKey = "test",
            DisplayName = "Test",
            InitialTouchpoint = "result",
            Touchpoints = [new StepDefinition { TouchpointKey = "result", DisplayName = "Result" }],
            Calculations = new ServiceBlueprintCalculationSet
            {
                Fields = new Dictionary<string, ServiceBlueprintCalculationField>
                {
                    ["member"] = new ServiceBlueprintCalculationField { Source = "service" }
                }
            }
        };

        workflow.ValidateDataDisplayBindings().Should().BeEmpty();
    }
}
