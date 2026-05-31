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
        Version = 1,
        Description = "Standard planning application workflow for submitting and tracking planning permission requests.",
        SchemaVersion = "1.0",
        InitialStageKey = "declaration",
        InstancePolicy = "single",
        Lanes =
        [
            ApplicantLane()
        ],
        Gateways =
        [
            new AuthoredGateway
            {
                GatewayKey = "route-application-form",
                DisplayName = "Route to application form",
                Kind = GatewayKind.Split,
                LaneKey = "applicant",
                Source = "declaration",
                Routes = [Route("declaration--continue--application-form", "application-form", "continue")]
            },
            new AuthoredGateway
            {
                GatewayKey = "route-check-answers",
                DisplayName = "Route to check answers",
                Kind = GatewayKind.Split,
                LaneKey = "applicant",
                Source = "application-form",
                Routes = [Route("application-form--continue--check-answers", "check-answers", "continue")]
            },
            new AuthoredGateway
            {
                GatewayKey = "route-submitted",
                DisplayName = "Route to submitted",
                Kind = GatewayKind.Split,
                LaneKey = "applicant",
                Source = "check-answers",
                Routes =
                [
                    new AuthoredRoute
                    {
                        Id = "check-answers--submit--submitted",
                        Target = "submitted",
                        Trigger = "submit",
                        Condition = new AuthoredCondition
                        {
                            Expression = "application.isComplete == true",
                            Description = "Prevent submission until the applicant has completed the form."
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
                                    ["formDefinitionId"] = "planning-application"
                                },
                                Summary = "Submit the application form to the business app."
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
                StageKey = "declaration",
                DisplayName = "Declaration",
                Description = "Collects applicant and site identity before the full planning form.",
                Kind = StageKind.Question,
                Actor = "applicant",
                LaneKey = "applicant",
                Actions =
                [
                    new AuthoredAction
                    {
                        Type = "forms.load",
                        Timing = ActionTiming.OnEntry,
                        ParameterSchemaKey = "forms-form-definition",
                        Parameters = new JsonObject
                        {
                            ["formDefinitionId"] = "planning-declaration"
                        },
                        Summary = "Load the declaration form."
                    }
                ],
                Fields =
                [
                    new AuthoredField
                    {
                        Key = "applicant-name",
                        Label = "Applicant name",
                        Type = FieldType.Text,
                        Required = true,
                        Hint = "Enter the full name of the person or organisation applying."
                    },
                    new AuthoredField
                    {
                        Key = "site-address",
                        Label = "Site address",
                        Type = FieldType.Textarea,
                        Required = true,
                        Hint = "Enter the full address of the site where development is proposed."
                    }
                ],
                EditorComment = "Entry point — collects basic applicant and site identity."
            },
            new AuthoredStage
            {
                StageKey = "application-form",
                DisplayName = "Application Form",
                Description = "Captures the substantive planning request.",
                Kind = StageKind.Question,
                Actor = "applicant",
                LaneKey = "applicant",
                Actions =
                [
                    new AuthoredAction
                    {
                        Type = "forms.save",
                        Timing = ActionTiming.OnExit,
                        ParameterSchemaKey = "forms-form-definition",
                        Parameters = new JsonObject
                        {
                            ["formDefinitionId"] = "planning-application"
                        },
                        Summary = "Persist the application form before moving on."
                    }
                ],
                Fields =
                [
                    new AuthoredField
                    {
                        Key = "description",
                        Label = "Description of proposed works",
                        Type = FieldType.Textarea,
                        Required = true,
                        Hint = "Provide a clear description of the development you are proposing."
                    },
                    new AuthoredField
                    {
                        Key = "development-type",
                        Label = "Type of development",
                        Type = FieldType.Select,
                        Required = true,
                        Options =
                        [
                            "New build",
                            "Extension",
                            "Change of use",
                            "Demolition",
                            "Other"
                        ]
                    }
                ]
            },
            new AuthoredStage
            {
                StageKey = "check-answers",
                DisplayName = "Check your answers",
                Description = "Summarises captured answers before final submission.",
                Kind = StageKind.CheckAnswers,
                Actor = "applicant",
                LaneKey = "applicant",
                EditorComment = "Summary of all answers before final submission."
            },
            new AuthoredStage
            {
                StageKey = "submitted",
                DisplayName = "Application submitted",
                Description = "Confirms receipt and moves the case into reviewer handling.",
                Kind = StageKind.Confirmation,
                Actor = "applicant",
                LaneKey = "applicant"
            }
        ],
        Handoffs =
        [
            new AuthoredHandoff
            {
                Id = "applicant-to-caseworker",
                FromStage = "check-answers",
                ToStage = "submitted",
                Label = "applicant-to-caseworker",
                ActorChange = "caseworker"
            }
        ],
        ParameterSchemas =
        [
            new AuthoredParameterSchema
            {
                Key = "forms-form-definition",
                Title = "Forms engine definition reference",
                Description = "Shared parameter contract for load/save/submit form actions.",
                AppliesTo = ["forms.load", "forms.save", "forms.submit"],
                ValueKind = ParameterValueKind.Object,
                AllowAdditionalProperties = false,
                Properties =
                [
                    new AuthoredParameterDefinition
                    {
                        Key = "formDefinitionId",
                        Title = "Form definition id",
                        Description = "Stable forms-engine key to load or persist.",
                        ValueKind = ParameterValueKind.String,
                        Editor = "text"
                    }
                ],
                Required = ["formDefinitionId"]
            }
        ],
        Metadata = new Dictionary<string, string>
        {
            ["serviceArea"] = "Planning",
            ["owner"] = "planning-team"
        }
    };

    private static AuthoredWorkflow CommunityEnquiryWorkflow() => new()
    {
        Id = Guid.Parse("b2c3d4e5-f6a7-8901-bcde-f12345678901"),
        DefinitionKey = "community-enquiry",
        DisplayName = "Get in Touch",
        Version = 1,
        InitialStageKey = "collecting-details",
        InstancePolicy = "single",
        Lanes = [ApplicantLane()],
        Gateways =
        [
            new AuthoredGateway
            {
                GatewayKey = "route-submitted",
                DisplayName = "Route to submitted",
                Kind = GatewayKind.Split,
                LaneKey = "applicant",
                Source = "collecting-details",
                Routes = [Route("collecting-details--submit--submitted", "submitted", "submit")]
            }
        ],
        Stages =
        [
            new AuthoredStage { StageKey = "collecting-details", DisplayName = "Your details", Kind = StageKind.Question, LaneKey = "applicant" },
            new AuthoredStage { StageKey = "submitted", DisplayName = "Thank you", Kind = StageKind.Confirmation, LaneKey = "applicant" }
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
        Lanes =
        [
            ApplicantLane(),
            new AuthoredLane { Key = "caseworker", DisplayName = "Caseworker", Actor = "caseworker" }
        ],
        Gateways =
        [
            new AuthoredGateway
            {
                GatewayKey = "request-submitted",
                DisplayName = "Request submitted",
                Kind = GatewayKind.Split,
                LaneKey = "applicant",
                Source = "collecting-info",
                Routes =
                [
                    Route("collecting-info--submit--review-complete", "review-complete", "submit"),
                    Route("collecting-info--submit--caseworker-review", "caseworker-review", "submit")
                ]
            },
            new AuthoredGateway
            {
                GatewayKey = "caseworker-route",
                DisplayName = "Route from caseworker review",
                Kind = GatewayKind.Split,
                LaneKey = "caseworker",
                Source = "caseworker-review",
                Routes = [Route("caseworker-review--complete-review--review-complete", "review-complete", "complete-review")]
            },
            new AuthoredGateway
            {
                GatewayKey = "review-complete",
                DisplayName = "Review complete",
                Kind = GatewayKind.Join,
                LaneKey = "applicant",
                WaitingInfo = new WaitingMetadata
                {
                    Content = "We've received your submission and it's currently being reviewed. You'll hear from us soon — no further action is needed right now.",
                    ExpectedWaitSeconds = 30,
                    PollIntervalMs = 5000,
                    AllowDefer = false
                },
                RequiredIncomingLanes = ["applicant", "caseworker"],
                Routes = [Route("review-complete--release--complete", "complete", "release")]
            }
        ],
        Stages =
        [
            new AuthoredStage
            {
                StageKey = "collecting-info",
                DisplayName = "Tell us about yourself",
                Kind = StageKind.Question,
                LaneKey = "applicant",
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
                StageKey = "caseworker-review",
                DisplayName = "Caseworker review",
                Kind = StageKind.Question,
                LaneKey = "caseworker",
                Description = "Caseworker confirms the review outcome before the applicant sees the final status."
            },
            new AuthoredStage
            {
                StageKey = "complete",
                DisplayName = "Request Complete",
                Kind = StageKind.Confirmation,
                LaneKey = "applicant"
            }
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
        Lanes =
        [
            ApplicantLane(),
            new AuthoredLane { Key = "payments", DisplayName = "Payments", Actor = "reviewer" }
        ],
        Gateways =
        [
            new AuthoredGateway
            {
                GatewayKey = "payment-submitted",
                DisplayName = "Payment submitted",
                Kind = GatewayKind.Split,
                LaneKey = "applicant",
                Source = "enter-details",
                Routes =
                [
                    Route("enter-details--submit--payment-settled", "payment-settled", "submit"),
                    Route("enter-details--submit--provider-processing", "provider-processing", "submit")
                ]
            },
            new AuthoredGateway
            {
                GatewayKey = "provider-route",
                DisplayName = "Route from provider processing",
                Kind = GatewayKind.Split,
                LaneKey = "payments",
                Source = "provider-processing",
                Routes =
                [
                    new AuthoredRoute
                    {
                        Id = "provider-processing--complete--payment-settled",
                        Target = "payment-settled",
                        Trigger = "complete",
                        RequiresRole = "reviewer"
                    }
                ]
            },
            new AuthoredGateway
            {
                GatewayKey = "payment-settled",
                DisplayName = "Payment settled",
                Kind = GatewayKind.Join,
                LaneKey = "applicant",
                WaitingInfo = new WaitingMetadata
                {
                    Content = "Your payment is being processed right now.",
                    ExpectedWaitSeconds = 30,
                    PollIntervalMs = 5000,
                    AllowDefer = true,
                    DeferMessage = "You can leave this page and return to your applications later. Your progress has been saved."
                },
                RequiredIncomingLanes = ["applicant", "payments"],
                Routes = [Route("payment-settled--release--payment-complete", "payment-complete", "release")]
            }
        ],
        Stages =
        [
            new AuthoredStage
            {
                StageKey = "enter-details",
                DisplayName = "Enter Payment Details",
                Kind = StageKind.Question,
                LaneKey = "applicant",
                Fields =
                [
                    new AuthoredField { Key = "cardholderName", Label = "Cardholder name", Type = FieldType.Text, Required = true },
                    new AuthoredField { Key = "amount", Label = "Amount (£)", Type = FieldType.Decimal, Required = true }
                ]
            },
            new AuthoredStage
            {
                StageKey = "provider-processing",
                DisplayName = "Provider processing",
                Kind = StageKind.Question,
                LaneKey = "payments",
                Description = "Payment provider processing and reconciliation work."
            },
            new AuthoredStage
            {
                StageKey = "payment-complete",
                DisplayName = "Payment Complete",
                Kind = StageKind.Confirmation,
                LaneKey = "applicant",
                Description = "Payment received. A receipt has been sent to your email address."
            }
        ]
    };

    private static AuthoredLane ApplicantLane() => new()
    {
        Key = "applicant",
        DisplayName = "Applicant",
        Actor = "applicant"
    };

    private static AuthoredRoute Route(string id, string target, string trigger) => new()
    {
        Id = id,
        Target = target,
        Trigger = trigger
    };
}
