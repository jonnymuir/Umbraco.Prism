using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Wayfinder.Models.ServiceDesign;
using UmbracoPrism.ProcessManager.Services;

namespace UmbracoPrism.MockBusinessApp.Services;

internal static class ServiceBlueprintSourceSaveRequestParser
{
    private const string InvalidServiceBlueprintPayloadTitle = "Invalid service blueprint payload";
    private const string InvalidServiceBlueprintPayloadDetail = "Every service blueprint component must include a supported 'type' value before the service blueprint can be saved.";
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

    public static async Task<ServiceBlueprintSourceSaveParseResult> ParseAsync(
        HttpContext context,
        JsonSerializerOptions serializerOptions,
        ServiceBlueprintAuthoringService authoringService,
        CancellationToken ct = default)
    {
        using var reader = new StreamReader(context.Request.Body);
        var payload = await reader.ReadToEndAsync(ct);

        if (string.IsNullOrWhiteSpace(payload))
        {
            return ServiceBlueprintSourceSaveParseResult.Fail(
                400,
                InvalidServiceBlueprintPayloadTitle,
                "Request body was empty.",
                "service-blueprint-payload-empty",
                [new ServiceBlueprintSourceSaveError("request-body-empty", "Provide a service blueprint JSON document in the request body.", "$")]);
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(payload);
        }
        catch (JsonException ex)
        {
            return ServiceBlueprintSourceSaveParseResult.Fail(
                400,
                InvalidServiceBlueprintPayloadTitle,
                "The service blueprint JSON could not be read.",
                "service-blueprint-json-invalid",
                [new ServiceBlueprintSourceSaveError(
                    "json-invalid",
                    "Fix the malformed JSON document and try saving again.",
                    string.IsNullOrWhiteSpace(ex.Path) ? "$" : ex.Path)]);
        }

        using (document)
        {
            var componentIssues = ValidateComponents(document.RootElement);
            if (componentIssues.Count > 0)
            {
                return ServiceBlueprintSourceSaveParseResult.Fail(
                    400,
                    InvalidServiceBlueprintPayloadTitle,
                    InvalidServiceBlueprintPayloadDetail,
                    "service-blueprint-component-invalid",
                    componentIssues);
            }
        }

        try
        {
            var serviceBlueprint = JsonSerializer.Deserialize<ServiceBlueprint>(payload, serializerOptions);
            if (serviceBlueprint is null)
            {
                return ServiceBlueprintSourceSaveParseResult.Fail(
                    400,
                    InvalidServiceBlueprintPayloadTitle,
                    "Request body was empty.",
                    "service-blueprint-payload-empty",
                    [new ServiceBlueprintSourceSaveError("request-body-empty", "Provide a service blueprint JSON document in the request body.", "$")]);
            }

            // Same toolkit validation the AI-authoring surface runs (ValidateGatewayRouting() +
            // calculations smoke-check) — this endpoint's component-type whitelist above is the
            // only check that's specific to this host's save path.
            var outcome = authoringService.Validate(serviceBlueprint);
            if (!outcome.IsValid)
            {
                return ServiceBlueprintSourceSaveParseResult.Fail(
                    400,
                    InvalidServiceBlueprintPayloadTitle,
                    "The service blueprint definition failed validation.",
                    "service-blueprint-validation-invalid",
                    outcome.Diagnostics.Take(MaxIssues)
                        .Select(diagnostic => new ServiceBlueprintSourceSaveError(diagnostic.Code, diagnostic.Message, diagnostic.Path))
                        .ToArray());
            }

            return ServiceBlueprintSourceSaveParseResult.Success(serviceBlueprint);
        }
        catch (JsonException ex)
        {
            return ServiceBlueprintSourceSaveParseResult.Fail(
                400,
                InvalidServiceBlueprintPayloadTitle,
                "The service blueprint JSON did not match the save contract.",
                "service-blueprint-json-invalid",
                [new ServiceBlueprintSourceSaveError(
                    "json-invalid",
                    "Fix the invalid service blueprint JSON value and try saving again.",
                    string.IsNullOrWhiteSpace(ex.Path) ? "$" : ex.Path)]);
        }
        catch (NotSupportedException)
        {
            return ServiceBlueprintSourceSaveParseResult.Fail(
                400,
                InvalidServiceBlueprintPayloadTitle,
                InvalidServiceBlueprintPayloadDetail,
                "service-blueprint-component-invalid",
                [new ServiceBlueprintSourceSaveError(
                    "component-type-invalid",
                    "Add a supported 'type' value to every service blueprint component before saving.",
                    "$.states[*].components[*]")]);
        }
    }

    public static IResult ToProblemResult(HttpContext context, ServiceBlueprintSourceSaveProblem problem)
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

    private static IReadOnlyList<ServiceBlueprintSourceSaveError> ValidateComponents(JsonElement root)
    {
        var errors = new List<ServiceBlueprintSourceSaveError>();
        ValidateStateComponents(root, "stages", errors);
        return errors;
    }

    private static void ValidateStateComponents(JsonElement root, string propertyName, List<ServiceBlueprintSourceSaveError> errors)
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

    private static void ValidateComponentArray(JsonElement components, string path, List<ServiceBlueprintSourceSaveError> errors)
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

    private static void ValidateComponent(JsonElement component, string path, List<ServiceBlueprintSourceSaveError> errors)
    {
        if (errors.Count >= MaxIssues)
        {
            return;
        }

        if (component.ValueKind != JsonValueKind.Object)
        {
            errors.Add(new ServiceBlueprintSourceSaveError(
                "component-invalid",
                "Service blueprint components must be JSON objects.",
                path));
            return;
        }

        if (!component.TryGetProperty("type", out var typeElement) || typeElement.ValueKind != JsonValueKind.String)
        {
            errors.Add(new ServiceBlueprintSourceSaveError(
                "component-type-missing",
                "Service blueprint components must declare a supported 'type' value.",
                path));
            return;
        }

        var type = typeElement.GetString();
        if (string.IsNullOrWhiteSpace(type) || !SupportedComponentTypes.Contains(type))
        {
            errors.Add(new ServiceBlueprintSourceSaveError(
                "component-type-unsupported",
                $"Service blueprint component type '{type ?? string.Empty}' is not supported by the save API.",
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

internal sealed record ServiceBlueprintSourceSaveParseResult(
    ServiceBlueprint? ServiceBlueprintValue,
    ServiceBlueprintSourceSaveProblem? Problem)
{
    public static ServiceBlueprintSourceSaveParseResult Success(ServiceBlueprint serviceBlueprint) => new(serviceBlueprint, null);

    public static ServiceBlueprintSourceSaveParseResult Fail(
        int statusCode,
        string title,
        string detail,
        string errorCode,
        IReadOnlyList<ServiceBlueprintSourceSaveError> errors) =>
        new(null, new ServiceBlueprintSourceSaveProblem(statusCode, title, detail, errorCode, errors));
}

internal sealed record ServiceBlueprintSourceSaveProblem(
    int StatusCode,
    string Title,
    string Detail,
    string ErrorCode,
    IReadOnlyList<ServiceBlueprintSourceSaveError> Errors);

internal sealed record ServiceBlueprintSourceSaveError(
    string Code,
    string Message,
    string? Path);
