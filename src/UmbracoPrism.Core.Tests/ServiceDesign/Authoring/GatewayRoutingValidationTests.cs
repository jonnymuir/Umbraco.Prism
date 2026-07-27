using FluentAssertions;
using UmbracoPrism.Shared.Models.ServiceDesign;

namespace UmbracoPrism.Core.Tests.ServiceDesign.Authoring;

/// <summary>
/// Validates the gateway routing rules enforced by <see cref="ServiceBlueprint.ValidateGatewayRouting"/>.
/// Rule: Routes FROM states must always target a gateway.
/// Rule: Routes FROM gateways may target states or other gateways.
/// </summary>
public class GatewayRoutingValidationTests
{
    [Fact]
    public void ValidateGatewayRouting_StateToGatewayToState_ReturnsNoErrors()
    {
        var workflow = new ServiceBlueprint
        {
            DefinitionKey = "test",
            DisplayName = "Test",
            InitialStage = "start",
            Stages = [
                new StageDefinition
                {
                    StageKey = "start",
                    DisplayName = "Start",
                    Routes =
                    [
                        new ServiceBlueprintRouteDefinition { Id = "start-to-gw", Target = "my-gateway", Trigger = "continue" }
                    ]
                },
                new StageDefinition { StageKey = "end", DisplayName = "End", Routes = [] }
            ],
            Gateways =
            [
                new ServiceBlueprintGatewayDefinition
                {
                    Key = "my-gateway",
                    DisplayName = "My Gateway",
                    GatewayType = "Split",
                    QueueKey = "web-user",
                    Routes =
                    [
                        new ServiceBlueprintRouteDefinition { Id = "gw-to-end", Target = "end", Trigger = "continue" }
                    ]
                }
            ]
        };

        var errors = workflow.ValidateGatewayRouting();

        errors.Should().BeEmpty("state → gateway → state is a valid routing pattern");
    }

    [Fact]
    public void ValidateGatewayRouting_GatewayWithNoOutgoingRoutes_ReturnsError()
    {
        // Reproduces a real agent-authored bug found live: a gateway saved with an empty routes
        // array validated cleanly (nothing previously checked for *zero* routes, only that each
        // existing route's own target resolves), then hard-failed the very first real submission
        // that reached it with a runtime GATEWAY_NO_OUTGOING error.
        var workflow = new ServiceBlueprint
        {
            DefinitionKey = "test",
            DisplayName = "Test",
            InitialStage = "start",
            Stages = [
                new StageDefinition
                {
                    StageKey = "start",
                    DisplayName = "Start",
                    Routes =
                    [
                        new ServiceBlueprintRouteDefinition { Id = "start-to-gw", Target = "dead-end-gateway", Trigger = "continue" }
                    ]
                }
            ],
            Gateways =
            [
                new ServiceBlueprintGatewayDefinition
                {
                    Key = "dead-end-gateway",
                    DisplayName = "Dead End",
                    GatewayType = "Join",
                    QueueKey = "web-user",
                    Routes = []
                }
            ]
        };

        var errors = workflow.ValidateGatewayRouting();

        errors.Should().ContainSingle(d =>
            d.Code == "GATEWAY_NO_OUTGOING_ROUTES" && d.Path == "gateways[0].routes");
    }

    [Fact]
    public void ValidateGatewayRouting_StateToState_ReturnsError()
    {
        var workflow = new ServiceBlueprint
        {
            DefinitionKey = "test",
            DisplayName = "Test",
            InitialStage = "start",
            Stages = [
                new StageDefinition
                {
                    StageKey = "start",
                    DisplayName = "Start",
                    Routes =
                    [
                        new ServiceBlueprintRouteDefinition { Id = "start-to-end", Target = "end", Trigger = "continue" }
                    ]
                },
                new StageDefinition { StageKey = "end", DisplayName = "End", Routes = [] }
            ],
            Gateways = []
        };

        var errors = workflow.ValidateGatewayRouting();

        errors.Should().ContainSingle(d => d.Message.Contains("Stage 'start'") && d.Message.Contains("'end'"),
            "a direct state → state route must be flagged");
    }

    [Fact]
    public void ValidateGatewayRouting_GatewayToState_ReturnsNoErrors()
    {
        var workflow = new ServiceBlueprint
        {
            DefinitionKey = "test",
            DisplayName = "Test",
            InitialStage = "start",
            Stages = [
                new StageDefinition
                {
                    StageKey = "start",
                    DisplayName = "Start",
                    Routes =
                    [
                        new ServiceBlueprintRouteDefinition { Id = "to-gw", Target = "gw", Trigger = "continue" }
                    ]
                },
                new StageDefinition { StageKey = "end", DisplayName = "End", Routes = [] }
            ],
            Gateways =
            [
                new ServiceBlueprintGatewayDefinition
                {
                    Key = "gw",
                    DisplayName = "GW",
                    GatewayType = "Split",
                    QueueKey = "web-user",
                    Routes =
                    [
                        new ServiceBlueprintRouteDefinition { Id = "gw-to-end", Target = "end", Trigger = "continue" }
                    ]
                }
            ]
        };

        var errors = workflow.ValidateGatewayRouting();

        errors.Should().BeEmpty("gateway → state is an explicitly allowed routing pattern");
    }

    [Fact]
    public void ValidateGatewayRouting_GatewayToGateway_ReturnsNoErrors()
    {
        // Matches payment-demo: Split gateway fans out to a Join gateway.
        var workflow = new ServiceBlueprint
        {
            DefinitionKey = "test",
            DisplayName = "Test",
            InitialStage = "start",
            Stages = [
                new StageDefinition
                {
                    StageKey = "start",
                    DisplayName = "Start",
                    Routes =
                    [
                        new ServiceBlueprintRouteDefinition { Id = "to-split", Target = "split", Trigger = "submit" }
                    ]
                },
                new StageDefinition { StageKey = "end", DisplayName = "End", Routes = [] }
            ],
            Gateways =
            [
                new ServiceBlueprintGatewayDefinition
                {
                    Key = "split",
                    DisplayName = "Split",
                    GatewayType = "Split",
                    QueueKey = "web-user",
                    Routes =
                    [
                        new ServiceBlueprintRouteDefinition { Id = "split-to-join", Target = "join", Trigger = "submit" },
                        new ServiceBlueprintRouteDefinition { Id = "split-to-end", Target = "end", Trigger = "submit" }
                    ]
                },
                new ServiceBlueprintGatewayDefinition
                {
                    Key = "join",
                    DisplayName = "Join",
                    GatewayType = "Join",
                    QueueKey = "web-user",
                    Routes =
                    [
                        new ServiceBlueprintRouteDefinition { Id = "join-to-end", Target = "end", Trigger = "release" }
                    ]
                }
            ]
        };

        var errors = workflow.ValidateGatewayRouting();

        errors.Should().BeEmpty("gateway → gateway (e.g. Split → Join) is an explicitly allowed routing pattern");
    }

    [Fact]
    public void ValidateGatewayRouting_MultipleStateToStateViolations_ReturnsOneErrorPerViolation()
    {
        var workflow = new ServiceBlueprint
        {
            DefinitionKey = "test",
            DisplayName = "Test",
            InitialStage = "a",
            Stages = [
                new StageDefinition
                {
                    StageKey = "a",
                    DisplayName = "A",
                    Routes =
                    [
                        new ServiceBlueprintRouteDefinition { Id = "a-to-b", Target = "b", Trigger = "continue" }
                    ]
                },
                new StageDefinition
                {
                    StageKey = "b",
                    DisplayName = "B",
                    Routes =
                    [
                        new ServiceBlueprintRouteDefinition { Id = "b-to-c", Target = "c", Trigger = "continue" }
                    ]
                },
                new StageDefinition { StageKey = "c", DisplayName = "C", Routes = [] }
            ],
            Gateways = []
        };

        var errors = workflow.ValidateGatewayRouting();

        errors.Should().HaveCount(2, "each direct state → state route is a separate violation");
        errors.Should().Contain(d => d.Message.Contains("'a'") && d.Message.Contains("'b'"));
        errors.Should().Contain(d => d.Message.Contains("'b'") && d.Message.Contains("'c'"));
    }

    [Fact]
    public void ValidateGatewayRouting_WorkflowWithNoRoutes_ReturnsNoErrors()
    {
        var workflow = new ServiceBlueprint
        {
            DefinitionKey = "test",
            DisplayName = "Test",
            InitialStage = "only",
            Stages = [
                new StageDefinition { StageKey = "only", DisplayName = "Only State", Routes = [] }
            ],
            Gateways = []
        };

        var errors = workflow.ValidateGatewayRouting();

        errors.Should().BeEmpty("a workflow with no outgoing routes has no routing violations");
    }

    [Fact]
    public void ValidateGatewayRouting_GatewayWithNoKey_ReturnsError()
    {
        // Reproduces a real agent-authored bug: a gateway saved with an empty key can never be
        // resolved by any route, and would only surface at runtime as an opaque "access denied".
        var workflow = new ServiceBlueprint
        {
            DefinitionKey = "test",
            DisplayName = "Test",
            InitialStage = "start",
            Stages = [
                new StageDefinition
                {
                    StageKey = "start",
                    DisplayName = "Start",
                    Routes = [new ServiceBlueprintRouteDefinition { Id = "to-gw", Target = "gw", Trigger = "continue" }]
                },
                new StageDefinition { StageKey = "end", DisplayName = "End", Routes = [] }
            ],
            Gateways =
            [
                new ServiceBlueprintGatewayDefinition
                {
                    Key = "",
                    DisplayName = "Unnamed gateway",
                    GatewayType = "Split",
                    Routes = [new ServiceBlueprintRouteDefinition { Id = "gw-to-end", Target = "end", Trigger = "continue" }]
                }
            ]
        };

        var errors = workflow.ValidateGatewayRouting();

        errors.Should().Contain(d => d.Code == "GATEWAY_MISSING_KEY");
    }

    [Fact]
    public void ValidateGatewayRouting_StateRouteTargetsNonExistentGateway_ReturnsError()
    {
        var workflow = new ServiceBlueprint
        {
            DefinitionKey = "test",
            DisplayName = "Test",
            InitialStage = "start",
            Stages = [
                new StageDefinition
                {
                    StageKey = "start",
                    DisplayName = "Start",
                    Routes = [new ServiceBlueprintRouteDefinition { Id = "to-gw", Target = "no-such-gateway", Trigger = "continue" }]
                }
            ],
            Gateways = []
        };

        var errors = workflow.ValidateGatewayRouting();

        errors.Should().ContainSingle(d =>
            d.Code == "ROUTE_TARGET_NOT_FOUND" && d.Message.Contains("'no-such-gateway'"));
    }

    [Fact]
    public void ValidateGatewayRouting_GatewayRouteTargetsNonExistentState_ReturnsError()
    {
        var workflow = new ServiceBlueprint
        {
            DefinitionKey = "test",
            DisplayName = "Test",
            InitialStage = "start",
            Stages = [
                new StageDefinition
                {
                    StageKey = "start",
                    DisplayName = "Start",
                    Routes = [new ServiceBlueprintRouteDefinition { Id = "to-gw", Target = "gw", Trigger = "continue" }]
                }
            ],
            Gateways =
            [
                new ServiceBlueprintGatewayDefinition
                {
                    Key = "gw",
                    DisplayName = "GW",
                    GatewayType = "Split",
                    Routes = [new ServiceBlueprintRouteDefinition { Id = "gw-to-nowhere", Target = "ghost", Trigger = "continue" }]
                }
            ]
        };

        var errors = workflow.ValidateGatewayRouting();

        errors.Should().ContainSingle(d =>
            d.Code == "ROUTE_TARGET_NOT_FOUND" && d.Message.Contains("'ghost'"));
    }

    [Fact]
    public void ValidateGatewayRouting_StateRouteWithEmptyTarget_ReturnsWarningNotError()
    {
        // The visual editor's "add a route" affordance deliberately allows saving with a route
        // not yet pointed anywhere (mid-edit) — this must stay a Warning, not an Error, or that
        // legitimate flow would break. But it should still be visible to an author finishing up.
        var workflow = new ServiceBlueprint
        {
            DefinitionKey = "test",
            DisplayName = "Test",
            InitialStage = "start",
            Stages = [
                new StageDefinition
                {
                    StageKey = "start",
                    DisplayName = "Start",
                    Routes = [new ServiceBlueprintRouteDefinition { Id = "unwired", Target = "", Trigger = "" }]
                }
            ],
            Gateways = []
        };

        var errors = workflow.ValidateGatewayRouting();

        errors.Should().ContainSingle(d =>
            d.Code == "ROUTE_TARGET_EMPTY" && d.Severity == ServiceBlueprintDiagnosticSeverity.Warning);
    }
}
