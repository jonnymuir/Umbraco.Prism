using System.Reflection;
using System.Text.Json.Serialization;

namespace UmbracoPrism.Shared.Models.ServiceDesign.Components;

/// <summary>
/// Reflects <see cref="PrismComponent"/>'s <c>[JsonDerivedType]</c> attribute list ONCE into
/// static lookups — the single source of truth for which component "type" discriminator
/// strings actually exist, avoiding a second hand-maintained list that can drift (a hardcoded
/// switch elsewhere in the runtime still lists "tel" despite <c>TelComponent</c> having no
/// <c>[JsonDerivedType]</c> entry and therefore never actually being deserializable — proof of
/// the drift risk this catalog exists to prevent; not fixed here, out of scope).
///
/// This also doubles as Prism's own honest, published declaration of what its stock rendering
/// pipeline supports — referenced directly (not re-derived or guessed) by any host wanting to
/// assert "I render the full built-in catalog" for one of its queues.
/// </summary>
public static class PrismComponentTypeCatalog
{
    private static readonly IReadOnlyDictionary<string, Type> DiscriminatorToTypeMap = BuildDiscriminatorToType();

    private static readonly IReadOnlyDictionary<Type, string> TypeToDiscriminatorMap =
        DiscriminatorToTypeMap.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);

    /// <summary>Every valid "type" discriminator string, e.g. "text", "summary-list", "chart".</summary>
    public static IReadOnlyList<string> AllDiscriminators { get; } =
        DiscriminatorToTypeMap.Keys.OrderBy(d => d, StringComparer.Ordinal).ToList();

    /// <summary>The discriminator string for a component instance, e.g. "text" for TextInputComponent.</summary>
    public static string DiscriminatorFor(PrismComponent component) =>
        TypeToDiscriminatorMap.TryGetValue(component.GetType(), out var discriminator)
            ? discriminator
            : throw new InvalidOperationException(
                $"{component.GetType().Name} has no [JsonDerivedType] discriminator on PrismComponent.");

    private static IReadOnlyDictionary<string, Type> BuildDiscriminatorToType() =>
        typeof(PrismComponent)
            .GetCustomAttributes<JsonDerivedTypeAttribute>(inherit: false)
            .ToDictionary(a => (string)a.TypeDiscriminator!, a => a.DerivedType, StringComparer.Ordinal);
}
