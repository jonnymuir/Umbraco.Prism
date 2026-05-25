using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using UmbracoPrism.WorkflowEditor.Authoring;

namespace UmbracoPrism.Core.Tests.Workflow.Authoring;

public class AuthoredWorkflowSchemaValidationTests
{
    private readonly WorkflowProjector _projector = new();

    [Fact]
    public void Project_WithValidActionSchema_HasNoErrors()
    {
        var result = _projector.Project(BuildValidWorkflow());

        result.HasErrors.Should().BeFalse();
    }

    [Fact]
    public void Project_WithMissingRequiredActionParameter_ReturnsValidationError()
    {
        var workflow = BuildValidWorkflow() with
        {
            Stages =
            [
                BuildValidWorkflow().Stages[0] with
                {
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
        };

        var result = _projector.Project(workflow);

        result.HasErrors.Should().BeTrue();
        result.Diagnostics.Should().Contain(d => d.Code == "PROJ120");
    }

    [Fact]
    public void Project_WithStageActionUsingTransitionTiming_ReturnsValidationError()
    {
        var workflow = BuildValidWorkflow() with
        {
            Stages =
            [
                BuildValidWorkflow().Stages[0] with
                {
                    Actions =
                    [
                        new AuthoredAction
                        {
                            Type = "forms.load",
                            Timing = ActionTiming.OnTransition,
                            ParameterSchemaKey = "forms-form-definition",
                            Parameters = new JsonObject
                            {
                                ["formDefinitionId"] = "details-form"
                            }
                        }
                    ]
                }
            ]
        };

        var result = _projector.Project(workflow);

        result.HasErrors.Should().BeTrue();
        result.Diagnostics.Should().Contain(d => d.Code == "PROJ117");
    }

    [Fact]
    public void SchemaDocument_DefinesStageTransitionActionAndParameterContracts()
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "UmbracoPrism.WorkflowEditor", "Authoring", "Schemas", "authored-workflow.schema.json"));

        File.Exists(path).Should().BeTrue();

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var defs = document.RootElement.GetProperty("$defs");

        defs.TryGetProperty("stage", out _).Should().BeTrue();
        defs.TryGetProperty("lane", out _).Should().BeTrue();
        defs.TryGetProperty("gateway", out _).Should().BeTrue();
        defs.TryGetProperty("transition", out _).Should().BeTrue();
        defs.TryGetProperty("action", out _).Should().BeTrue();
        defs.TryGetProperty("parameterSchema", out _).Should().BeTrue();
    }

    [Fact]
    public void Project_WithUnknownStageLane_ReturnsValidationError()
    {
        var baseline = BuildValidWorkflow();
        var workflow = baseline with
        {
            Stages =
            [
                baseline.Stages[0] with
                {
                    LaneKey = "missing-lane"
                }
            ]
        };

        var result = _projector.Project(workflow);

        result.HasErrors.Should().BeTrue();
        result.Diagnostics.Should().Contain(d => d.Code == "PROJ129");
    }

    [Fact]
    public void Project_WithGatewayAndLane_PreservesLaneOwnedMetadata()
    {
        var result = _projector.Project(BuildValidWorkflow());

        result.HasErrors.Should().BeFalse();
        result.File.Metadata!.Lanes.Should().ContainSingle(lane => lane.Key == "applicant" && lane.Actor == "applicant");
        result.File.Metadata.Gateways.Should().ContainSingle(gateway =>
            gateway.Key == "review-split"
            && gateway.LaneKey == "applicant"
            && gateway.Actor == "applicant");
        result.File.States.Should().ContainSingle(state => state.StateKey == "details")
            .Which.Metadata!.LaneKey.Should().Be("applicant");
    }

    private static AuthoredWorkflow BuildValidWorkflow() => new()
    {
        DefinitionKey = "schema-validation",
        DisplayName = "Schema Validation",
        InitialStageKey = "details",
        Lanes =
        [
            new AuthoredLane
            {
                Key = "applicant",
                DisplayName = "Applicant lane",
                Actor = "applicant"
            }
        ],
        Gateways =
        [
            new AuthoredGateway
            {
                GatewayKey = "review-split",
                DisplayName = "Review split",
                Kind = GatewayKind.Split,
                LaneKey = "applicant"
            }
        ],
        Stages =
        [
            new AuthoredStage
            {
                StageKey = "details",
                DisplayName = "Details",
                Kind = StageKind.Question,
                LaneKey = "applicant",
                Actions =
                [
                    new AuthoredAction
                    {
                        Type = "forms.load",
                        Timing = ActionTiming.OnEntry,
                        ParameterSchemaKey = "forms-form-definition",
                        Parameters = new JsonObject
                        {
                            ["formDefinitionId"] = "details-form"
                        }
                    }
                ],
                Fields =
                [
                    new AuthoredField
                    {
                        Key = "full-name",
                        Label = "Full name",
                        Type = FieldType.Text,
                        Required = true
                    }
                ]
            }
        ],
        Transitions = [],
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
        ]
    };
}
