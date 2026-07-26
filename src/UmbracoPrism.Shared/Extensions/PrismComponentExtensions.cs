using UmbracoPrism.Shared.Models.ServiceDesign.Components;

namespace UmbracoPrism.Shared.Extensions;

/// <summary>
/// Tree-walking helpers for the v2.0 polymorphic <see cref="PrismComponent"/> hierarchy.
/// </summary>
public static class PrismComponentExtensions
{
    /// <summary>
    /// Recursively walks the component tree and yields every component, including
    /// descendants nested inside fieldsets, accordion sections and conditional children
    /// of radios/checkboxes.
    /// </summary>
    public static IEnumerable<PrismComponent> Flatten(this IEnumerable<PrismComponent> components)
    {
        foreach (var component in components)
        {
            yield return component;

            switch (component)
            {
                case FieldsetComponent fieldset:
                    foreach (var child in fieldset.Children.Flatten())
                        yield return child;
                    break;

                case AccordionComponent accordion:
                    foreach (var section in accordion.Sections)
                        foreach (var child in section.Children.Flatten())
                            yield return child;
                    break;

                case RadiosComponent radios when radios.ConditionalChildren is { Count: > 0 }:
                    foreach (var children in radios.ConditionalChildren.Values)
                        foreach (var child in children.Flatten())
                            yield return child;
                    break;

                case CheckboxesComponent checkboxes when checkboxes.ConditionalChildren is { Count: > 0 }:
                    foreach (var children in checkboxes.ConditionalChildren.Values)
                        foreach (var child in children.Flatten())
                            yield return child;
                    break;
            }
        }
    }

    /// <summary>
    /// Returns every <see cref="InputComponent"/> in the tree, regardless of nesting depth.
    /// </summary>
    public static IEnumerable<InputComponent> GetAllInputs(this IEnumerable<PrismComponent> components)
        => components.Flatten().OfType<InputComponent>();

    /// <summary>
    /// Recursively walks the component tree like <see cref="Flatten"/>, but also yields each
    /// component's document path (e.g. <c>touchpoints.review.components[2].children[0]</c>) rooted
    /// at <paramref name="basePath"/>, for callers that need to address a specific component in
    /// diagnostics.
    /// </summary>
    public static IEnumerable<(PrismComponent Component, string Path)> FlattenWithPaths(
        this IEnumerable<PrismComponent> components, string basePath)
    {
        var index = 0;
        foreach (var component in components)
        {
            var path = $"{basePath}[{index}]";
            index++;
            yield return (component, path);

            switch (component)
            {
                case FieldsetComponent fieldset:
                    foreach (var descendant in fieldset.Children.FlattenWithPaths($"{path}.children"))
                        yield return descendant;
                    break;

                case AccordionComponent accordion:
                {
                    var sectionIndex = 0;
                    foreach (var section in accordion.Sections)
                    {
                        foreach (var descendant in section.Children.FlattenWithPaths($"{path}.sections[{sectionIndex}].children"))
                            yield return descendant;
                        sectionIndex++;
                    }

                    break;
                }

                case RadiosComponent radios when radios.ConditionalChildren is { Count: > 0 }:
                    foreach (var (option, children) in radios.ConditionalChildren)
                        foreach (var descendant in children.FlattenWithPaths($"{path}.conditionalChildren.{option}"))
                            yield return descendant;
                    break;

                case CheckboxesComponent checkboxes when checkboxes.ConditionalChildren is { Count: > 0 }:
                    foreach (var (option, children) in checkboxes.ConditionalChildren)
                        foreach (var descendant in children.FlattenWithPaths($"{path}.conditionalChildren.{option}"))
                            yield return descendant;
                    break;
            }
        }
    }

    /// <summary>
    /// Returns the first descendant of type <typeparamref name="T"/> in the tree, or null if none exists.
    /// </summary>
    public static T? FindFirst<T>(this IEnumerable<PrismComponent> components) where T : PrismComponent
        => components.Flatten().OfType<T>().FirstOrDefault();

    /// <summary>
    /// Infers the GDS step type for a touchpoint based on the components it contains.
    /// Replaces the V1 <c>EffectiveStepType</c> property which lived on the touchpoint record.
    /// </summary>
    public static string InferStepType(this IEnumerable<PrismComponent> components)
    {
        var list = components as IReadOnlyCollection<PrismComponent> ?? components.ToArray();

        if (list.OfType<WaitingComponent>().Any())
            return "status-timeline";
        if (list.OfType<PanelComponent>().Any())
            return "confirmation";
        if (list.OfType<SummaryListComponent>().Any())
            return "check-answers";
        if (list.OfType<TaskListComponent>().Any())
            return "task-list";
        return "question";
    }
}
