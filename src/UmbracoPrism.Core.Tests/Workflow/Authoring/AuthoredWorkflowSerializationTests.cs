using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using UmbracoPrism.Shared.Models.Workflow.Components;
using UmbracoPrism.WorkflowEditor.Authoring;

namespace UmbracoPrism.Core.Tests.Workflow.Authoring;

public class AuthoredWorkflowSerializationTests
{
    private static readonly JsonSerializerOptions RoundTripOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    [Fact]
    public void AuthoredWorkflow_RoundTripsWithoutDataLoss()
    {
        var original = BuildTestWorkflow();

        var json = JsonSerializer.Serialize(original, RoundTripOptions);
        var restored = JsonSerializer.Deserialize<AuthoredWorkflow>(json, RoundTripOptions)!;

        restored.Queues.Should().ContainSingle();
        restored.Queues[0].Key.Should().Be("web-user");

        restored.Stages.Should().HaveCount(2);
        restored.Stages[0].QueueKey.Should().Be("web-user");
        restored.Stages[0].Routes.Should().ContainSingle(route =>
            route.Target == "route-submit" && route.Trigger == "submit");

        restored.Gateways.Should().ContainSingle();
        var gateway = restored.Gateways[0];
        gateway.GatewayKey.Should().Be("route-submit");
        gateway.QueueKey.Should().Be("web-user");
        gateway.Routes.Should().ContainSingle(route =>
            route.Target == "done" && route.Trigger == "submit");
    }

    [Fact]
    public void AuthoredWorkflow_SerializesWithLockedQueueOnlyPropertyNames()
    {
        var workflow = BuildTestWorkflow();
        var json = JsonSerializer.Serialize(workflow, RoundTripOptions);
        using var document = JsonDocument.Parse(json);

        var root = document.RootElement;
        root.TryGetProperty("queues", out _).Should().BeTrue();
        root.TryGetProperty("lanes", out _).Should().BeFalse();
        root.TryGetProperty("transitions", out _).Should().BeFalse();

        var stage = root.GetProperty("stages")[0];
        stage.TryGetProperty("queueKey", out _).Should().BeTrue();
        stage.TryGetProperty("routes", out _).Should().BeTrue();
        stage.TryGetProperty("laneKey", out _).Should().BeFalse();

        var gateway = root.GetProperty("gateways")[0];
        gateway.TryGetProperty("queueKey", out _).Should().BeTrue();
        gateway.TryGetProperty("routes", out _).Should().BeTrue();
        gateway.TryGetProperty("source", out _).Should().BeFalse();
    }

    [Fact]
    public void AuthoredStage_ComponentsRoundTrip_PolymorphicTree()
    {
        var stage = new AuthoredStage
        {
            StageKey = "details",
            DisplayName = "Your details",
            Kind = StageKind.Question,
            QueueKey = "web-user",
            Components =
            [
                new BodyComponent { Content = "Tell us about yourself." },
                new FieldsetComponent
                {
                    Legend = "Identity",
                    LegendSize = "m",
                    Children =
                    [
                        new TextInputComponent { FieldKey = "name", Label = "Full name", Required = true },
                        new EmailComponent { FieldKey = "email", Label = "Email", Required = true }
                    ]
                }
            ]
        };

        var json = JsonSerializer.Serialize(stage, RoundTripOptions);
        var restored = JsonSerializer.Deserialize<AuthoredStage>(json, RoundTripOptions)!;

        restored.QueueKey.Should().Be("web-user");
        restored.Components.Should().HaveCount(2);
    }

    [Fact]
    public async Task FilesystemStore_LoadsFixtureDocument()
    {
        var fixturesPath = WorkflowAuthoringFixtureLocator.GetFixturesPath();
        var workflow = await AuthoredWorkflowFixtureLoader.LoadAsync(fixturesPath, "planning");

        workflow.Should().NotBeNull();
        workflow!.DefinitionKey.Should().Be("planning-application");
        workflow.Queues.Should().NotBeEmpty();
        workflow.Stages.Should().HaveCount(4);
    }

    private static AuthoredWorkflow BuildTestWorkflow() => new()
    {
        Id = new Guid("aaaabbbb-cccc-dddd-eeee-ffffffffffff"),
        DefinitionKey = "test-workflow",
        DisplayName = "Test Workflow",
        Version = 1,
        Description = "A minimal workflow for serialization tests.",
        SchemaVersion = "1.0",
        InitialStageKey = "details",
        InstancePolicy = "single",
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
                GatewayKey = "route-submit",
                DisplayName = "Route to completion",
                Kind = GatewayKind.Split,
                QueueKey = "web-user",
                Routes =
                [
                    new AuthoredRoute
                    {
                        Id = "gateway-submit-done",
                        Target = "done",
                        Trigger = "submit",
                        Condition = new AuthoredCondition
                        {
                            Expression = "form.isValid == true",
                            Description = "Only submit valid forms."
                        },
                        Actions =
                        [
                            new AuthoredAction
                            {
                                Type = "forms.submit",
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
            }
        ],
        Stages =
        [
            new AuthoredStage
            {
                StageKey = "details",
                DisplayName = "Your details",
                Kind = StageKind.Question,
                QueueKey = "web-user",
                Routes =
                [
                    new AuthoredRoute
                    {
                        Id = "details-submit-route",
                        Target = "route-submit",
                        Trigger = "submit"
                    }
                ],
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
                DisplayName = "Complete",
                Kind = StageKind.Confirmation,
                QueueKey = "web-user"
            }
        ],
        ParameterSchemas =
        [
            new AuthoredParameterSchema
            {
                Key = "forms-form-definition",
                AppliesTo = ["forms.load", "forms.submit"],
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
        Metadata = new Dictionary<string, string> { ["owner"] = "test-team" }
    };
}
