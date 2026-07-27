using FluentAssertions;
using UmbracoPrism.Core.Models.ServiceDesign;
using UmbracoPrism.Shared.Models.ServiceDesign;

namespace UmbracoPrism.Core.Tests.Models;

public class ServiceRequestRenderShellResolverTests
{
    [Fact]
    public void GivenWaitingComponent_WhenResolvingShell_ThenReturnsWaiting()
    {
        var shell = ServiceRequestRenderShellResolver.ResolveShell(
            new[]
            {
                new PrismComponentRenderPayload { Type = "waiting", Content = "Please wait." }
            },
            legacyStepType: string.Empty,
            hasWaitingConfig: false,
            hasAvailableActions: false);

        shell.Should().Be("waiting");
    }

    [Fact]
    public void GivenSummaryListsAndContent_WhenResolvingShell_ThenReturnsCheckAnswers()
    {
        var shell = ServiceRequestRenderShellResolver.ResolveShell(
            new PrismComponentRenderPayload[]
            {
                new()
                {
                    Type = "body",
                    Content = "Check your answers before sending."
                },
                new()
                {
                    Type = "summary-list",
                    Fields =
                    [
                        new FieldRenderPayload
                        {
                            FieldKey = "email",
                            Label = "Email",
                            FieldType = "text",
                            Required = true
                        }
                    ]
                }
            },
            legacyStepType: string.Empty,
            hasWaitingConfig: false,
            hasAvailableActions: true);

        shell.Should().Be("check-answers");
    }

    [Fact]
    public void GivenPanelWithoutInputs_WhenResolvingShell_ThenReturnsConfirmation()
    {
        var shell = ServiceRequestRenderShellResolver.ResolveShell(
            new[]
            {
                new PrismComponentRenderPayload { Type = "panel", Heading = "Done" },
                new PrismComponentRenderPayload { Type = "body", Content = "Thanks." }
            },
            legacyStepType: string.Empty,
            hasWaitingConfig: false,
            hasAvailableActions: false);

        shell.Should().Be("confirmation");
    }

    [Fact]
    public void GivenReadOnlyContentWithoutActions_WhenResolvingShell_ThenReturnsStatusTimeline()
    {
        var shell = ServiceRequestRenderShellResolver.ResolveShell(
            new[]
            {
                new PrismComponentRenderPayload { Type = "heading", Content = "Under review" },
                new PrismComponentRenderPayload { Type = "body", Content = "No action needed right now." }
            },
            legacyStepType: string.Empty,
            hasWaitingConfig: false,
            hasAvailableActions: false);

        shell.Should().Be("status-timeline");
    }

    [Fact]
    public void GivenEditableFieldset_WhenResolvingShell_ThenReturnsQuestion()
    {
        var shell = ServiceRequestRenderShellResolver.ResolveShell(
            new[]
            {
                new PrismComponentRenderPayload
                {
                    Type = "fieldset",
                    Fields =
                    [
                        new FieldRenderPayload
                        {
                            FieldKey = "full-name",
                            Label = "Full name",
                            FieldType = "text",
                            Required = true
                        }
                    ]
                }
            },
            legacyStepType: string.Empty,
            hasWaitingConfig: false,
            hasAvailableActions: true);

        shell.Should().Be("question");
    }

    [Fact]
    public void GivenLegacyAliasWithoutComponents_WhenResolvingShell_ThenFallsBackToCanonicalShell()
    {
        var shell = ServiceRequestRenderShellResolver.ResolveShell(
            Array.Empty<PrismComponentRenderPayload>(),
            legacyStepType: "review",
            hasWaitingConfig: false,
            hasAvailableActions: false);

        shell.Should().Be("check-answers");
    }
}
