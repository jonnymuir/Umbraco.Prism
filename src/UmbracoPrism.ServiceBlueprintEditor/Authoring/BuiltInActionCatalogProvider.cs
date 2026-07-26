using System.Text.Json.Nodes;

namespace UmbracoPrism.ServiceBlueprintEditor.Authoring;

/// <summary>
/// Built-in blueprint action catalog for Blueprint Editor V1.
/// Keeps action discovery in the authoring layer while preserving stable runtime action type keys.
/// </summary>
public sealed class BuiltInActionCatalogProvider : IActionCatalogProvider, IActionCatalogSource
{
    private readonly IReadOnlyList<ActionCatalogEntry> _entries;
    private readonly IReadOnlyDictionary<string, ActionCatalogEntry> _entriesByType;

    public BuiltInActionCatalogProvider()
        : this(new DefaultParameterWidgetMapper())
    {
    }

    public BuiltInActionCatalogProvider(IParameterWidgetMapper widgetMapper)
    {
        _entries = CreateEntries(widgetMapper);
        _entriesByType = _entries.ToDictionary(entry => entry.Type, StringComparer.Ordinal);
    }

    public IReadOnlyList<ActionCatalogEntry> GetEntries() => _entries;

    public IReadOnlyList<ActionCatalogEntry> GetCatalog() => _entries;

    public ActionCatalogEntry? GetEntry(string actionType)
        => _entriesByType.TryGetValue(actionType, out var entry) ? entry : null;

    private static IReadOnlyList<ActionCatalogEntry> CreateEntries(IParameterWidgetMapper widgetMapper)
    {
        var formReferenceSchema = new AuthoredParameterSchema
        {
            Key = "forms.form-reference",
            Title = "Forms engine reference",
            Description = "Shared parameters for forms-engine load/save/submit actions.",
            AppliesTo = ["forms.load", "forms.save", "forms.submit"],
            AllowAdditionalProperties = false,
            Properties =
            [
                new AuthoredParameterDefinition
                {
                    Key = "formDefinitionId",
                    Title = "Form definition id",
                    Description = "Stable forms-engine key to load, save, or submit.",
                    ValueKind = ParameterValueKind.String,
                    Editor = ParameterWidgets.Text
                }
            ],
            Required = ["formDefinitionId"]
        };

        return
        [
            CreateEntry(
                widgetMapper,
                type: "forms.load",
                label: "Load form",
                summary: "Load a forms-engine definition when a touchpoint opens.",
                appliesTo: [ActionCatalogScopes.TouchpointOnEntry],
                paramsSchema: formReferenceSchema,
                defaultParams: new JsonObject
                {
                    ["formDefinitionId"] = string.Empty
                },
                status: ActionCatalogStatuses.Available,
                runtimeImplementation: "reference-business-app"),

            CreateEntry(
                widgetMapper,
                type: "forms.save",
                label: "Save form",
                summary: "Persist the current forms-engine payload before leaving a touchpoint.",
                appliesTo: [ActionCatalogScopes.TouchpointOnExit],
                paramsSchema: formReferenceSchema,
                defaultParams: new JsonObject
                {
                    ["formDefinitionId"] = string.Empty
                },
                status: ActionCatalogStatuses.Available,
                runtimeImplementation: "reference-business-app"),

            CreateEntry(
                widgetMapper,
                type: "forms.submit",
                label: "Submit form",
                summary: "Validate and submit a forms-engine definition while taking a transition.",
                appliesTo: [ActionCatalogScopes.Transition],
                paramsSchema: formReferenceSchema,
                defaultParams: new JsonObject
                {
                    ["formDefinitionId"] = string.Empty
                },
                status: ActionCatalogStatuses.Available,
                runtimeImplementation: "reference-business-app"),

            CreateEntry(
                widgetMapper,
                type: "case.assign",
                label: "Assign case",
                summary: "Assign the current case to a role, queue, or named user.",
                appliesTo: [ActionCatalogScopes.TouchpointOnEntry, ActionCatalogScopes.Transition],
                paramsSchema: new AuthoredParameterSchema
                {
                    Key = "case.assign",
                    Title = "Case assignment",
                    Description = "Parameters for assigning a case as the blueprint advances.",
                    AppliesTo = ["case.assign"],
                    AllowAdditionalProperties = false,
                    Properties =
                    [
                        new AuthoredParameterDefinition
                        {
                            Key = "assigneeType",
                            Title = "Assignment target type",
                            ValueKind = ParameterValueKind.String,
                            Editor = ParameterWidgets.Select,
                            AllowedValues = ["role", "queue", "user"],
                            DefaultValue = JsonValue.Create("role")
                        },
                        new AuthoredParameterDefinition
                        {
                            Key = "assigneeValue",
                            Title = "Assignment target",
                            Description = "Role name, queue name, or user identifier.",
                            ValueKind = ParameterValueKind.String,
                            Editor = ParameterWidgets.Text
                        },
                        new AuthoredParameterDefinition
                        {
                            Key = "overwriteExisting",
                            Title = "Overwrite existing assignment",
                            ValueKind = ParameterValueKind.Boolean,
                            Editor = ParameterWidgets.Toggle,
                            DefaultValue = JsonValue.Create(false)
                        }
                    ],
                    Required = ["assigneeType", "assigneeValue"]
                },
                defaultParams: new JsonObject
                {
                    ["assigneeType"] = "role",
                    ["assigneeValue"] = string.Empty,
                    ["overwriteExisting"] = false
                },
                status: ActionCatalogStatuses.Available,
                runtimeImplementation: "reference-business-app"),

            CreateEntry(
                widgetMapper,
                type: "case.enqueue",
                label: "Enqueue case",
                summary: "Place the case into a named queue with an optional priority.",
                appliesTo: [ActionCatalogScopes.TouchpointOnEntry, ActionCatalogScopes.Transition],
                paramsSchema: new AuthoredParameterSchema
                {
                    Key = "case.enqueue",
                    Title = "Queue placement",
                    Description = "Parameters for queueing work after a touchpoint or transition.",
                    AppliesTo = ["case.enqueue"],
                    AllowAdditionalProperties = false,
                    Properties =
                    [
                        new AuthoredParameterDefinition
                        {
                            Key = "queue",
                            Title = "Queue",
                            ValueKind = ParameterValueKind.String,
                            Editor = ParameterWidgets.Text
                        },
                        new AuthoredParameterDefinition
                        {
                            Key = "priority",
                            Title = "Priority",
                            ValueKind = ParameterValueKind.String,
                            Editor = ParameterWidgets.Select,
                            AllowedValues = ["low", "normal", "high"],
                            DefaultValue = JsonValue.Create("normal")
                        }
                    ],
                    Required = ["queue"]
                },
                defaultParams: new JsonObject
                {
                    ["queue"] = string.Empty,
                    ["priority"] = "normal"
                },
                status: ActionCatalogStatuses.Available,
                runtimeImplementation: "reference-business-app"),

            CreateEntry(
                widgetMapper,
                type: "case.set-status",
                label: "Set case status",
                summary: "Update the case status shown to staff and applicants.",
                appliesTo: [ActionCatalogScopes.TouchpointOnEntry, ActionCatalogScopes.Transition],
                paramsSchema: new AuthoredParameterSchema
                {
                    Key = "case.set-status",
                    Title = "Case status",
                    Description = "Parameters for publishing a new case status.",
                    AppliesTo = ["case.set-status"],
                    AllowAdditionalProperties = false,
                    Properties =
                    [
                        new AuthoredParameterDefinition
                        {
                            Key = "status",
                            Title = "Status",
                            ValueKind = ParameterValueKind.String,
                            Editor = ParameterWidgets.Text
                        },
                        new AuthoredParameterDefinition
                        {
                            Key = "reason",
                            Title = "Reason",
                            ValueKind = ParameterValueKind.String,
                            Editor = ParameterWidgets.Textarea
                        }
                    ],
                    Required = ["status"]
                },
                defaultParams: new JsonObject
                {
                    ["status"] = string.Empty,
                    ["reason"] = string.Empty
                },
                status: ActionCatalogStatuses.Available,
                runtimeImplementation: "reference-business-app"),

            CreateEntry(
                widgetMapper,
                type: "case.add-note",
                label: "Add case note",
                summary: "Attach an internal or public note to the current case.",
                appliesTo: [ActionCatalogScopes.TouchpointOnExit, ActionCatalogScopes.Transition],
                paramsSchema: new AuthoredParameterSchema
                {
                    Key = "case.add-note",
                    Title = "Case note",
                    Description = "Parameters for recording a blueprint note.",
                    AppliesTo = ["case.add-note"],
                    AllowAdditionalProperties = false,
                    Properties =
                    [
                        new AuthoredParameterDefinition
                        {
                            Key = "note",
                            Title = "Note",
                            ValueKind = ParameterValueKind.String,
                            Editor = ParameterWidgets.Textarea
                        },
                        new AuthoredParameterDefinition
                        {
                            Key = "visibility",
                            Title = "Visibility",
                            ValueKind = ParameterValueKind.String,
                            Editor = ParameterWidgets.Select,
                            AllowedValues = ["internal", "public"],
                            DefaultValue = JsonValue.Create("internal")
                        }
                    ],
                    Required = ["note"]
                },
                defaultParams: new JsonObject
                {
                    ["note"] = string.Empty,
                    ["visibility"] = "internal"
                },
                status: ActionCatalogStatuses.Available,
                runtimeImplementation: "reference-business-app"),

            CreateEntry(
                widgetMapper,
                type: "notifications.send-email",
                label: "Send email",
                summary: "Queue an email notification using a named template.",
                appliesTo: [ActionCatalogScopes.TouchpointOnEntry, ActionCatalogScopes.Transition],
                paramsSchema: new AuthoredParameterSchema
                {
                    Key = "notifications.send-email",
                    Title = "Email notification",
                    Description = "Parameters for sending a blueprint email.",
                    AppliesTo = ["notifications.send-email"],
                    AllowAdditionalProperties = false,
                    Properties =
                    [
                        new AuthoredParameterDefinition
                        {
                            Key = "templateId",
                            Title = "Template id",
                            ValueKind = ParameterValueKind.String,
                            Editor = ParameterWidgets.Text
                        },
                        new AuthoredParameterDefinition
                        {
                            Key = "recipientEmail",
                            Title = "Recipient email",
                            Description = "Resolved email address or token to expand at runtime.",
                            ValueKind = ParameterValueKind.String,
                            Format = "email",
                            Editor = ParameterWidgets.Text
                        },
                        new AuthoredParameterDefinition
                        {
                            Key = "subject",
                            Title = "Subject override",
                            ValueKind = ParameterValueKind.String,
                            Editor = ParameterWidgets.Text
                        }
                    ],
                    Required = ["templateId", "recipientEmail"]
                },
                defaultParams: new JsonObject
                {
                    ["templateId"] = string.Empty,
                    ["recipientEmail"] = string.Empty,
                    ["subject"] = string.Empty
                },
                status: ActionCatalogStatuses.Available,
                runtimeImplementation: "reference-business-app"),

            CreateEntry(
                widgetMapper,
                type: "notifications.send-sms",
                label: "Send SMS",
                summary: "Queue an SMS notification using a named template.",
                appliesTo: [ActionCatalogScopes.TouchpointOnEntry, ActionCatalogScopes.Transition],
                paramsSchema: new AuthoredParameterSchema
                {
                    Key = "notifications.send-sms",
                    Title = "SMS notification",
                    Description = "Parameters for sending a blueprint SMS.",
                    AppliesTo = ["notifications.send-sms"],
                    AllowAdditionalProperties = false,
                    Properties =
                    [
                        new AuthoredParameterDefinition
                        {
                            Key = "templateId",
                            Title = "Template id",
                            ValueKind = ParameterValueKind.String,
                            Editor = ParameterWidgets.Text
                        },
                        new AuthoredParameterDefinition
                        {
                            Key = "recipientNumber",
                            Title = "Recipient number",
                            ValueKind = ParameterValueKind.String,
                            Editor = ParameterWidgets.Text
                        }
                    ],
                    Required = ["templateId", "recipientNumber"]
                },
                defaultParams: new JsonObject
                {
                    ["templateId"] = string.Empty,
                    ["recipientNumber"] = string.Empty
                },
                status: ActionCatalogStatuses.Available,
                runtimeImplementation: "reference-business-app")
        ];
    }

    private static ActionCatalogEntry CreateEntry(
        IParameterWidgetMapper widgetMapper,
        string type,
        string label,
        string summary,
        IReadOnlyList<string> appliesTo,
        AuthoredParameterSchema paramsSchema,
        JsonObject defaultParams,
        string status,
        string runtimeImplementation)
        => new()
        {
            Type = type,
            Label = label,
            Summary = summary,
            AppliesTo = appliesTo,
            ParamsSchema = paramsSchema,
            ParameterWidgets = widgetMapper.BuildWidgetMap(paramsSchema),
            DefaultParams = defaultParams,
            Status = status,
            RuntimeImplementation = runtimeImplementation
        };
}
