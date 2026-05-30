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
    public void Project_TransitionReferencingUnknownStage_ReportsProj004Warning()
    {
        var result = _projector.Project(new AuthoredWorkflow
        {
            DefinitionKey = "validation-test",
            DisplayName = "Validation Test",
            InitialStageKey = "start",
            Stages = [new AuthoredStage { StageKey = "start", DisplayName = "Start" }],
            Gateways = [new AuthoredGateway { GatewayKey = "route", DisplayName = "Route", Kind = GatewayKind.Split, LaneKey = "applicant" }],
            Lanes = [new AuthoredLane { Key = "applicant", DisplayName = "Applicant" }],
            Transitions = [new AuthoredTransition { Source = "start", Target = "missing", Trigger = "continue" }]
        });

        result.HasErrors.Should().BeFalse("unknown transition targets are currently warnings, not blocking errors");
        result.Diagnostics.Should().ContainSingle(d => d.Code == "PROJ004" && d.StageKey == "missing");
    }

    [Fact]
    public void Project_DirectStageToStageTransition_ReportsProj141()
    {
        var result = _projector.Project(new AuthoredWorkflow
        {
            DefinitionKey = "validation-test",
            DisplayName = "Validation Test",
            InitialStageKey = "start",
            Stages =
            [
                new AuthoredStage { StageKey = "start", DisplayName = "Start" },
                new AuthoredStage { StageKey = "done", DisplayName = "Done", Kind = StageKind.Confirmation }
            ],
            Transitions = [new AuthoredTransition { Source = "start", Target = "done", Trigger = "continue" }]
        });

        result.HasErrors.Should().BeTrue();
        result.Diagnostics.Should().ContainSingle(d => d.Code == "PROJ141" && d.StageKey == "start");
    }

    [Fact]
    public void Project_StageWaitingMetadata_ReportsProj140()
    {
        // Retired stage kinds ("Waiting"/"StatusTimeline") and stage-level waiting payloads must be
        // rejected at the JSON boundary — the C# enum no longer carries these values, so we feed raw JSON.
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
              "actions": [],
              "waiting": { "content": "Hold on" }
            }
          ],
          "transitions": []
        }
        """;
        var authored = JsonSerializer.Deserialize<AuthoredWorkflow>(json)!;

        var result = _projector.Project(authored);

        result.HasErrors.Should().BeTrue();
        result.Diagnostics.Should().ContainSingle(d => d.Code == "PROJ140" && d.StageKey == "start");
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
    public void Project_DirectStageToStageRoute_InGatewayOnlyWorkflow_IsRejected()
    {
        var result = _projector.Project(new AuthoredWorkflow
        {
            DefinitionKey = "gateway-only-validation",
            DisplayName = "Gateway-only validation",
            InitialStageKey = "draft",
            Lanes =
            [
                new AuthoredLane { Key = "applicant", DisplayName = "Applicant", Actor = "applicant" },
                new AuthoredLane { Key = "caseworker", DisplayName = "Caseworker", Actor = "caseworker" }
            ],
            Gateways =
            [
                new AuthoredGateway
                {
                    GatewayKey = "decision-join",
                    DisplayName = "Decision join",
                    Kind = GatewayKind.Join,
                    LaneKey = "caseworker",
                    WaitingInfo = new WaitingMetadata
                    {
                        Content = "Waiting for the review to finish.",
                        ExpectedWaitSeconds = 120,
                        PollIntervalMs = 5000,
                        AllowDefer = false
                    },
                    RequiredIncomingLanes = ["applicant", "caseworker"]
                }
            ],
            Stages =
            [
                new AuthoredStage { StageKey = "draft", DisplayName = "Draft", Kind = StageKind.Question, LaneKey = "applicant" },
                new AuthoredStage { StageKey = "decision", DisplayName = "Decision", Kind = StageKind.Confirmation, LaneKey = "caseworker" }
            ],
            Transitions =
            [
                new AuthoredTransition { Source = "draft", Target = "decision", Trigger = "skip-gateway" }
            ]
        });

        result.HasErrors.Should().BeTrue(
            "gateway-only workflows should not jump straight from one stage to another");
        result.Diagnostics.Should().Contain(d => d.Message.Contains("gateway", StringComparison.OrdinalIgnoreCase),
            "authors should be told that routing belongs on gateways");
    }

    [Fact]
    public void Project_WaitingStage_InGatewayOnlyModel_IsRejected()
    {
        // Authoring docs that still carry the retired waiting-stage shape must be rejected at the JSON boundary.
        const string json = """
        {
          "definitionKey": "waiting-stage-validation",
          "displayName": "Waiting stage validation",
          "schemaVersion": "1.0",
          "initialStageKey": "draft",
          "lanes": [{ "key": "applicant", "title": "Applicant", "actor": "applicant" }],
          "stages": [
            { "key": "draft", "title": "Draft", "type": "Question", "laneKey": "applicant", "actions": [] },
            {
              "key": "wait-for-review",
              "title": "Wait for review",
              "type": "Waiting",
              "laneKey": "applicant",
              "actions": [],
              "waiting": {
                "content": "Waiting for a review.",
                "expectedWaitSeconds": 120,
                "pollIntervalMs": 5000,
                "allowDefer": false
              }
            }
          ],
          "transitions": [
            { "source": "draft", "target": "wait-for-review", "trigger": "continue", "conditions": [], "actions": [] }
          ]
        }
        """;
        var authored = JsonSerializer.Deserialize<AuthoredWorkflow>(json)!;

        var result = _projector.Project(authored);

        result.HasErrors.Should().BeTrue(
            "waiting belongs on join gateways in the corrected model");
        result.Diagnostics.Should().Contain(d => d.Code == "PROJ140",
            "PROJ140 should fire when stage-level waiting metadata or retired stage kinds appear");
    }

    [Fact]
    public void Project_GatewayToSplitGatewayTransition_ReportsProj142()
    {
        var result = _projector.Project(new AuthoredWorkflow
        {
            DefinitionKey = "gateway-target-validation",
            DisplayName = "Gateway target validation",
            InitialStageKey = "start",
            Lanes = [new AuthoredLane { Key = "applicant", DisplayName = "Applicant" }],
            Stages =
            [
                new AuthoredStage { StageKey = "start", DisplayName = "Start", LaneKey = "applicant" }
            ],
            Gateways =
            [
                new AuthoredGateway { GatewayKey = "split-a", DisplayName = "Split A", Kind = GatewayKind.Split, LaneKey = "applicant" },
                new AuthoredGateway { GatewayKey = "split-b", DisplayName = "Split B", Kind = GatewayKind.Split, LaneKey = "applicant" }
            ],
            Transitions =
            [
                new AuthoredTransition { Source = "start", Target = "split-a", Trigger = "continue" },
                // split-a → split-b is invalid: gateways may only target a stage or a join gateway.
                new AuthoredTransition { Source = "split-a", Target = "split-b", Trigger = "fan-out" }
            ]
        });

        result.HasErrors.Should().BeTrue();
        result.Diagnostics.Should().ContainSingle(d => d.Code == "PROJ142" && d.StageKey == "split-a");
    }
}
