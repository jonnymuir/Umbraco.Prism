using FluentAssertions;
using UmbracoPrism.Shared.Models.ServiceDesign;

namespace UmbracoPrism.Core.Tests.ServiceDesign.Authoring;

/// <summary>
/// Validates the terminal-reachability rule enforced by <see cref="ServiceBlueprint.ValidateReachability"/>.
/// Rule: every state and gateway must have *some* path to a terminal state (one with no outgoing
/// routes) — a dead-end loop that never reaches one is flagged even though every individual route
/// target resolves and every gateway has outgoing routes.
/// </summary>
public class ReachabilityValidationTests
{
    [Fact]
    public void ValidateReachability_LinearStateGatewayState_ReturnsNoErrors()
    {
        var workflow = new ServiceBlueprint
        {
            DefinitionKey = "test",
            DisplayName = "Test",
            InitialTouchpoint = "start",
            Touchpoints = [
                new StepDefinition
                {
                    TouchpointKey = "start",
                    DisplayName = "Start",
                    Routes = [new ServiceBlueprintRouteDefinition { Id = "start-to-gw", Target = "gw", Trigger = "continue" }]
                },
                new StepDefinition { TouchpointKey = "end", DisplayName = "End", Routes = [] }
            ],
            Gateways =
            [
                new ServiceBlueprintGatewayDefinition
                {
                    Key = "gw",
                    DisplayName = "Gateway",
                    GatewayType = "Split",
                    QueueKey = "web-user",
                    Routes = [new ServiceBlueprintRouteDefinition { Id = "gw-to-end", Target = "end", Trigger = "continue" }]
                }
            ]
        };

        workflow.ValidateReachability().Should().BeEmpty("every node has a path to the terminal 'end' state");
    }

    [Fact]
    public void ValidateReachability_SelfLoopWithAnEscapeRoute_ReturnsNoErrors()
    {
        // Mirrors money-modeller.json's "model" stage: a "recalculate" route loops back to itself
        // via a gateway, but a second route out of the same stage still leads to a terminal state.
        // Only *some* path needs to reach a terminal — a self-loop alongside a real exit is fine.
        var workflow = new ServiceBlueprint
        {
            DefinitionKey = "test",
            DisplayName = "Test",
            InitialTouchpoint = "model",
            Touchpoints = [
                new StepDefinition
                {
                    TouchpointKey = "model",
                    DisplayName = "Model",
                    Routes =
                    [
                        new ServiceBlueprintRouteDefinition { Id = "recalculate", Target = "recalculate-loop", Trigger = "recalculate" },
                        new ServiceBlueprintRouteDefinition { Id = "finish", Target = "to-end", Trigger = "finish" }
                    ]
                },
                new StepDefinition { TouchpointKey = "end", DisplayName = "End", Routes = [] }
            ],
            Gateways =
            [
                new ServiceBlueprintGatewayDefinition
                {
                    Key = "recalculate-loop",
                    DisplayName = "Recalculate",
                    GatewayType = "Split",
                    QueueKey = "web-user",
                    Routes = [new ServiceBlueprintRouteDefinition { Id = "back-to-model", Target = "model", Trigger = "continue" }]
                },
                new ServiceBlueprintGatewayDefinition
                {
                    Key = "to-end",
                    DisplayName = "To End",
                    GatewayType = "Split",
                    QueueKey = "web-user",
                    Routes = [new ServiceBlueprintRouteDefinition { Id = "gw-to-end", Target = "end", Trigger = "continue" }]
                }
            ]
        };

        workflow.ValidateReachability().Should().BeEmpty(
            "'model' still reaches 'end' via 'to-end', even though 'recalculate-loop' only loops back to 'model'");
    }

    [Fact]
    public void ValidateReachability_RequestMoreInfoLoopWithNoWayOut_ReturnsErrorsForLoopNodes()
    {
        // Reproduces the real defect found reviewing a recorded agent build: a business-side
        // "request more info" gateway that only ever routed within the business queue, with no
        // path back to a state the informant could actually answer from. Every individual route
        // target resolved and every gateway had outgoing routes, so ValidateGatewayRouting passed
        // — but any instance that took this branch could never complete.
        var workflow = new ServiceBlueprint
        {
            DefinitionKey = "test",
            DisplayName = "Test",
            InitialTouchpoint = "assess",
            Touchpoints = [
                new StepDefinition
                {
                    TouchpointKey = "assess",
                    DisplayName = "Assess evidence and record the decision",
                    QueueKey = "business-user",
                    Routes =
                    [
                        new ServiceBlueprintRouteDefinition { Id = "request-more-info", Target = "still-gathering", Trigger = "request-more-info" }
                    ]
                }
            ],
            Gateways =
            [
                new ServiceBlueprintGatewayDefinition
                {
                    Key = "still-gathering",
                    DisplayName = "Still gathering evidence",
                    GatewayType = "Split",
                    QueueKey = "business-user",
                    Routes = [new ServiceBlueprintRouteDefinition { Id = "back-to-assess", Target = "assess", Trigger = "continue" }]
                }
            ]
        };

        var errors = workflow.ValidateReachability();

        errors.Should().Contain(d => d.Code == "STATE_UNREACHABLE_TERMINAL" && d.Path == "states.assess");
        errors.Should().Contain(d => d.Code == "GATEWAY_UNREACHABLE_TERMINAL" && d.Path == "gateways[0]");
    }

    [Fact]
    public void ValidateReachability_DanglingRouteAlongsideAValidExit_IsNotFlagged()
    {
        // A route target that resolves to nothing is already reported by ValidateGatewayRouting
        // (ROUTE_TARGET_NOT_FOUND) — this rule silently skips the unresolved edge rather than
        // treating it as a real path, so a state with one dangling route and one real route to a
        // terminal is judged solely on the route that actually works.
        var workflow = new ServiceBlueprint
        {
            DefinitionKey = "test",
            DisplayName = "Test",
            InitialTouchpoint = "start",
            Touchpoints = [
                new StepDefinition
                {
                    TouchpointKey = "start",
                    DisplayName = "Start",
                    Routes =
                    [
                        new ServiceBlueprintRouteDefinition { Id = "start-to-nowhere", Target = "does-not-exist", Trigger = "abandon" },
                        new ServiceBlueprintRouteDefinition { Id = "start-to-gw", Target = "gw", Trigger = "continue" }
                    ]
                },
                new StepDefinition { TouchpointKey = "end", DisplayName = "End", Routes = [] }
            ],
            Gateways =
            [
                new ServiceBlueprintGatewayDefinition
                {
                    Key = "gw",
                    DisplayName = "Gateway",
                    GatewayType = "Split",
                    QueueKey = "web-user",
                    Routes = [new ServiceBlueprintRouteDefinition { Id = "gw-to-end", Target = "end", Trigger = "continue" }]
                }
            ]
        };

        workflow.ValidateReachability().Should().BeEmpty(
            "the dangling route is ValidateGatewayRouting's concern, and the other route still reaches 'end'");
    }
}
