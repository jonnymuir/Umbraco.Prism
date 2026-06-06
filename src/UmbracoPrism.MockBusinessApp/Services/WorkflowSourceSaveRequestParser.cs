using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using UmbracoPrism.Shared.Models.Workflow;

namespace UmbracoPrism.MockBusinessApp.Services;

internal static class WorkflowSourceSaveRequestParser
{
    private const string InvalidWorkflowPayloadTitle = "Invalid workflow payload";
    private const string InvalidWorkflowPayloadDetail = "Every workflow component must include a supported 'type' value before the workflow can be saved.";
    private const int MaxIssues = 20;

    private static readonly HashSet<string> SupportedComponentTypes =
    [
        "fieldset",
        "accordion",
        "panel",
        "text",
        "number",
        "decimal",
        "select",
        "radio",
        "checkboxlist",
        "date",
        "email",
        "textarea",
        "boolean",
        "body",
        "heading",
        "inset-text",
        "warning-text",
        "details",
        "notification-banner",
        "waiting",
        "summary-list",
        "task-list"
    ];

    public static async Task<WorkflowSourceSaveParseResult> ParseAsync(
        HttpContext context,
        JsonSerializerOptions serializerOptions,
        CancellationToken ct = default)
    {
        using var reader = new StreamReader(context.Request.Body);
        var payload = await reader.ReadToEndAsync(ct);

        if (string.IsNullOrWhiteSpace(payload))
        {
            return WorkflowSourceSaveParseResult.Fail(
                400,
                InvalidWorkflowPayloadTitle,
                "Request body was empty.",
                "workflow-payload-empty",
                [new WorkflowSourceSaveError("request-body-empty", "Provide a workflow JSON document in the request body.", "$")]);
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(payload);
        }
        catch (JsonException ex)
        {
            return WorkflowSourceSaveParseResult.Fail(
                400,
                InvalidWorkflowPayloadTitle,
                "The workflow JSON could not be read.",
                "workflow-json-invalid",
                [new WorkflowSourceSaveError(
                    "json-invalid",
                    "Fix the malformed JSON document and try saving again.",
                    string.IsNullOrWhiteSpace(ex.Path) ? "$" : ex.Path)]);
        }

        using (document)
        {
            var componentIssues = ValidateComponents(document.RootElement);
            if (componentIssues.Count > 0)
            {
                return WorkflowSourceSaveParseResult.Fail(
                    400,
                    InvalidWorkflowPayloadTitle,
                    InvalidWorkflowPayloadDetail,
                    "workflow-component-invalid",
                    componentIssues);
            }
        }

        try
        {
            var workflow = JsonSerializer.Deserialize<WorkflowDefinitionFile>(payload, serializerOptions);
            if (workflow is null)
            {
                return WorkflowSourceSaveParseResult.Fail(
                    400,
                    InvalidWorkflowPayloadTitle,
                    "Request body was empty.",
                    "workflow-payload-empty",
                    [new WorkflowSourceSaveError("request-body-empty", "Provide a workflow JSON document in the request body.", "$")]);
            }

            var routingErrors = workflow.ValidateGatewayRouting();
            if (routingErrors.Count > 0)
            {
                return WorkflowSourceSaveParseResult.Fail(
                    400,
                    InvalidWorkflowPayloadTitle,
                    "State routes must always target a gateway, never another state directly.",
                    "workflow-gateway-routing-invalid",
                    routingErrors.Take(MaxIssues)
                        .Select(msg => new WorkflowSourceSaveError("state-route-targets-state", msg, "$.states[*].routes[*].target"))
                        .ToArray());
            }

            return WorkflowSourceSaveParseResult.Success(workflow);
        }
        catch (JsonException ex)
        {
            return WorkflowSourceSaveParseResult.Fail(
                400,
                InvalidWorkflowPayloadTitle,
                "The workflow JSON did not match the save contract.",
                "workflow-json-invalid",
                [new WorkflowSourceSaveError(
                    "json-invalid",
                    "Fix the invalid workflow JSON value and try saving again.",
                    string.IsNullOrWhiteSpace(ex.Path) ? "$" : ex.Path)]);
        }
        catch (NotSupportedException)
        {
            return WorkflowSourceSaveParseResult.Fail(
                400,
                InvalidWorkflowPayloadTitle,
                InvalidWorkflowPayloadDetail,
                "workflow-component-invalid",
                [new WorkflowSourceSaveError(
                    "component-type-invalid",
                    "Add a supported 'type' value to every workflow component before saving.",
                    "$.states[*].components[*]")]);
        }
    }

    public static IResult ToProblemResult(HttpContext context, WorkflowSourceSaveProblem problem)
    {
        var details = new ProblemDetails
        {
            Title = problem.Title,
            Detail = problem.Detail,
            Status = problem.StatusCode,
            Type = $"urn:umbraco-prism:problem:{problem.ErrorCode}",
            Instance = context.Request.Path.Value
        };

        details.Extensions["errorCode"] = problem.ErrorCode;
        details.Extensions["traceId"] = context.TraceIdentifier;
        details.Extensions["errors"] = problem.Errors.Select(error => new Dictionary<string, object?>
        {
            ["code"] = error.Code,
            ["message"] = error.Message,
            ["path"] = error.Path
        }).ToArray();

        return Results.Json(details, statusCode: problem.StatusCode, contentType: "application/problem+json");
    }

    private static IReadOnlyList<WorkflowSourceSaveError> ValidateComponents(JsonElement root)
    {
        var errors = new List<WorkflowSourceSaveError>();
        ValidateStateComponents(root, "states", errors);
        ValidateStateComponents(root, "stages", errors);
        return errors;
    }

    private static void ValidateStateComponents(JsonElement root, string propertyName, List<WorkflowSourceSaveError> errors)
    {
        if (errors.Count >= MaxIssues
            || !root.TryGetProperty(propertyName, out var states)
            || states.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var index = 0;
        foreach (var state in states.EnumerateArray())
        {
            if (errors.Count >= MaxIssues)
            {
                return;
            }

            if (state.ValueKind == JsonValueKind.Object && state.TryGetProperty("components", out var components))
            {
                ValidateComponentArray(components, $"$.{propertyName}[{index}].components", errors);
            }

            index++;
        }
    }

    private static void ValidateComponentArray(JsonElement components, string path, List<WorkflowSourceSaveError> errors)
    {
        if (errors.Count >= MaxIssues || components.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var index = 0;
        foreach (var component in components.EnumerateArray())
        {
            if (errors.Count >= MaxIssues)
            {
                return;
            }

            ValidateComponent(component, $"{path}[{index}]", errors);
            index++;
        }
    }

    private static void ValidateComponent(JsonElement component, string path, List<WorkflowSourceSaveError> errors)
    {
        if (errors.Count >= MaxIssues)
        {
            return;
        }

        if (component.ValueKind != JsonValueKind.Object)
        {
            errors.Add(new WorkflowSourceSaveError(
                "component-invalid",
                "Workflow components must be JSON objects.",
                path));
            return;
        }

        if (!component.TryGetProperty("type", out var typeElement) || typeElement.ValueKind != JsonValueKind.String)
        {
            errors.Add(new WorkflowSourceSaveError(
                "component-type-missing",
                "Workflow components must declare a supported 'type' value.",
                path));
            return;
        }

        var type = typeElement.GetString();
        if (string.IsNullOrWhiteSpace(type) || !SupportedComponentTypes.Contains(type))
        {
            errors.Add(new WorkflowSourceSaveError(
                "component-type-unsupported",
                $"Workflow component type '{type ?? string.Empty}' is not supported by the save API.",
                path));
            return;
        }

        if (component.TryGetProperty("children", out var children))
        {
            ValidateComponentArray(children, $"{path}.children", errors);
        }

        if (component.TryGetProperty("sections", out var sections) && sections.ValueKind == JsonValueKind.Array)
        {
            var sectionIndex = 0;
            foreach (var section in sections.EnumerateArray())
            {
                if (errors.Count >= MaxIssues)
                {
                    return;
                }

                if (section.ValueKind == JsonValueKind.Object && section.TryGetProperty("children", out var sectionChildren))
                {
                    ValidateComponentArray(sectionChildren, $"{path}.sections[{sectionIndex}].children", errors);
                }

                sectionIndex++;
            }
        }

        if (component.TryGetProperty("conditionalChildren", out var conditionalChildren)
            && conditionalChildren.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in conditionalChildren.EnumerateObject())
            {
                if (errors.Count >= MaxIssues)
                {
                    return;
                }

                ValidateComponentArray(property.Value, $"{path}.conditionalChildren.{property.Name}", errors);
            }
        }
    }
}

internal sealed record WorkflowSourceSaveParseResult(
    WorkflowDefinitionFile? Workflow,
    WorkflowSourceSaveProblem? Problem)
{
    public static WorkflowSourceSaveParseResult Success(WorkflowDefinitionFile workflow) => new(workflow, null);

    public static WorkflowSourceSaveParseResult Fail(
        int statusCode,
        string title,
        string detail,
        string errorCode,
        IReadOnlyList<WorkflowSourceSaveError> errors) =>
        new(null, new WorkflowSourceSaveProblem(statusCode, title, detail, errorCode, errors));
}

internal sealed record WorkflowSourceSaveProblem(
    int StatusCode,
    string Title,
    string Detail,
    string ErrorCode,
    IReadOnlyList<WorkflowSourceSaveError> Errors);

internal sealed record WorkflowSourceSaveError(
    string Code,
    string Message,
    string? Path);
