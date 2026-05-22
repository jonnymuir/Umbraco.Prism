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
        defs.TryGetProperty("transition", out _).Should().BeTrue();
        defs.TryGetProperty("action", out _).Should().BeTrue();
        defs.TryGetProperty("parameterSchema", out _).Should().BeTrue();
    }

    private static AuthoredWorkflow BuildValidWorkflow() => new()
    {
        DefinitionKey = "schema-validation",
        DisplayName = "Schema Validation",
        InitialStageKey = "details",
        Stages =
        [
            new AuthoredStage
            {
                StageKey = "details",
                DisplayName = "Details",
                Kind = StageKind.Question,
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
