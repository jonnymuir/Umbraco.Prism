using System.Text.Json.Serialization;

namespace UmbracoPrism.Shared.Models.Workflow.Calculations;

/// <summary>
/// Declarative calculation block for a workflow definition. Fields are evaluated in
/// declaration order against the instance's input values (plus any service-sourced
/// inputs the host supplies); series are bounded comprehensions over an integer range.
/// The expression language is total — arithmetic, comparisons, boolean logic, and a
/// whitelisted function set only. There is no eval, no member access, no side effects.
/// </summary>
public sealed record WorkflowCalculationSet
{
    /// <summary>Named lookup tables (e.g. actuarial factor tables).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, WorkflowCalculationTable>? Tables { get; init; }

    /// <summary>
    /// Named calculated fields, evaluated in declaration order. A field may reference
    /// inputs and any field declared before it.
    /// </summary>
    public IReadOnlyDictionary<string, WorkflowCalculationField> Fields { get; init; }
        = new Dictionary<string, WorkflowCalculationField>();

    /// <summary>Named series — one row per integer step of the loop variable.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, WorkflowCalculationSeries>? Series { get; init; }
}

/// <summary>A single calculated field.</summary>
public sealed record WorkflowCalculationField
{
    /// <summary>Expression to evaluate. Null when <see cref="Source"/> is "service".</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Expr { get; init; }

    /// <summary>
    /// "service" marks a value the host application supplies at render time (the declared
    /// seam for maths that lives in an external system of record).
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Source { get; init; }

    /// <summary>
    /// Optional display format applied wherever this field's value is shown
    /// (stat-groups, summary-lists). Currently supported: "gbp" (£, no pence).
    /// The raw value stays numeric inside expressions.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Format { get; init; }
}

/// <summary>
/// A one-dimensional lookup table. Keys are numeric; <c>lookup(name, key)</c> clamps
/// below the first and above the last key, and interpolates between keys per
/// <see cref="Interpolate"/>.
/// </summary>
public sealed record WorkflowCalculationTable
{
    /// <summary>"linear" (default) or "step" (use the nearest key at or below).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Interpolate { get; init; }

    /// <summary>Key → value pairs. Keys are parsed as invariant decimals.</summary>
    public IReadOnlyDictionary<string, decimal> Values { get; init; }
        = new Dictionary<string, decimal>();
}

/// <summary>
/// A bounded comprehension: for each integer value of <see cref="Over"/> from
/// <see cref="From"/> to <see cref="To"/> (inclusive, step 1), evaluate each expression
/// in <see cref="Values"/> with the loop variable in scope. Produces one row per step.
/// </summary>
public sealed record WorkflowCalculationSeries
{
    /// <summary>Loop variable name, available to the value expressions.</summary>
    public string Over { get; init; } = "";

    /// <summary>Expression for the inclusive lower bound.</summary>
    public string From { get; init; } = "";

    /// <summary>Expression for the inclusive upper bound.</summary>
    public string To { get; init; } = "";

    /// <summary>Named value expressions evaluated per row.</summary>
    public IReadOnlyDictionary<string, string> Values { get; init; }
        = new Dictionary<string, string>();
}
