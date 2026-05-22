using System.Text.Json.Nodes;
using FluentAssertions;
using UmbracoPrism.WorkflowEditor.Authoring;

namespace UmbracoPrism.Core.Tests.Workflow.Authoring;

public class AuthoredWorkflowValidationTests
{
    private readonly WorkflowProjector _projector = new();

    [Fact]
    public void Project_EmptyStageKey_ReportsProj001()
    {
        var result = _projector.Project(new AuthoredWorkflow
        {
            DefinitionKey = "validation-test",
            DisplayName = "Validation Test",
            InitialStageKey = "start",
            Stages = [new AuthoredStage { StageKey = "", DisplayName = "Start" }]
        });

        result.HasErrors.Should().BeTrue();
        result.Diagnostics.Should().ContainSingle(d => d.Code == "PROJ001");
    }

    [Fact]
    public void Project_DuplicateStageKeys_ReportProj002()
    {
        var result = _projector.Project(new AuthoredWorkflow
        {
            DefinitionKey = "validation-test",
            DisplayName = "Validation Test",
            InitialStageKey = "start",
            Stages =
            [
                new AuthoredStage { StageKey = "start", DisplayName = "Start" },
                new AuthoredStage { StageKey = "start", DisplayName = "Duplicate start" }
            ]
        });

        result.HasErrors.Should().BeTrue();
        result.Diagnostics.Should().ContainSingle(d => d.Code == "PROJ002" && d.StageKey == "start");
    }

    [Fact]
    public void Project_MissingInitialStage_ReportsProj003()
    {
        var result = _projector.Project(new AuthoredWorkflow
        {
            DefinitionKey = "validation-test",
            DisplayName = "Validation Test",
            InitialStageKey = "missing",
            Stages = [new AuthoredStage { StageKey = "start", DisplayName = "Start" }]
        });

        result.HasErrors.Should().BeTrue();
        result.Diagnostics.Should().ContainSingle(d => d.Code == "PROJ003");
    }

    [Fact]
    public void Project_TransitionReferencingUnknownStage_ReportsProj004Warning()
    {
        var result = _projector.Project(new AuthoredWorkflow
        {
            DefinitionKey = "validation-test",
            DisplayName = "Validation Test",
            InitialStageKey = "start",
            Stages = [new AuthoredStage { StageKey = "start", DisplayName = "Start" }],
            Transitions = [new AuthoredTransition { FromStage = "start", ToStage = "missing", Action = "continue" }]
        });

        result.HasErrors.Should().BeFalse("unknown transition targets are currently warnings, not blocking errors");
        result.Diagnostics.Should().ContainSingle(d => d.Code == "PROJ004" && d.StageKey == "missing");
    }

    [Fact]
    public void Project_ActionReferencingUnknownParameterSchema_ReportsProj118()
    {
        var result = _projector.Project(new AuthoredWorkflow
        {
            DefinitionKey = "validation-test",
            DisplayName = "Validation Test",
            InitialStageKey = "start",
            Stages =
            [
                new AuthoredStage
                {
                    StageKey = "start",
                    DisplayName = "Start",
                    Actions =
                    [
                        new AuthoredAction
                        {
                            Type = "forms.load",
                            Timing = ActionTiming.OnEntry,
                            ParameterSchemaKey = "missing-schema"
                        }
                    ]
                }
            ]
        });

        result.HasErrors.Should().BeTrue();
        result.Diagnostics.Should().ContainSingle(d => d.Code == "PROJ118" && d.StageKey == "start");
    }

    [Fact]
    public void Project_ActionMissingRequiredParameter_ReportsProj120()
    {
        var result = _projector.Project(new AuthoredWorkflow
        {
            DefinitionKey = "validation-test",
            DisplayName = "Validation Test",
            InitialStageKey = "start",
            ParameterSchemas =
            [
                new AuthoredParameterSchema
                {
                    Key = "forms-form-definition",
                    AppliesTo = ["forms.load"],
                    AllowAdditionalProperties = false,
                    Properties =
                    [
                        new AuthoredParameterDefinition
                        {
                            Key = "formDefinitionId",
                            ValueKind = ParameterValueKind.String
                        }
                    ],
                    Required = ["formDefinitionId"]
                }
            ],
            Stages =
            [
                new AuthoredStage
                {
                    StageKey = "start",
                    DisplayName = "Start",
                    Actions =
                    [
                        new AuthoredAction
                        {
                            Type = "forms.load",
                            Timing = ActionTiming.OnEntry,
                            ParameterSchemaKey = "forms-form-definition",
                            Parameters = new JsonObject()
                        }
                    ]
                }
            ]
        });

        result.HasErrors.Should().BeTrue();
        result.Diagnostics.Should().ContainSingle(d => d.Code == "PROJ120" && d.StageKey == "start");
    }
}
