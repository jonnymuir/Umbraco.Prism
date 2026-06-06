using System.Text.Json.Nodes;
using FluentAssertions;
using UmbracoPrism.Shared.Models.Workflow.Components;
using UmbracoPrism.WorkflowEditor.Authoring;

namespace UmbracoPrism.Core.Tests.Workflow.Authoring;

public class AuthoredWorkflowSchemaValidationTests
{
    private readonly WorkflowProjector _projector = new();

    [Fact]
    public void Project_WithValidActionSchema_HasNoErrors()
    {
        _projector.Project(BuildValidWorkflow()).HasErrors.Should().BeFalse();
    }

    [Fact]
    public void Project_WithStateRouteToState_ReturnsValidationError()
    {
        var workflow = BuildValidWorkflow() with
        {
            Stages =
            [
                BuildValidWorkflow().Stages[0] with
                {
                    Routes =
                    [
                        new AuthoredRoute
                        {
                            Id = "bad-direct-route",
                            Target = "done",
                            Trigger = "continue"
                        }
                    ]
                },
                BuildValidWorkflow().Stages[1]
            ]
        };

        var result = _projector.Project(workflow);
        result.HasErrors.Should().BeTrue();
        result.Diagnostics.Should().Contain(d => d.Code == "PROJ157");
    }

    [Fact]
    public void SchemaDocument_DefinesStageQueueGatewayAndParameterContracts()
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "UmbracoPrism.WorkflowEditor", "Authoring", "Schemas", "authored-workflow.schema.json"));

        using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
        var defs = document.RootElement.GetProperty("$defs");

        defs.TryGetProperty("stage", out _).Should().BeTrue();
        defs.TryGetProperty("queue", out _).Should().BeTrue();
        defs.TryGetProperty("gateway", out _).Should().BeTrue();
        defs.TryGetProperty("route", out _).Should().BeTrue();
        defs.TryGetProperty("action", out _).Should().BeTrue();
        defs.TryGetProperty("parameterSchema", out _).Should().BeTrue();
    }

    [Fact]
    public void Project_WithUnknownStageQueue_ReturnsValidationError()
    {
        var baseline = BuildValidWorkflow();
        var workflow = baseline with
        {
            Stages =
            [
                baseline.Stages[0] with
                {
                    QueueKey = "missing-queue"
                },
                baseline.Stages[1]
            ]
        };

        var result = _projector.Project(workflow);
        result.HasErrors.Should().BeTrue();
        result.Diagnostics.Should().Contain(d => d.Code == "PROJ129");
    }

    [Fact]
    public void Project_WithGatewayAndQueue_PreservesQueueOwnedMetadata()
    {
        var result = _projector.Project(BuildValidWorkflow());

        result.File.Queues.Should().ContainSingle(queue => queue.Key == "web-user" && queue.Actor == "applicant");
        result.File.Gateways.Should().ContainSingle(gateway =>
            gateway.Key == "review-split"
            && gateway.QueueKey == "web-user"
            && gateway.Actor == "applicant");
        result.File.States.Should().ContainSingle(state => state.StateKey == "details")
            .Which.QueueKey.Should().Be("web-user");
    }

    private static AuthoredWorkflow BuildValidWorkflow() => new()
    {
        DefinitionKey = "schema-validation",
        DisplayName = "Schema Validation",
        InitialStageKey = "details",
        Queues =
        [
            new AuthoredQueue
            {
                Key = "web-user",
                DisplayName = "Applicant",
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
                QueueKey = "web-user",
                Routes = [new AuthoredRoute { Id = "to-done", Target = "done", Trigger = "continue" }]
            }
        ],
        Stages =
        [
            new AuthoredStage
            {
                StageKey = "details",
                DisplayName = "Details",
                Kind = StageKind.Question,
                QueueKey = "web-user",
                Routes = [new AuthoredRoute { Id = "details-continue-gateway", Target = "review-split", Trigger = "continue" }],
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
                Components =
                [
                    new FieldsetComponent
                    {
                        Children =
                        [
                            new TextInputComponent
                            {
                                FieldKey = "full-name",
                                Label = "Full name",
                                Required = true
                            }
                        ]
                    }
                ]
            },
            new AuthoredStage
            {
                StageKey = "done",
                DisplayName = "Done",
                Kind = StageKind.Confirmation,
                QueueKey = "web-user"
            }
        ],
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
