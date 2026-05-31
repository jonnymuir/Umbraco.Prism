using System.Text.Json;
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
    public void Project_RouteTargetUnknown_ReportsProj150()
    {
        var result = _projector.Project(new AuthoredWorkflow
        {
            DefinitionKey = "validation-test",
            DisplayName = "Validation Test",
            InitialStageKey = "start",
            Lanes = [new AuthoredLane { Key = "applicant", DisplayName = "Applicant" }],
            Stages = [new AuthoredStage { StageKey = "start", DisplayName = "Start", LaneKey = "applicant" }],
            Gateways =
            [
                new AuthoredGateway
                {
                    GatewayKey = "route",
                    DisplayName = "Route",
                    Kind = GatewayKind.Split,
                    LaneKey = "applicant",
                    Source = "start",
                    Routes = [new AuthoredRoute { Id = "r1", Target = "missing-stage", Trigger = "continue" }]
                }
            ]
        });

        result.HasErrors.Should().BeTrue();
        result.Diagnostics.Should().Contain(d => d.Code == "PROJ150" && d.Message.Contains("missing-stage"));
    }

    [Fact]
    public void Project_SplitGatewayWithoutSource_ReportsProj141()
    {
        var result = _projector.Project(new AuthoredWorkflow
        {
            DefinitionKey = "validation-test",
            DisplayName = "Validation Test",
            InitialStageKey = "start",
            Lanes = [new AuthoredLane { Key = "applicant", DisplayName = "Applicant" }],
            Stages = [new AuthoredStage { StageKey = "start", DisplayName = "Start", LaneKey = "applicant" }],
            Gateways =
            [
                new AuthoredGateway
                {
                    GatewayKey = "sourceless",
                    DisplayName = "Sourceless",
                    Kind = GatewayKind.Split,
                    LaneKey = "applicant",
                    Routes = [new AuthoredRoute { Id = "r1", Target = "start", Trigger = "loop" }]
                }
            ]
        });

        result.HasErrors.Should().BeTrue();
        result.Diagnostics.Should().Contain(d => d.Code == "PROJ141");
    }

    [Fact]
    public void Project_TwoSplitGatewaysShareSourceStage_ReportsProj143()
    {
        var result = _projector.Project(new AuthoredWorkflow
        {
            DefinitionKey = "validation-test",
            DisplayName = "Validation Test",
            InitialStageKey = "start",
            Lanes = [new AuthoredLane { Key = "applicant", DisplayName = "Applicant" }],
            Stages =
            [
                new AuthoredStage { StageKey = "start", DisplayName = "Start", LaneKey = "applicant" },
                new AuthoredStage { StageKey = "a", DisplayName = "A", Kind = StageKind.Confirmation, LaneKey = "applicant" },
                new AuthoredStage { StageKey = "b", DisplayName = "B", Kind = StageKind.Confirmation, LaneKey = "applicant" }
            ],
            Gateways =
            [
                new AuthoredGateway
                {
                    GatewayKey = "first",
                    DisplayName = "First",
                    Kind = GatewayKind.Split,
                    LaneKey = "applicant",
                    Source = "start",
                    Routes = [new AuthoredRoute { Id = "r1", Target = "a", Trigger = "continue" }]
                },
                new AuthoredGateway
                {
                    GatewayKey = "second",
                    DisplayName = "Second",
                    Kind = GatewayKind.Split,
                    LaneKey = "applicant",
                    Source = "start",
                    Routes = [new AuthoredRoute { Id = "r2", Target = "b", Trigger = "branch" }]
                }
            ]
        });

        result.HasErrors.Should().BeTrue();
        result.Diagnostics.Should().Contain(d => d.Code == "PROJ143");
    }

    [Fact]
    public void Project_GatewayWithNoRoutes_ReportsProj144()
    {
        var result = _projector.Project(new AuthoredWorkflow
        {
            DefinitionKey = "validation-test",
            DisplayName = "Validation Test",
            InitialStageKey = "start",
            Lanes = [new AuthoredLane { Key = "applicant", DisplayName = "Applicant" }],
            Stages = [new AuthoredStage { StageKey = "start", DisplayName = "Start", LaneKey = "applicant" }],
            Gateways =
            [
                new AuthoredGateway
                {
                    GatewayKey = "empty",
                    DisplayName = "Empty",
                    Kind = GatewayKind.Split,
                    LaneKey = "applicant",
                    Source = "start"
                }
            ]
        });

        result.HasErrors.Should().BeTrue();
        result.Diagnostics.Should().Contain(d => d.Code == "PROJ144");
    }

    [Fact]
    public void Project_StageWithUnknownKind_ReportsProj005()
    {
        // Stage kind is a closed enum (Question, CheckAnswers, Confirmation, TaskList).
        // The retired "Waiting" / "StatusTimeline" tokens — and any other unknown
        // token — must surface a clear PROJ005 error rather than a silent rewrite.
        const string json = """
        {
          "definitionKey": "validation-test",
          "displayName": "Validation Test",
          "schemaVersion": "1.0",
          "initialStageKey": "start",
          "stages": [
            {
              "key": "start",
              "title": "Start",
              "type": "Waiting",
              "actions": []
            }
          ]
        }
        """;
        var authored = JsonSerializer.Deserialize<AuthoredWorkflow>(json)!;

        var result = _projector.Project(authored);

        result.HasErrors.Should().BeTrue();
        result.Diagnostics.Should().ContainSingle(d => d.Code == "PROJ005" && d.StageKey == "start")
            .Which.Message.Should().Contain("Waiting");
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

    [Fact]
    public void Project_JoinGatewayWithSource_ReportsProj152()
    {
        var result = _projector.Project(new AuthoredWorkflow
        {
            DefinitionKey = "validation-test",
            DisplayName = "Validation Test",
            InitialStageKey = "start",
            Lanes = [new AuthoredLane { Key = "applicant", DisplayName = "Applicant" }],
            Stages =
            [
                new AuthoredStage { StageKey = "start", DisplayName = "Start", LaneKey = "applicant" },
                new AuthoredStage { StageKey = "end", DisplayName = "End", Kind = StageKind.Confirmation, LaneKey = "applicant" }
            ],
            Gateways =
            [
                new AuthoredGateway
                {
                    GatewayKey = "bad-join",
                    DisplayName = "Bad join",
                    Kind = GatewayKind.Join,
                    LaneKey = "applicant",
                    Source = "start", // join must not declare a source
                    RequiredIncomingLanes = ["applicant"],
                    WaitingInfo = new WaitingMetadata { Content = "wait" },
                    Routes = [new AuthoredRoute { Id = "r", Target = "end", Trigger = "release" }]
                }
            ]
        });

        result.HasErrors.Should().BeTrue();
        result.Diagnostics.Should().Contain(d => d.Code == "PROJ152");
    }
}
