using UmbracoPrism.Shared.Models.ServiceDesign;

namespace UmbracoPrism.Core.Models.ServiceDesign;

/// <summary>
/// Resolves the workflow render shell from the component tree, with legacy <c>stepType</c> fallback.
/// </summary>
public static class ServiceRequestRenderShellResolver
{
    public static string ResolveShell(
        IReadOnlyList<PrismComponentRenderPayload>? components,
        string? legacyStepType,
        bool hasWaitingConfig,
        bool hasAvailableActions)
    {
        var items = components ?? Array.Empty<PrismComponentRenderPayload>();

        if (hasWaitingConfig || HasComponentType(items, "waiting"))
        {
            return "waiting";
        }

        if (HasComponentType(items, "task-list"))
        {
            return "task-list";
        }

        if (AllDataCarryingComponentsAreSummaryLists(items))
        {
            return "check-answers";
        }

        var hasInteractiveInputs = items.Any(ComponentHasInteractiveInputs);

        if (HasComponentType(items, "panel") && !hasInteractiveInputs)
        {
            return "confirmation";
        }

        if (items.Count > 0 && !hasInteractiveInputs && !hasAvailableActions)
        {
            return "status-timeline";
        }

        return NormalizeShell(legacyStepType) ?? "question";
    }

    private static bool AllDataCarryingComponentsAreSummaryLists(IReadOnlyList<PrismComponentRenderPayload> components)
    {
        var dataCarryingTypes = components
            .Where(ComponentCarriesFieldData)
            .Select(c => NormalizeType(c.Type))
            .ToArray();

        return dataCarryingTypes.Length > 0
            && dataCarryingTypes.All(type => string.Equals(type, "summary-list", StringComparison.Ordinal));
    }

    private static bool ComponentCarriesFieldData(PrismComponentRenderPayload component) =>
        component.Fields.Any() || (component.AccordionSections?.Any(section => section.Fields.Any()) ?? false);

    private static bool ComponentHasInteractiveInputs(PrismComponentRenderPayload component)
    {
        if (component.Fields.Any(field => !IsContentOnlyFieldType(field.FieldType)))
        {
            return true;
        }

        return component.AccordionSections?.Any(section =>
            section.Fields.Any(field => !IsContentOnlyFieldType(field.FieldType))) ?? false;
    }

    private static bool IsContentOnlyFieldType(string? fieldType) => NormalizeType(fieldType) switch
    {
        "inset-text" or "warning-text" or "details" or "notification-banner" or "body" or "heading" => true,
        _ => false
    };

    private static bool HasComponentType(IReadOnlyList<PrismComponentRenderPayload> components, string expectedType) =>
        components.Any(component => string.Equals(
            NormalizeType(component.Type),
            expectedType,
            StringComparison.Ordinal));

    private static string? NormalizeShell(string? stepType) => NormalizeType(stepType) switch
    {
        "collect" => "question",
        "review" => "check-answers",
        "completion" => "confirmation",
        "statustimeline" => "status-timeline",
        "tasklist" => "task-list",
        "" => null,
        var normalized => normalized
    };

    private static string NormalizeType(string? value) =>
        (value ?? string.Empty).Trim().ToLowerInvariant();
}
