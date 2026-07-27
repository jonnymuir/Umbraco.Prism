using System.Globalization;
using UmbracoPrism.Shared.Models.ServiceDesign.Calculations;

namespace UmbracoPrism.Shared.Services.Calculations;

/// <summary>Result of evaluating a <see cref="ServiceBlueprintCalculationSet"/>.</summary>
public sealed record CalculationResult
{
    /// <summary>Computed field values (decimal, bool or string) in declaration order.</summary>
    public IReadOnlyDictionary<string, object?> Fields { get; init; }
        = new Dictionary<string, object?>();

    /// <summary>Computed series: name → rows, each row containing the loop variable plus the value columns.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyDictionary<string, object?>>> Series { get; init; }
        = new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, object?>>>();
}

/// <summary>Which part of a <see cref="ServiceBlueprintCalculationSet"/> a <see cref="CalculationDiagnostic"/> came from.</summary>
public enum CalculationDiagnosticKind { Field, Series }

/// <summary>One field/series failure collected by <see cref="CalculationEvaluator.EvaluateCollectingErrors"/>.</summary>
public sealed record CalculationDiagnostic(CalculationDiagnosticKind Kind, string Name, string Message);

/// <summary>
/// Result of <see cref="CalculationEvaluator.EvaluateCollectingErrors"/> — the best-effort
/// <see cref="CalculationResult"/> (failed fields/series are omitted, not thrown) plus every
/// diagnostic collected along the way.
/// </summary>
public sealed record CalculationEvaluationResult(CalculationResult Result, IReadOnlyList<CalculationDiagnostic> Diagnostics);

/// <summary>
/// Evaluates a blueprint calculation set against a scope of input values.
///
/// Numeric semantics: all arithmetic is <see cref="decimal"/>. The single exception is
/// <c>pow</c>, which round-trips through <see cref="double"/> because fractional
/// exponents are not exact in any base — outputs that matter should be wrapped in
/// <c>round()</c>. Any other runtime implementing this grammar (e.g. the TypeScript
/// client evaluator) must match these semantics; the shared golden fixtures in
/// <c>src/UmbracoPrism.Shared/calculation-fixtures/</c> are the conformance suite.
/// </summary>
public sealed class CalculationEvaluator
{
    private const int MaxSeriesRows = 1000;

    /// <summary>
    /// Evaluates a single standalone expression (e.g. a component's showWhen) against a
    /// scope that already contains inputs and calculated fields. Tables from
    /// <paramref name="context"/> are available to lookup().
    /// </summary>
    public object? EvaluateExpression(
        string expression,
        IReadOnlyDictionary<string, object?> scope,
        ServiceBlueprintCalculationSet? context = null)
    {
        return EvaluateNode(
            CalculationExpressionParser.Parse(expression),
            scope,
            context ?? new ServiceBlueprintCalculationSet(),
            $"expression '{expression}'");
    }

    public CalculationResult Evaluate(
        ServiceBlueprintCalculationSet calculations,
        IReadOnlyDictionary<string, object?> inputs) =>
        EvaluateCore(calculations, inputs, collectErrors: false).Result;

    /// <summary>
    /// Like <see cref="Evaluate"/>, but collects every field/series failure instead of throwing
    /// on the first one — a field or series that fails is simply omitted from the result and
    /// recorded as a <see cref="CalculationDiagnostic"/>. Used by authoring-time validation,
    /// where an author needs every problem in one pass, not just the first.
    /// </summary>
    public CalculationEvaluationResult EvaluateCollectingErrors(
        ServiceBlueprintCalculationSet calculations,
        IReadOnlyDictionary<string, object?> inputs) =>
        EvaluateCore(calculations, inputs, collectErrors: true);

    private CalculationEvaluationResult EvaluateCore(
        ServiceBlueprintCalculationSet calculations,
        IReadOnlyDictionary<string, object?> inputs,
        bool collectErrors)
    {
        var scope = new Dictionary<string, object?>(inputs, StringComparer.Ordinal);
        var fields = new Dictionary<string, object?>(StringComparer.Ordinal);
        var diagnostics = new List<CalculationDiagnostic>();

        foreach (var (name, field) in calculations.Fields)
        {
            if (string.Equals(field.Source, "service", StringComparison.OrdinalIgnoreCase))
            {
                if (!scope.ContainsKey(name))
                {
                    throw new CalculationException(
                        $"Field '{name}' is service-sourced but the host did not supply it.");
                }

                continue;
            }

            if (string.IsNullOrWhiteSpace(field.Expr))
            {
                throw new CalculationException($"Field '{name}' has no expression and no service source.");
            }

            if (scope.ContainsKey(name))
            {
                throw new CalculationException($"Field '{name}' collides with an input or earlier field.");
            }

            try
            {
                var value = EvaluateNode(CalculationExpressionParser.Parse(field.Expr), scope, calculations, name);
                scope[name] = value;
                fields[name] = value;
            }
            catch (CalculationException ex) when (collectErrors)
            {
                diagnostics.Add(new CalculationDiagnostic(CalculationDiagnosticKind.Field, name, ex.Message));
                scope[name] = null;
            }
        }

        var series = new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, object?>>>(StringComparer.Ordinal);
        foreach (var (name, definition) in calculations.Series ?? new Dictionary<string, ServiceBlueprintCalculationSeries>())
        {
            try
            {
                series[name] = EvaluateSeries(name, definition, scope, calculations);
            }
            catch (CalculationException ex) when (collectErrors)
            {
                diagnostics.Add(new CalculationDiagnostic(CalculationDiagnosticKind.Series, name, ex.Message));
            }
        }

        return new CalculationEvaluationResult(new CalculationResult { Fields = fields, Series = series }, diagnostics);
    }

    private IReadOnlyList<IReadOnlyDictionary<string, object?>> EvaluateSeries(
        string seriesName,
        ServiceBlueprintCalculationSeries definition,
        Dictionary<string, object?> scope,
        ServiceBlueprintCalculationSet calculations)
    {
        if (string.IsNullOrWhiteSpace(definition.Over))
        {
            throw new CalculationException($"Series '{seriesName}' has no loop variable ('over').");
        }

        if (scope.ContainsKey(definition.Over))
        {
            throw new CalculationException(
                $"Series '{seriesName}' loop variable '{definition.Over}' collides with an existing name.");
        }

        var from = ToInteger(
            EvaluateNode(CalculationExpressionParser.Parse(definition.From), scope, calculations, seriesName),
            $"series '{seriesName}' 'from'");
        var to = ToInteger(
            EvaluateNode(CalculationExpressionParser.Parse(definition.To), scope, calculations, seriesName),
            $"series '{seriesName}' 'to'");

        if (to - from + 1 > MaxSeriesRows)
        {
            throw new CalculationException(
                $"Series '{seriesName}' would produce {to - from + 1} rows; the limit is {MaxSeriesRows}.");
        }

        var parsedValues = definition.Values
            .ToDictionary(pair => pair.Key, pair => CalculationExpressionParser.Parse(pair.Value));

        var rows = new List<IReadOnlyDictionary<string, object?>>();
        var rowScope = new Dictionary<string, object?>(scope, StringComparer.Ordinal);
        for (var step = from; step <= to; step++)
        {
            rowScope[definition.Over] = (decimal)step;
            var row = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [definition.Over] = (decimal)step
            };

            foreach (var (column, node) in parsedValues)
            {
                row[column] = EvaluateNode(node, rowScope, calculations, $"{seriesName}.{column}");
            }

            rows.Add(row);
        }

        return rows;
    }

    private object? EvaluateNode(
        CalcNode node,
        IReadOnlyDictionary<string, object?> scope,
        ServiceBlueprintCalculationSet calculations,
        string context)
    {
        switch (node)
        {
            case CalcNode.Number number:
                return number.Value;
            case CalcNode.Text text:
                return text.Value;
            case CalcNode.Bool boolean:
                return boolean.Value;

            case CalcNode.Identifier identifier:
                return ResolvePath(identifier.Path, scope, context);

            case CalcNode.Unary { Op: "-" } negate:
                return -ToDecimal(EvaluateNode(negate.Operand, scope, calculations, context), context);
            case CalcNode.Unary { Op: "not" } not:
                return !ToBool(EvaluateNode(not.Operand, scope, calculations, context), context);

            case CalcNode.Binary binary:
                return EvaluateBinary(binary, scope, calculations, context);

            case CalcNode.Call call:
                return EvaluateCall(call, scope, calculations, context);

            default:
                throw new CalculationException($"Unsupported expression node in {context}.");
        }
    }

    private object? EvaluateBinary(
        CalcNode.Binary binary,
        IReadOnlyDictionary<string, object?> scope,
        ServiceBlueprintCalculationSet calculations,
        string context)
    {
        // Short-circuit boolean operators before evaluating the right side.
        if (binary.Op is "and" or "or")
        {
            var leftBool = ToBool(EvaluateNode(binary.Left, scope, calculations, context), context);
            if (binary.Op == "and" && !leftBool)
            {
                return false;
            }

            if (binary.Op == "or" && leftBool)
            {
                return true;
            }

            return ToBool(EvaluateNode(binary.Right, scope, calculations, context), context);
        }

        var left = EvaluateNode(binary.Left, scope, calculations, context);
        var right = EvaluateNode(binary.Right, scope, calculations, context);

        switch (binary.Op)
        {
            case "=":
                return ValuesEqual(left, right);
            case "<>":
                return !ValuesEqual(left, right);
            case "<":
                return ToDecimal(left, context) < ToDecimal(right, context);
            case "<=":
                return ToDecimal(left, context) <= ToDecimal(right, context);
            case ">":
                return ToDecimal(left, context) > ToDecimal(right, context);
            case ">=":
                return ToDecimal(left, context) >= ToDecimal(right, context);
            case "+":
                return ToDecimal(left, context) + ToDecimal(right, context);
            case "-":
                return ToDecimal(left, context) - ToDecimal(right, context);
            case "*":
                return ToDecimal(left, context) * ToDecimal(right, context);
            case "/":
            {
                var divisor = ToDecimal(right, context);
                if (divisor == 0m)
                {
                    throw new CalculationException($"Division by zero in {context}.");
                }

                return ToDecimal(left, context) / divisor;
            }
            default:
                throw new CalculationException($"Unknown operator '{binary.Op}' in {context}.");
        }
    }

    private object? EvaluateCall(
        CalcNode.Call call,
        IReadOnlyDictionary<string, object?> scope,
        ServiceBlueprintCalculationSet calculations,
        string context)
    {
        object? Arg(int i) => EvaluateNode(call.Args[i], scope, calculations, context);
        decimal Num(int i) => ToDecimal(Arg(i), context);

        void RequireArgs(int count)
        {
            if (call.Args.Count != count)
            {
                throw new CalculationException(
                    $"{call.Name}() expects {count} argument(s), got {call.Args.Count}, in {context}.");
            }
        }

        switch (call.Name)
        {
            case "if":
                RequireArgs(3);
                return ToBool(Arg(0), context) ? Arg(1) : Arg(2);

            case "min":
            case "max":
            {
                if (call.Args.Count < 2)
                {
                    throw new CalculationException($"{call.Name}() expects at least 2 arguments in {context}.");
                }

                var result = Num(0);
                for (var i = 1; i < call.Args.Count; i++)
                {
                    var next = Num(i);
                    result = call.Name == "min" ? Math.Min(result, next) : Math.Max(result, next);
                }

                return result;
            }

            case "clamp":
                RequireArgs(3);
                return Math.Min(Math.Max(Num(0), Num(1)), Num(2));

            case "abs":
                RequireArgs(1);
                return Math.Abs(Num(0));

            case "floor":
                RequireArgs(1);
                return Math.Floor(Num(0));

            case "round":
            {
                if (call.Args.Count is not (1 or 2))
                {
                    throw new CalculationException($"round() expects 1 or 2 arguments in {context}.");
                }

                var places = call.Args.Count == 2 ? (int)Num(1) : 0;
                return Math.Round(Num(0), places, MidpointRounding.AwayFromZero);
            }

            case "pow":
                RequireArgs(2);
                return (decimal)Math.Pow((double)Num(0), (double)Num(1));

            case "lookup":
                RequireArgs(2);
                if (call.Args[0] is not CalcNode.Identifier tableRef)
                {
                    throw new CalculationException($"lookup() requires a table name as its first argument in {context}.");
                }

                return Lookup(tableRef.Path, Num(1), calculations, context);

            default:
                throw new CalculationException($"Unknown function '{call.Name}' in {context}.");
        }
    }

    private static decimal Lookup(
        string tableName,
        decimal key,
        ServiceBlueprintCalculationSet calculations,
        string context)
    {
        if (calculations.Tables is null || !calculations.Tables.TryGetValue(tableName, out var table))
        {
            throw new CalculationException($"Unknown table '{tableName}' in {context}.");
        }

        var points = table.Values
            .Select(pair => (
                Key: decimal.Parse(pair.Key, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture),
                pair.Value))
            .OrderBy(point => point.Key)
            .ToArray();

        if (points.Length == 0)
        {
            throw new CalculationException($"Table '{tableName}' is empty, in {context}.");
        }

        if (key <= points[0].Key)
        {
            return points[0].Value;
        }

        if (key >= points[^1].Key)
        {
            return points[^1].Value;
        }

        for (var i = 1; i < points.Length; i++)
        {
            if (key > points[i].Key)
            {
                continue;
            }

            var (lowKey, lowValue) = points[i - 1];
            var (highKey, highValue) = points[i];

            if (string.Equals(table.Interpolate, "step", StringComparison.OrdinalIgnoreCase))
            {
                return key == highKey ? highValue : lowValue;
            }

            return lowValue + (highValue - lowValue) * (key - lowKey) / (highKey - lowKey);
        }

        return points[^1].Value;
    }

    private static object? ResolvePath(string path, IReadOnlyDictionary<string, object?> scope, string context)
    {
        var segments = path.Split('.');
        object? current = scope;
        foreach (var segment in segments)
        {
            if (current is IReadOnlyDictionary<string, object?> dictionary)
            {
                if (!dictionary.TryGetValue(segment, out current))
                {
                    throw new CalculationException($"Unknown name '{path}' in {context}.");
                }

                continue;
            }

            throw new CalculationException($"'{path}' cannot be resolved ('{segment}' is not a group) in {context}.");
        }

        return current;
    }

    private static bool ValuesEqual(object? left, object? right)
    {
        if (left is decimal || right is decimal)
        {
            return left is decimal l && right is decimal r && l == r;
        }

        return Equals(left, right);
    }

    private static decimal ToDecimal(object? value, string context) => value switch
    {
        decimal d => d,
        int i => i,
        long l => l,
        double dbl => (decimal)dbl,
        _ => throw new CalculationException(
            $"Expected a number but got {(value is null ? "nothing" : $"'{value}'")} in {context}.")
    };

    private static bool ToBool(object? value, string context) => value switch
    {
        bool b => b,
        _ => throw new CalculationException(
            $"Expected true/false but got {(value is null ? "nothing" : $"'{value}'")} in {context}.")
    };

    private static int ToInteger(object? value, string context)
    {
        var number = ToDecimal(value, context);
        if (number != Math.Floor(number))
        {
            throw new CalculationException($"Expected a whole number in {context}.");
        }

        return (int)number;
    }
}
