# The Prism Calculation Language

A total, side-effect-free expression language for the maths behind a service blueprint —
pension quotes, eligibility thresholds, banded tax calculations, whatever a stage
needs to compute. It's the **only** place business maths should live: don't
hand-write it in a host service or a client component (see
[CLAUDE.md](../../CLAUDE.md#declarative-calculations--live-stages-money-modeller-pattern)).
Two runtimes implement this exact grammar with matching semantics — C#
(`Wayfinder/Services/Calculations`, authoritative) and TypeScript
(`src/UmbracoPrism.Client/src/calculations/calculation-engine.ts`, indicative,
re-evaluates the same definitions client-side between form submits). Both are
checked against one conformance suite,
[`calculation-golden.json`](../../src/Wayfinder/calculation-fixtures/calculation-golden.json)
— if you're unsure whether something is legal syntax, that file is the ground truth.

This document is also exposed as an MCP resource (`service-blueprint-docs://calculation-language`)
so an AI agent authoring service blueprints through the MCP toolkit can fetch it directly, without
needing filesystem access to this repo — see
[AI-Ready Service Blueprint Authoring](./ai-service-blueprint-authoring.md).

## Where it lives in a service blueprint

A `ServiceBlueprint` may carry a top-level `calculations` block:

```json
"calculations": {
  "tables": { "<tableName>": { "interpolate": "linear", "values": { "<key>": <number>, ... } } },
  "fields":  { "<fieldName>": { "expr": "<expression>" } },
  "series":  { "<seriesName>": { "over": "<loopVar>", "from": "<expr>", "to": "<expr>", "values": { "<column>": "<expression>" } } }
}
```

- **`fields`** are evaluated once, in declaration order, into a shared scope. Each
  field's expression can reference any input field, any earlier-declared field, or a
  service-sourced value — but not a field declared later (forward references are an
  error).
- **`tables`** are static lookup tables consumed via the `lookup()` function.
- **`series`** repeat a set of expressions across a range of an integer loop variable
  (e.g. "projected income for every age 66 to 90") — the standard way to drive a
  `chart` component.

Every input component (`number`, `decimal`, `slider`, etc.) is automatically in
scope under its `fieldKey`, typed as `decimal` for numeric field types and `string`
otherwise (`CalculationScopeBuilder`), seeded from the submitted value or the
component's own `default` if nothing's been submitted yet. Any component may also
declare a `showWhen` expression (a plain string in this same language) to control
its own visibility — see [Visibility (`showWhen`)](#visibility-showwhen) below.

**`validate_service_blueprint` has no submitted values to work with.** If an input has
neither a real submission (there isn't one — it's a static check) nor a `default`,
it's simply absent from scope, not an error — any field expression referencing it
then fails with an unresolvable-reference diagnostic, which looks like the
expression is wrong even when it isn't. Two ways to avoid this false alarm while
authoring: give the input a sensible `default` (recommended — it also seeds the
real form), or verify the calculation via `simulate_service_blueprint` instead, which takes
real `fieldValues` per step and resolves cleanly regardless of defaults. Every
worked example below (including `money-modeller.json`) declares a `default` on
every input its calculations depend on for exactly this reason.

## Grammar

Lowest to highest precedence:

```
or             ::= and ( "or" and )*
and            ::= not ( "and" not )*
not            ::= "not" not | comparison
comparison     ::= additive ( ("=" | "<>" | "<" | "<=" | ">" | ">=") additive )?
additive       ::= multiplicative ( ("+" | "-") multiplicative )*
multiplicative ::= unary ( ("*" | "/") unary )*
unary          ::= "-" unary | primary
primary        ::= number | string | "true" | "false" | identifier-path
                  | identifier "(" args ")" | "(" or ")"
```

That's the entire language — no assignment, no loops (outside the declarative
`series` construct), no array indexing, no arbitrary method calls. Every expression
is guaranteed to terminate.

- **Numbers** are invariant-culture decimals: `55`, `0.1`, `74208`. No thousands
  separators, no currency symbols in the expression itself — those belong on the
  *input component* (`prefix: "£"`), stripped before the value reaches the scope.
- **Strings** use single quotes: `'Maximum tax-free cash'`.
- **Booleans** are the bare identifiers `true`/`false`.
- **Identifier paths** are dotted for nested (service-sourced) values:
  `member.age`, `member.active`. A bare identifier resolves an input field, an
  earlier calculated field, or a series loop variable.
- Comparison, `and`/`or`/`not` all read as plain English. `and`/`or` **short-circuit**
  — `false and (1 / 0 > 0)` evaluates to `false` without ever touching the division.
- Operator precedence is standard (`*`/`/` bind tighter than `+`/`-`; comparisons
  bind tighter than `and`; `and` binds tighter than `or`); use parentheses to
  override it, as usual.

## Numeric semantics

All arithmetic is C# `decimal` / an equivalent fixed-point type client-side — **not**
floating point, so `0.1 + 0.2 = 0.3` exactly, no drift. The one exception is `pow()`,
which round-trips through `double` because fractional exponents have no exact
decimal representation in any base. If a `pow()` result needs to match exactly
between runtimes or feed into a currency display, wrap it in `round()`.

## Functions

| Function | Signature | Behaviour |
|---|---|---|
| `if` | `if(cond, then, else)` | Ternary. `cond` must evaluate to a boolean. |
| `min` / `max` | `min(a, b, ...)` (2+ args) | Numeric min/max across all arguments. |
| `clamp` | `clamp(value, lo, hi)` | Equivalent to `min(max(value, lo), hi)`. |
| `abs` | `abs(x)` | Absolute value. |
| `floor` | `floor(x)` | Round toward negative infinity. |
| `round` | `round(x)` or `round(x, places)` | Rounds **away from zero** at the midpoint (`round(2.5) = 3`, `round(-2.5) = -3`), not banker's rounding. `places` defaults to `0`. |
| `pow` | `pow(x, y)` | `x` raised to the power `y`. Round-trips through `double` — see [Numeric semantics](#numeric-semantics). |
| `lookup` | `lookup(tableName, key)` | Looks up `key` in a `tables` entry. The first argument must be a bare table name, not an expression. See [Tables and `lookup()`](#tables-and-lookup). |

Calling an unknown function, or calling a known one with the wrong argument count,
is a `CalculationException` — see [Errors](#errors).

## Tables and `lookup()`

```json
"tables": {
  "pensionAgeFactor": {
    "interpolate": "linear",
    "values": { "55": 0.56, "66": 1.0, "75": 1.27 }
  }
}
```

- `values` keys are decimal strings (parsed as the lookup key), values are numbers.
- `interpolate` is `"linear"` (default) or `"step"`.
  - **Linear**: interpolates between the two nearest keys —
    `lookup(pensionAgeFactor, 60)` with the table above gives `0.56 + (1.0 - 0.56) * (60 - 55) / (66 - 55) ≈ 0.76`.
  - **Step**: holds the *lower* key's value until the next key is reached exactly —
    `lookup(band, 99)` on `{"0": 10, "100": 20}` returns `10`, not `20`, until the key
    hits `100`.
- A key **outside** the table's range clamps to the nearest edge value rather than
  erroring — `lookup(factor, 90)` on a table whose highest key is `75` returns the
  value at `75`.
- An unknown table name, or an empty table, is a `CalculationException`.

## Series

```json
"series": {
  "incomeByAge": {
    "over": "age",
    "from": "retireAgeEff",
    "to": "90",
    "values": {
      "db": "round(pensionOut)",
      "sp": "if(age >= statePensionAge, round(statePension), 0)"
    }
  }
}
```

Produces one row per integer step from `from` to `to` inclusive, with the loop
variable (`age` here) in scope for every `values` expression alongside every field
and input. `from`/`to` are themselves expressions (evaluated once, must resolve to
whole numbers) — so a series range can depend on a calculated field, as above
(`retireAgeEff`). Capped at 1000 rows; a series that would produce more is a
`CalculationException`. If `to < from`, the series is simply empty — not an error.
The loop variable name must not collide with an existing input or field name.

Series are the data source for `chart` components — bind a chart's `series` property
to the series name, and its `x`/band keys to columns declared in `values`.

## Visibility (`showWhen`)

Any component (not just inputs — headings, warning banners, whole fieldsets) may
carry a `showWhen` string in this same expression language, evaluated against the
same scope as `calculations` (inputs, service inputs, calculated fields). A few real
examples from `money-modeller.json`:

```json
{ "type": "warning-text", "showWhen": "not quoteMode and retireAge < npa", "content": "..." }
{ "type": "slider", "showWhen": "not quoteMode and hasDc", "fieldKey": "invReturn", ... }
```

When it evaluates to `false`, the component renders hidden; the client-side
live-form runtime re-evaluates it as the user changes inputs, so visibility updates
without a full page round-trip. `showWhen` must evaluate to a boolean — a `showWhen`
that evaluates to a number or string is a `CalculationException`, same as any other
type mismatch.

## Service-sourced fields

A field with no `expr` and `"source": "service"` instead is supplied by the *host
application*, not computed:

```json
"fields": { "member": { "source": "service" } }
```

The host implements `ProcessManagerEngine.ResolveServiceInputs(...)` to supply it
(e.g. a member record fetched from a system of record) — see
`BusinessAppProcessManager.ResolveServiceInputs` in `UmbracoPrism.MockBusinessApp` for
the real example backing `money-modeller.json`'s `member` field. A service field with
no value supplied is a `CalculationException` when evaluated for real; the
`validate_service_blueprint`/`simulate_service_blueprint` MCP tools have specific, non-fatal handling
for this case — see [AI-Ready Service Blueprint Authoring](./ai-service-blueprint-authoring.md).

## Format hints

A field may declare `"format": "gbp"` to tell the *rendering* layer (not the
calculation itself) to display it as currency — the calculated value stays a plain
`decimal` in the scope; formatting is a display concern applied when the value is
bound into a `stat-group` item, `summary-list` row, or similar.

## Errors

Every parse or evaluation failure throws `CalculationException` with a message
carrying enough context to act on — position for parse errors
(`"Unexpected 'x' at position 12 in: 1 + x"`), field/series/expression name for
evaluation errors (`"Field 'x' has no expression and no service source."`,
`"Unknown name 'y' in expression 'x'."`). Common cases:

| Situation | Example message |
|---|---|
| Unknown identifier | `Unknown name 'nosuchthing' in expression '...'.` |
| Unknown function | `Unknown function 'eval' in ...` |
| Wrong argument count | `clamp() expects 3 argument(s), got 2, in ...` |
| Type mismatch | `Expected a number but got 'true' in ...` |
| Division by zero | `Division by zero in ...` |
| Forward field reference | `Unknown name 'b' in expression 'b + 1'.` (fields only see earlier-declared fields) |
| Field name collides with an input or earlier field | `Field 'x' collides with an input or earlier field.` |
| Unresolved service field | `Field 'member' is service-sourced but the host did not supply it.` |
| Unknown table | `Unknown table 'foo' in ...` |
| Series too large | `Series 's' would produce 2000 rows; the limit is 1000.` |

When authoring through the MCP toolkit, `validate_service_blueprint` surfaces these against
the whole service blueprint (calculated fields, series, **and** every component's `showWhen`)
as structured diagnostics you can act on directly, rather than needing to run the
service blueprint to discover a broken expression — see
[AI-Ready Service Blueprint Authoring](./ai-service-blueprint-authoring.md).

## Worked example: `money-modeller.json`

The one seed service blueprint that exercises the full language end-to-end is
[`money-modeller.json`](../../src/UmbracoPrism.MockBusinessApp/service-blueprints/money-modeller.json)
— a pension modeller. Its field chain is worth reading top-to-bottom as a model for
how to structure a non-trivial calculation set: each field builds on the last, so
the dependency order *is* the declaration order.

1. **`member`** — `source: "service"`, the host-supplied member record (name, age,
   salary, accrued benefits).
2. **`quoteMode`** (`qPension > 0`) and **`todaysMoney`** — booleans that gate large
   parts of the rest of the calculation and several components' `showWhen`.
3. **`minRetireAge`** (`max(55, member.age + 1)`) / **`maxRetireAge`** — bounds fed
   into `retireAgeEff` via `clamp()`.
4. **`retireAgeEff`** — `clamp(if(quoteMode, qAge, retireAge), minRetireAge, maxRetireAge)`,
   the single effective retirement age used everywhere downstream, whether the
   member is modelling freely or matching a formal quote.
5. **`futurePension`**, **`basePension`**, **`baseLump`**, **`pot`** — the projection
   maths, using `pow()` for compound growth (`pow(1 + realReturn, years)`) and
   `if(member.active and not quoteMode, ..., 0)` to gate projection entirely when a
   member has already left or is working from a fixed quote.
6. **`pensionFactor`** / **`lumpFactor`** — `lookup()` against the `pensionAgeFactor`
   / `lumpAgeFactor` tables, applying the early/late retirement adjustment.
7. **`adjPension`**, **`adjLump`**, **`adjPot`**, **`totalValue`** — the age- and
   inflation-adjusted final figures, and the total pot value used to work out the
   maximum tax-free cash (`maxTfc = 0.25 * totalValue`).
8. **`resultPension`**, **`resultCash`**, **`resultDcIncome`**, **`resultTotal`** —
   the final `round(...)`-ed, `format: "gbp"` fields bound directly to the
   `stat-group` component on the `model` stage.
9. The **`incomeByAge`** series projects `resultPension`-style figures across every
   age from `retireAgeEff` to `90`, feeding the `chart` component.

Fetch the full file to see every field's exact expression — this is the reference
to copy the *shape* of a real calculation set from, not to reproduce verbatim.
