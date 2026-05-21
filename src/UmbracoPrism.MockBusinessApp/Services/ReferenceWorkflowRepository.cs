using System.Text.Json.Nodes;
using UmbracoPrism.WorkflowEditor.Authoring;

namespace UmbracoPrism.MockBusinessApp.Services;

/// <summary>
/// Reference implementation that seeds exactly four demo workflows for the Prism showcase.
/// All four are available to the editor, the front-end journey, and the runtime engine.
/// Downstream apps replace this with their own authored workflow repository (filesystem, database, etc.).
/// </summary>
public static class ReferenceWorkflowRepository
{
    /// <summary>
    /// Returns the four reference workflows seeded for the demo application.
    /// These are the authoritative authored sources; projection creates the runtime definitions.
    /// </summary>
    public static IReadOnlyList<KeyValuePair<string, AuthoredWorkflow>> GetReferenceWorkflows()
    {
        return
        [
            new KeyValuePair<string, AuthoredWorkflow>("planning", PlanningWorkflow()),
            new KeyValuePair<string, AuthoredWorkflow>("community-enquiry", CommunityEnquiryWorkflow()),
            new KeyValuePair<string, AuthoredWorkflow>("information-request", InformationRequestWorkflow()),
            new KeyValuePair<string, AuthoredWorkflow>("payment-demo", PaymentDemoWorkflow())
        ];
    }

    private static AuthoredWorkflow PlanningWorkflow() => new()
    {
        Id = Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890"),
        DefinitionKey = "planning-application",
        DisplayName = "Planning Application",
        Version = 2,
        InitialStageKey = "declaration",
        InstancePolicy = "single",
        Stages =
        [
            new AuthoredStage { StageKey = "declaration", DisplayName = "Declaration", Kind = StageKind.Question },
            new AuthoredStage { StageKey = "submitted", DisplayName = "Submitted", Kind = StageKind.Confirmation }
        ],
        Transitions =
        [
            new AuthoredTransition { FromStage = "declaration", ToStage = "submitted", Action = "submit" }
        ]
    };

    private static AuthoredWorkflow CommunityEnquiryWorkflow() => new()
    {
        Id = Guid.Parse("b2c3d4e5-f6a7-8901-bcde-f12345678901"),
        DefinitionKey = "community-enquiry",
        DisplayName = "Get in Touch",
        Version = 1,
        InitialStageKey = "collecting-details",
        InstancePolicy = "single",
        Stages =
        [
            new AuthoredStage { StageKey = "collecting-details", DisplayName = "Your details", Kind = StageKind.Question },
            new AuthoredStage { StageKey = "submitted", DisplayName = "Thank you", Kind = StageKind.Confirmation }
        ],
        Transitions =
        [
            new AuthoredTransition { FromStage = "collecting-details", ToStage = "submitted", Action = "submit" }
        ]
    };

    private static AuthoredWorkflow InformationRequestWorkflow() => new()
    {
        Id = Guid.Parse("c3d4e5f6-a7b8-9012-cdef-123456789012"),
        DefinitionKey = "information-request",
        DisplayName = "Information Request",
        Version = 1,
        InitialStageKey = "collecting-info",
        Stages =
        [
            new AuthoredStage { StageKey = "collecting-info", DisplayName = "Tell us about yourself", Kind = StageKind.Question },
            new AuthoredStage { StageKey = "submitted", DisplayName = "Request submitted", Kind = StageKind.Confirmation }
        ],
        Transitions =
        [
            new AuthoredTransition { FromStage = "collecting-info", ToStage = "submitted", Action = "submit" }
        ]
    };

    private static AuthoredWorkflow PaymentDemoWorkflow() => new()
    {
        Id = Guid.Parse("d4e5f6a7-b8c9-0123-def0-123456789abc"),
        DefinitionKey = "payment-demo",
        DisplayName = "Payment Demo",
        Version = 1,
        InitialStageKey = "enter-details",
        InstancePolicy = "single",
        Stages =
        [
            new AuthoredStage { StageKey = "enter-details", DisplayName = "Enter Payment Details", Kind = StageKind.Question },
            new AuthoredStage { StageKey = "payment-confirmed", DisplayName = "Payment confirmed", Kind = StageKind.Confirmation }
        ],
        Transitions =
        [
            new AuthoredTransition { FromStage = "enter-details", ToStage = "payment-confirmed", Action = "pay" }
        ]
    };
}
