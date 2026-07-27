using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using UmbracoPrism.ServiceBlueprintEditor.Authoring;

namespace UmbracoPrism.Core.Tests.ServiceDesign.Authoring;

public class AuthoredServiceBlueprintValidationTests
{
    private readonly ServiceBlueprintProjector _projector = new();

    [Fact]
    public void Project_EmptyStageKey_ReportsProj001()
    {
        var result = _projector.Project(new AuthoredServiceBlueprint
        {
            DefinitionKey = "validation-test",
            DisplayName = "Validation Test",
            InitialTouchpointKey = "start",
            Touchpoints = [new AuthoredTouchpoint { TouchpointKey = "", DisplayName = "Start" }]
        });

        result.Diagnostics.Should().ContainSingle(d => d.Code == "PROJ001");
    }

    [Fact]
    public void Project_StateRouteTargetUnknownGateway_ReportsProj157()
    {
        var result = _projector.Project(new AuthoredServiceBlueprint
        {
            DefinitionKey = "validation-test",
            DisplayName = "Validation Test",
            InitialTouchpointKey = "start",
            Queues = [new AuthoredQueue { Key = "web-user", DisplayName = "Applicant" }],
            Touchpoints =
            [
                new AuthoredTouchpoint
                {
                    TouchpointKey = "start",
                    DisplayName = "Start",
                    QueueKey = "web-user",
                    Routes =
                    [
                        new AuthoredRoute
                        {
                            Id = "bad-route",
                            Target = "missing-gateway",
                            Trigger = "continue"
                        }
                    ]
                }
            ]
        });

        result.Diagnostics.Should().Contain(d => d.Code == "PROJ157");
    }

    [Fact]
    public void Project_GatewayRouteTargetUnknown_ReportsProj150()
    {
        var result = _projector.Project(new AuthoredServiceBlueprint
        {
            DefinitionKey = "validation-test",
            DisplayName = "Validation Test",
            InitialTouchpointKey = "start",
            Queues = [new AuthoredQueue { Key = "web-user", DisplayName = "Applicant" }],
            Touchpoints =
            [
                new AuthoredTouchpoint
                {
                    TouchpointKey = "start",
                    DisplayName = "Start",
                    QueueKey = "web-user",
                    Routes = [new AuthoredRoute { Id = "to-gateway", Target = "route", Trigger = "continue" }]
                }
            ],
            Gateways =
            [
                new AuthoredGateway
                {
                    GatewayKey = "route",
                    DisplayName = "Route",
                    Kind = GatewayKind.Split,
                    QueueKey = "web-user",
                    Routes = [new AuthoredRoute { Id = "r1", Target = "missing-stage", Trigger = "continue" }]
                }
            ]
        });

        result.Diagnostics.Should().Contain(d => d.Code == "PROJ150");
    }

    [Fact]
    public void Project_JoinGatewayWithoutRequiredIncomingQueues_ReportsProj138()
    {
        var result = _projector.Project(new AuthoredServiceBlueprint
        {
            DefinitionKey = "validation-test",
            DisplayName = "Validation Test",
            InitialTouchpointKey = "start",
            Queues = [new AuthoredQueue { Key = "web-user", DisplayName = "Applicant" }],
            Touchpoints =
            [
                new AuthoredTouchpoint
                {
                    TouchpointKey = "start",
                    DisplayName = "Start",
                    QueueKey = "web-user",
                    Routes = [new AuthoredRoute { Id = "to-join", Target = "join", Trigger = "continue" }]
                },
                new AuthoredTouchpoint
                {
                    TouchpointKey = "end",
                    DisplayName = "End",
                    QueueKey = "web-user"
                }
            ],
            Gateways =
            [
                new AuthoredGateway
                {
                    GatewayKey = "join",
                    DisplayName = "Join",
                    Kind = GatewayKind.Join,
                    QueueKey = "web-user",
                    WaitingInfo = new WaitingMetadata { Content = "Waiting." },
                    Routes = [new AuthoredRoute { Id = "release", Target = "end", Trigger = "release" }]
                }
            ]
        });

        result.Diagnostics.Should().Contain(d => d.Code == "PROJ138");
    }

    [Fact]
    public void Project_StageWithUnknownKind_ReportsProj005()
    {
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

        var authored = JsonSerializer.Deserialize<AuthoredServiceBlueprint>(json)!;
        var result = _projector.Project(authored);

        result.Diagnostics.Should().ContainSingle(d => d.Code == "PROJ005");
    }

    [Fact]
    public void Project_ActionMissingRequiredParameter_ReportsProj120()
    {
        var result = _projector.Project(new AuthoredServiceBlueprint
        {
            DefinitionKey = "validation-test",
            DisplayName = "Validation Test",
            InitialTouchpointKey = "start",
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
            Touchpoints =
            [
                new AuthoredTouchpoint
                {
                    TouchpointKey = "start",
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

        result.Diagnostics.Should().ContainSingle(d => d.Code == "PROJ120");
    }

    [Fact]
    public void Project_StageRouteCanTargetJoinGatewayDirectly()
    {
        var result = _projector.Project(new AuthoredServiceBlueprint
        {
            DefinitionKey = "payment-validation-test",
            DisplayName = "Payment Validation Test",
            InitialTouchpointKey = "enter-details",
            Queues =
            [
                new AuthoredQueue { Key = "web-user", DisplayName = "Applicant" },
                new AuthoredQueue { Key = "business-user", DisplayName = "Payments" }
            ],
            Gateways =
            [
                new AuthoredGateway
                {
                    GatewayKey = "submit-payment",
                    DisplayName = "Submit payment",
                    Kind = GatewayKind.Split,
                    QueueKey = "web-user",
                    Routes =
                    [
                        new AuthoredRoute { Id = "submit-to-join", Target = "await-payment-confirmation", Trigger = "submit" },
                        new AuthoredRoute { Id = "submit-to-confirm", Target = "confirm-payment-received", Trigger = "submit" }
                    ]
                },
                new AuthoredGateway
                {
                    GatewayKey = "await-payment-confirmation",
                    DisplayName = "Await payment confirmation",
                    Kind = GatewayKind.Join,
                    QueueKey = "web-user",
                    WaitingInfo = new WaitingMetadata { Content = "Waiting for payment confirmation." },
                    RequiredIncomingQueues = ["web-user", "business-user"],
                    Routes = [new AuthoredRoute { Id = "release", Target = "payment-complete", Trigger = "release" }]
                }
            ],
            Touchpoints =
            [
                new AuthoredTouchpoint
                {
                    TouchpointKey = "enter-details",
                    DisplayName = "Enter details",
                    QueueKey = "web-user",
                    Routes = [new AuthoredRoute { Id = "enter-submit", Target = "submit-payment", Trigger = "submit" }]
                },
                new AuthoredTouchpoint
                {
                    TouchpointKey = "confirm-payment-received",
                    DisplayName = "Confirm payment received",
                    QueueKey = "business-user",
                    Routes = [new AuthoredRoute { Id = "confirm-join", Target = "await-payment-confirmation", Trigger = "confirm" }]
                },
                new AuthoredTouchpoint
                {
                    TouchpointKey = "payment-complete",
                    DisplayName = "Payment complete",
                    QueueKey = "web-user"
                }
            ]
        });

        result.HasErrors.Should().BeFalse();
        result.Diagnostics.Select(d => d.Code).Should().NotContain(new[] { "PROJ138", "PROJ150", "PROJ157" });
    }
}
