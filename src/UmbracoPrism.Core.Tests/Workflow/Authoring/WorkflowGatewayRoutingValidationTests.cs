using FluentAssertions;
using UmbracoPrism.Shared.Models.Workflow;

namespace UmbracoPrism.Core.Tests.Workflow.Authoring;

/// <summary>
/// Validates the gateway routing rules enforced by <see cref="WorkflowDefinitionFile.ValidateGatewayRouting"/>.
/// Rule: Routes FROM states must always target a gateway.
/// Rule: Routes FROM gateways may target states or other gateways.
/// </summary>
public class WorkflowGatewayRoutingValidationTests
{
    [Fact]
    public void ValidateGatewayRouting_StateToGatewayToState_ReturnsNoErrors()
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
                    Routes =
                    [
                        new WorkflowRouteDefinition { Id = "start-to-gw", Target = "my-gateway", Trigger = "continue" }
                    ]
                },
                new StepDefinition { StateKey = "end", DisplayName = "End", Routes = [] }
            ],
            Gateways =
            [
                new WorkflowGatewayDefinition
                {
                    Key = "my-gateway",
                    DisplayName = "My Gateway",
                    GatewayType = "Split",
                    Routes =
                    [
                        new WorkflowRouteDefinition { Id = "gw-to-end", Target = "end", Trigger = "continue" }
                    ]
                }
            ]
        };

        var errors = workflow.ValidateGatewayRouting();

        errors.Should().BeEmpty("state → gateway → state is a valid routing pattern");
    }

    [Fact]
    public void ValidateGatewayRouting_StateToState_ReturnsError()
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
                    Routes =
                    [
                        new WorkflowRouteDefinition { Id = "start-to-end", Target = "end", Trigger = "continue" }
                    ]
                },
                new StepDefinition { StateKey = "end", DisplayName = "End", Routes = [] }
            ],
            Gateways = []
        };

        var errors = workflow.ValidateGatewayRouting();

        errors.Should().ContainSingle(e => e.Contains("State 'start'") && e.Contains("'end'"),
            "a direct state → state route must be flagged");
    }

    [Fact]
    public void ValidateGatewayRouting_GatewayToState_ReturnsNoErrors()
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
                    Routes =
                    [
                        new WorkflowRouteDefinition { Id = "to-gw", Target = "gw", Trigger = "continue" }
                    ]
                },
                new StepDefinition { StateKey = "end", DisplayName = "End", Routes = [] }
            ],
            Gateways =
            [
                new WorkflowGatewayDefinition
                {
                    Key = "gw",
                    DisplayName = "GW",
                    GatewayType = "Split",
                    Routes =
                    [
                        new WorkflowRouteDefinition { Id = "gw-to-end", Target = "end", Trigger = "continue" }
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
                        new WorkflowRouteDefinition { Id = "to-split", Target = "split", Trigger = "submit" }
                    ]
                },
                new StepDefinition { StateKey = "end", DisplayName = "End", Routes = [] }
            ],
            Gateways =
            [
                new WorkflowGatewayDefinition
                {
                    Key = "split",
                    DisplayName = "Split",
                    GatewayType = "Split",
                    Routes =
                    [
                        new WorkflowRouteDefinition { Id = "split-to-join", Target = "join", Trigger = "submit" },
                        new WorkflowRouteDefinition { Id = "split-to-end", Target = "end", Trigger = "submit" }
                    ]
                },
                new WorkflowGatewayDefinition
                {
                    Key = "join",
                    DisplayName = "Join",
                    GatewayType = "Join",
                    Routes =
                    [
                        new WorkflowRouteDefinition { Id = "join-to-end", Target = "end", Trigger = "release" }
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
        var workflow = new WorkflowDefinitionFile
        {
            DefinitionKey = "test",
            DisplayName = "Test",
            InitialState = "a",
            States =
            [
                new StepDefinition
                {
                    StateKey = "a",
                    DisplayName = "A",
                    Routes =
                    [
                        new WorkflowRouteDefinition { Id = "a-to-b", Target = "b", Trigger = "continue" }
                    ]
                },
                new StepDefinition
                {
                    StateKey = "b",
                    DisplayName = "B",
                    Routes =
                    [
                        new WorkflowRouteDefinition { Id = "b-to-c", Target = "c", Trigger = "continue" }
                    ]
                },
                new StepDefinition { StateKey = "c", DisplayName = "C", Routes = [] }
            ],
            Gateways = []
        };

        var errors = workflow.ValidateGatewayRouting();

        errors.Should().HaveCount(2, "each direct state → state route is a separate violation");
        errors.Should().Contain(e => e.Contains("'a'") && e.Contains("'b'"));
        errors.Should().Contain(e => e.Contains("'b'") && e.Contains("'c'"));
    }

    [Fact]
    public void ValidateGatewayRouting_WorkflowWithNoRoutes_ReturnsNoErrors()
    {
        var workflow = new WorkflowDefinitionFile
        {
            DefinitionKey = "test",
            DisplayName = "Test",
            InitialState = "only",
            States =
            [
                new StepDefinition { StateKey = "only", DisplayName = "Only State", Routes = [] }
            ],
            Gateways = []
        };

        var errors = workflow.ValidateGatewayRouting();

        errors.Should().BeEmpty("a workflow with no outgoing routes has no routing violations");
    }
}
