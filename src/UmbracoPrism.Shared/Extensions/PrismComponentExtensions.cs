using UmbracoPrism.Shared.Models.Workflow.Components;

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
    /// Returns the first descendant of type <typeparamref name="T"/> in the tree, or null if none exists.
    /// </summary>
    public static T? FindFirst<T>(this IEnumerable<PrismComponent> components) where T : PrismComponent
        => components.Flatten().OfType<T>().FirstOrDefault();

    /// <summary>
    /// Infers the GDS step type for a state based on the components it contains.
    /// Replaces the V1 <c>EffectiveStepType</c> property which lived on the state record.
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
