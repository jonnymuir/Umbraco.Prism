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
        InstancePolicy = "single",
        InitialStageKey = "collecting-info",
        Stages =
        [
            new AuthoredStage
            {
                StageKey = "collecting-info",
                DisplayName = "Tell us about yourself",
                Kind = StageKind.Question,
                Fields =
                [
                    new AuthoredField { Key = "firstName", Label = "First name", Type = FieldType.Text, Required = true, Hint = "As it appears on your ID" },
                    new AuthoredField { Key = "lastName", Label = "Last name", Type = FieldType.Text, Required = true },
                    new AuthoredField { Key = "dateOfBirth", Label = "Date of birth", Type = FieldType.Date, Required = true, Hint = "For example, 12 03 1985" },
                    new AuthoredField { Key = "email", Label = "Email address", Type = FieldType.Email, Required = true, Hint = "We'll only use this to contact you about your request" },
                    new AuthoredField
                    {
                        Key = "requestType",
                        Label = "What type of request is this?",
                        Type = FieldType.Select,
                        Required = true,
                        Options =
                        [
                            "General enquiry",
                            "Data subject access request",
                            "Complaint",
                            "Technical support"
                        ]
                    },
                    new AuthoredField
                    {
                        Key = "description",
                        Label = "Tell us more about your request",
                        Type = FieldType.Textarea,
                        Required = true,
                        Hint = "Include as much detail as possible. Maximum 1000 characters."
                    },
                    new AuthoredField
                    {
                        Key = "urgency",
                        Label = "How urgent is this?",
                        Type = FieldType.Radios,
                        Required = true,
                        Options =
                        [
                            "Standard (5-7 working days)",
                            "Urgent (2 working days)",
                            "Critical (same day)"
                        ]
                    }
                ]
            },
            new AuthoredStage
            {
                StageKey = "under-review",
                DisplayName = "Your request is being reviewed",
                Kind = StageKind.Waiting,
                Waiting = new WaitingMetadata
                {
                    Content = "We've received your submission and it's currently being reviewed. You'll hear from us soon — no further action is needed right now.",
                    ExpectedWaitSeconds = 30,
                    PollIntervalMs = 5000,
                    AllowDefer = false
                }
            }
        ],
        Transitions =
        [
            new AuthoredTransition { FromStage = "collecting-info", ToStage = "under-review", Action = "submit" }
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
            new AuthoredStage
            {
                StageKey = "enter-details",
                DisplayName = "Enter Payment Details",
                Kind = StageKind.Question,
                Fields =
                [
                    new AuthoredField { Key = "cardholderName", Label = "Cardholder name", Type = FieldType.Text, Required = true },
                    new AuthoredField { Key = "amount", Label = "Amount (£)", Type = FieldType.Decimal, Required = true }
                ]
            },
            new AuthoredStage
            {
                StageKey = "processing-payment",
                DisplayName = "Processing Your Payment",
                Kind = StageKind.Waiting,
                Waiting = new WaitingMetadata
                {
                    Content = "Your payment is being processed right now.",
                    ExpectedWaitSeconds = 30,
                    PollIntervalMs = 5000,
                    AllowDefer = true,
                    DeferMessage = "You can leave this page and return to your applications later. Your progress has been saved."
                }
            },
            new AuthoredStage
            {
                StageKey = "payment-complete",
                DisplayName = "Payment Complete",
                Kind = StageKind.Confirmation,
                Description = "Payment received. A receipt has been sent to your email address."
            }
        ],
        Transitions =
        [
            new AuthoredTransition { FromStage = "enter-details", ToStage = "processing-payment", Action = "submit" },
            new AuthoredTransition { FromStage = "processing-payment", ToStage = "payment-complete", Action = "complete", RequiresRole = "reviewer" }
        ]
    };
}
