using FluentAssertions;
using UmbracoPrism.Shared.Models.Workflow;

namespace UmbracoPrism.Core.Tests.Workflow.Authoring;

/// <summary>
/// Validates the terminal-reachability rule enforced by <see cref="WorkflowDefinitionFile.ValidateReachability"/>.
/// Rule: every state and gateway must have *some* path to a terminal state (one with no outgoing
/// routes) — a dead-end loop that never reaches one is flagged even though every individual route
/// target resolves and every gateway has outgoing routes.
/// </summary>
public class WorkflowReachabilityValidationTests
{
    [Fact]
    public void ValidateReachability_LinearStateGatewayState_ReturnsNoErrors()
    {
        var workflow = new WorkflowDefinitionFile
        {
            DefinitionKey = "test",
            DisplayName = "Test",
            InitialState = "start",
            States =
            [
                new StepDefinition
                {
                    StateKey = "start",
                    DisplayName = "Start",
                    Routes = [new WorkflowRouteDefinition { Id = "start-to-gw", Target = "gw", Trigger = "continue" }]
                },
                new StepDefinition { StateKey = "end", DisplayName = "End", Routes = [] }
            ],
            Gateways =
            [
                new WorkflowGatewayDefinition
                {
                    Key = "gw",
                    DisplayName = "Gateway",
                    GatewayType = "Split",
                    QueueKey = "web-user",
                    Routes = [new WorkflowRouteDefinition { Id = "gw-to-end", Target = "end", Trigger = "continue" }]
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
        var workflow = new WorkflowDefinitionFile
        {
            DefinitionKey = "test",
            DisplayName = "Test",
            InitialState = "model",
            States =
            [
                new StepDefinition
                {
                    StateKey = "model",
                    DisplayName = "Model",
                    Routes =
                    [
                        new WorkflowRouteDefinition { Id = "recalculate", Target = "recalculate-loop", Trigger = "recalculate" },
                        new WorkflowRouteDefinition { Id = "finish", Target = "to-end", Trigger = "finish" }
                    ]
                },
                new StepDefinition { StateKey = "end", DisplayName = "End", Routes = [] }
            ],
            Gateways =
            [
                new WorkflowGatewayDefinition
                {
                    Key = "recalculate-loop",
                    DisplayName = "Recalculate",
                    GatewayType = "Split",
                    QueueKey = "web-user",
                    Routes = [new WorkflowRouteDefinition { Id = "back-to-model", Target = "model", Trigger = "continue" }]
                },
                new WorkflowGatewayDefinition
                {
                    Key = "to-end",
                    DisplayName = "To End",
                    GatewayType = "Split",
                    QueueKey = "web-user",
                    Routes = [new WorkflowRouteDefinition { Id = "gw-to-end", Target = "end", Trigger = "continue" }]
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
        var workflow = new WorkflowDefinitionFile
        {
            DefinitionKey = "test",
            DisplayName = "Test",
            InitialState = "assess",
            States =
            [
                new StepDefinition
                {
                    StateKey = "assess",
                    DisplayName = "Assess evidence and record the decision",
                    QueueKey = "business-user",
                    Routes =
                    [
                        new WorkflowRouteDefinition { Id = "request-more-info", Target = "still-gathering", Trigger = "request-more-info" }
                    ]
                }
            ],
            Gateways =
            [
                new WorkflowGatewayDefinition
                {
                    Key = "still-gathering",
                    DisplayName = "Still gathering evidence",
                    GatewayType = "Split",
                    QueueKey = "business-user",
                    Routes = [new WorkflowRouteDefinition { Id = "back-to-assess", Target = "assess", Trigger = "continue" }]
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
        var workflow = new WorkflowDefinitionFile
        {
            DefinitionKey = "test",
            DisplayName = "Test",
            InitialState = "start",
            States =
            [
                new StepDefinition
                {
                    StateKey = "start",
                    DisplayName = "Start",
                    Routes =
                    [
                        new WorkflowRouteDefinition { Id = "start-to-nowhere", Target = "does-not-exist", Trigger = "abandon" },
                        new WorkflowRouteDefinition { Id = "start-to-gw", Target = "gw", Trigger = "continue" }
                    ]
                },
                new StepDefinition { StateKey = "end", DisplayName = "End", Routes = [] }
            ],
            Gateways =
            [
                new WorkflowGatewayDefinition
                {
                    Key = "gw",
                    DisplayName = "Gateway",
                    GatewayType = "Split",
                    QueueKey = "web-user",
                    Routes = [new WorkflowRouteDefinition { Id = "gw-to-end", Target = "end", Trigger = "continue" }]
                }
            ]
        };

        workflow.ValidateReachability().Should().BeEmpty(
            "the dangling route is ValidateGatewayRouting's concern, and the other route still reaches 'end'");
    }
}
