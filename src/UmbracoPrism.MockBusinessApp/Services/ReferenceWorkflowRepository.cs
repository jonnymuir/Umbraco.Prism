using System.Text.Json.Nodes;
using UmbracoPrism.Shared.Models.Workflow.Components;
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
                Components =
                [
                    new BodyComponent
                    {
                        Content = "Tell us who is applying and where the development will take place. We use these details to create your case file."
                    },
                    new FieldsetComponent
                    {
                        Legend = "Applicant and site",
                        LegendSize = "m",
                        Children =
                        [
                            new TextInputComponent
                            {
                                FieldKey = "applicant-name",
                                Label = "Applicant name",
                                Required = true,
                                Hint = "Enter the full name of the person or organisation applying."
                            },
                            new TextareaComponent
                            {
                                FieldKey = "site-address",
                                Label = "Site address",
                                Required = true,
                                Hint = "Enter the full address of the site where development is proposed."
                            }
                        ]
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
                Components =
                [
                    new FieldsetComponent
                    {
                        Legend = "About your proposal",
                        LegendSize = "m",
                        Children =
                        [
                            new TextareaComponent
                            {
                                FieldKey = "description",
                                Label = "Description of proposed works",
                                Required = true,
                                Hint = "Provide a clear description of the development you are proposing."
                            },
                            new SelectComponent
                            {
                                FieldKey = "development-type",
                                Label = "Type of development",
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
                    new InsetTextComponent
                    {
                        Content = "You can save and return to this form at any point — your answers are kept on your case until you submit."
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
                Components =
                [
                    new BodyComponent
                    {
                        Content = "Tell us a bit about you so we can route your enquiry to the right team."
                    },
                    new FieldsetComponent
                    {
                        Legend = "About you and your enquiry",
                        LegendSize = "m",
                        Children =
                        [
                            new TextInputComponent { FieldKey = "firstName", Label = "First name", Required = true, Hint = "As it appears on your ID" },
                            new TextInputComponent { FieldKey = "lastName", Label = "Last name", Required = true },
                            new DateInputComponent { FieldKey = "dateOfBirth", Label = "Date of birth", Required = true, Hint = "For example, 12 03 1985" },
                            new EmailComponent { FieldKey = "email", Label = "Email address", Required = true, Hint = "We'll only use this to contact you about your request" },
                            new SelectComponent
                            {
                                FieldKey = "requestType",
                                Label = "What type of request is this?",
                                Required = true,
                                Options =
                                [
                                    "General enquiry",
                                    "Data subject access request",
                                    "Complaint",
                                    "Technical support"
                                ]
                            },
                            new TextareaComponent
                            {
                                FieldKey = "description",
                                Label = "Tell us more about your request",
                                Required = true,
                                Hint = "Include as much detail as possible. Maximum 1000 characters."
                            },
                            new RadiosComponent
                            {
                                FieldKey = "urgency",
                                Label = "How urgent is this?",
                                Required = true,
                                Options =
                                [
                                    "Standard (5-7 working days)",
                                    "Urgent (2 working days)",
                                    "Critical (same day)"
                                ]
                            }
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
            new AuthoredLane { Key = "payments", DisplayName = "Payments team", Actor = "reviewer" }
        ],
        Gateways =
        [
            new AuthoredGateway
            {
                GatewayKey = "submit-payment",
                DisplayName = "Submit payment → notify back-office",
                Kind = GatewayKind.Split,
                LaneKey = "applicant",
                Source = "enter-details",
                Routes =
                [
                    Route("enter-details--submit--await-payment-confirmation", "await-payment-confirmation", "submit"),
                    Route("enter-details--submit--confirm-payment-received", "confirm-payment-received", "submit")
                ]
            },
            new AuthoredGateway
            {
                GatewayKey = "payment-confirmed",
                DisplayName = "Payment confirmed",
                Kind = GatewayKind.Split,
                LaneKey = "payments",
                Source = "confirm-payment-received",
                Routes =
                [
                    new AuthoredRoute
                    {
                        Id = "confirm-payment-received--confirm--await-payment-confirmation",
                        Target = "await-payment-confirmation",
                        Trigger = "confirm",
                        RequiresRole = "reviewer"
                    }
                ]
            },
            new AuthoredGateway
            {
                GatewayKey = "await-payment-confirmation",
                DisplayName = "Awaiting payment confirmation",
                Kind = GatewayKind.Join,
                LaneKey = "applicant",
                WaitingInfo = new WaitingMetadata
                {
                    Content = "We're waiting for the payments team to confirm receipt of your payment.",
                    ExpectedWaitSeconds = 60,
                    PollIntervalMs = 5000,
                    AllowDefer = true,
                    DeferMessage = "You can leave this page and return later. We'll update this payment as soon as the confirmation arrives."
                },
                RequiredIncomingLanes = ["applicant", "payments"],
                Routes = [Route("await-payment-confirmation--release--payment-complete", "payment-complete", "release")]
            }
        ],
        Stages =
        [
            new AuthoredStage
            {
                StageKey = "enter-details",
                DisplayName = "Enter payment details",
                Kind = StageKind.Question,
                LaneKey = "applicant",
                Components =
                [
                    new BodyComponent
                    {
                        Content = "Provide the payment details for this application. We'll take you to a waiting screen while the payments team confirms receipt."
                    },
                    new FieldsetComponent
                    {
                        Legend = "Payment details",
                        LegendSize = "m",
                        Children =
                        [
                            new TextInputComponent
                            {
                                FieldKey = "cardholderName",
                                Label = "Cardholder name",
                                Required = true,
                                Hint = "Enter the name exactly as it appears on the card."
                            },
                            new TextInputComponent
                            {
                                FieldKey = "paymentReference",
                                Label = "Payment reference",
                                Required = true,
                                Hint = "Use the reference shown on your application or invoice."
                            },
                            new EmailComponent
                            {
                                FieldKey = "receiptEmail",
                                Label = "Email address for the receipt",
                                Required = true,
                                Hint = "We'll send confirmation to this address once the payment is confirmed."
                            },
                            new DecimalInputComponent
                            {
                                FieldKey = "amount",
                                Label = "Amount (£)",
                                Required = true,
                                Hint = "Enter the amount you are paying today."
                            }
                        ]
                    },
                    new InsetTextComponent
                    {
                        Content = "After you submit, your payment will wait for a quick back-office confirmation before this journey completes."
                    },
                    new WarningTextComponent
                    {
                        Content = "Do not close the page until you see the confirmation screen, unless you choose to leave and return later."
                    }
                ]
            },
            new AuthoredStage
            {
                StageKey = "confirm-payment-received",
                DisplayName = "Confirm payment received",
                Kind = StageKind.Question,
                LaneKey = "payments",
                Description = "Back-office confirmation step for reconciling the payment before the applicant is released.",
                Components =
                [
                    new BodyComponent
                    {
                        Content = "Record the confirmation details from the payment provider so the applicant can be released from the waiting step."
                    },
                    new FieldsetComponent
                    {
                        Legend = "Confirmation details",
                        LegendSize = "m",
                        Children =
                        [
                            new TextInputComponent
                            {
                                FieldKey = "confirmationReference",
                                Label = "Confirmation reference",
                                Required = true,
                                Hint = "Enter the provider or ledger reference used to match this payment."
                            },
                            new DecimalInputComponent
                            {
                                FieldKey = "amountReceived",
                                Label = "Amount received (£)",
                                Required = true,
                                Hint = "Use the settled amount shown in the provider response."
                            },
                            new TextareaComponent
                            {
                                FieldKey = "notes",
                                Label = "Notes",
                                Hint = "Add any brief context the service team may need later."
                            }
                        ]
                    }
                ]
            },
            new AuthoredStage
            {
                StageKey = "payment-complete",
                DisplayName = "Payment complete",
                Kind = StageKind.Confirmation,
                LaneKey = "applicant",
                Description = "Confirms that the payment has been matched and the receipt is on its way.",
                Components =
                [
                    new PanelComponent
                    {
                        Heading = "Payment confirmed"
                    },
                    new BodyComponent
                    {
                        Content = "We've matched your payment and sent a receipt to the email address you provided."
                    },
                    new InsetTextComponent
                    {
                        Content = "Keep your payment reference handy if you contact us about this application."
                    }
                ]
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
