/**
 * TypeScript evaluator for the Prism service blueprint calculation language.
 *
 * This mirrors the C# implementation (UmbracoPrism.Shared/Services/Calculations) —
 * same grammar, same functions, same semantics. The shared conformance suite in
 * UmbracoPrism.Shared/calculation-fixtures/calculation-golden.json is executed against
 * both runtimes; change either implementation only alongside those fixtures.
 *
 * Numeric semantics: fixed-point decimal (BigInt mantissa, 12 decimal places) rather
 * than IEEE floats, so 0.1 + 0.2 = 0.3 here exactly as it does in C# decimal. The one
 * float excursion is pow(), which computes via double in both runtimes — wrap outputs
 * that matter in round().
 */

export class CalculationError extends Error {}

const SCALE = 12;
const ONE = 10n ** BigInt(SCALE);

/** Fixed-point decimal value: mantissa scaled by 10^12. */
export class Dec {
  readonly m: bigint;

  private constructor(mantissa: bigint) {
    this.m = mantissa;
  }

  static readonly zero = new Dec(0n);

  static fromParts(mantissa: bigint): Dec {
    return new Dec(mantissa);
  }

  static fromString(text: string): Dec {
    const match = /^(-?)(\d+)(?:\.(\d+))?$/.exec(text.trim());
    if (!match) {
      throw new CalculationError(`Invalid number '${text}'.`);
    }

    const [, sign, whole, fraction = ''] = match;
    const digits = fraction.length > SCALE ? fraction.slice(0, SCALE) : fraction.padEnd(SCALE, '0');
    let mantissa = BigInt(whole) * ONE + BigInt(digits === '' ? 0 : digits);
    if (fraction.length > SCALE && Number(fraction[SCALE]) >= 5) {
      mantissa += 1n;
    }

    return new Dec(sign === '-' ? -mantissa : mantissa);
  }

  static fromNumber(value: number): Dec {
    if (!Number.isFinite(value)) {
      throw new CalculationError(`Cannot convert ${value} to a decimal.`);
    }

    return Dec.fromString(value.toFixed(SCALE));
  }

  toNumber(): number {
    return Number(this.m) / Number(ONE);
  }

  toString(): string {
    const negative = this.m < 0n;
    const abs = negative ? -this.m : this.m;
    const whole = abs / ONE;
    const fraction = (abs % ONE).toString().padStart(SCALE, '0').replace(/0+$/, '');
    return `${negative ? '-' : ''}${whole}${fraction ? '.' + fraction : ''}`;
  }

  add(other: Dec): Dec {
    return new Dec(this.m + other.m);
  }

  sub(other: Dec): Dec {
    return new Dec(this.m - other.m);
  }

  mul(other: Dec): Dec {
    return new Dec(divRoundHalfAway(this.m * other.m, ONE));
  }

  div(other: Dec): Dec {
    if (other.m === 0n) {
      throw new CalculationError('Division by zero.');
    }

    return new Dec(divRoundHalfAway(this.m * ONE, other.m));
  }

  neg(): Dec {
    return new Dec(-this.m);
  }

  abs(): Dec {
    return this.m < 0n ? this.neg() : this;
  }

  floor(): Dec {
    const remainder = ((this.m % ONE) + ONE) % ONE;
    return new Dec(this.m - remainder);
  }

  round(places: number): Dec {
    const factor = 10n ** BigInt(SCALE - places);
    return new Dec(divRoundHalfAway(this.m, factor) * factor);
  }

  cmp(other: Dec): number {
    return this.m < other.m ? -1 : this.m > other.m ? 1 : 0;
  }

  eq(other: Dec): boolean {
    return this.m === other.m;
  }
}

function divRoundHalfAway(numerator: bigint, denominator: bigint): bigint {
  const negative = numerator < 0n !== denominator < 0n;
  const n = numerator < 0n ? -numerator : numerator;
  const d = denominator < 0n ? -denominator : denominator;
  const quotient = n / d;
  const remainder = n % d;
  const rounded = remainder * 2n >= d ? quotient + 1n : quotient;
  return negative ? -rounded : rounded;
}

export type CalcValue = Dec | boolean | string;
export type CalcScope = Record<string, unknown>;

export interface CalculationTable {
  interpolate?: string;
  values: Record<string, number>;
}

export interface CalculationField {
  expr?: string;
  source?: string;
  /** Display format applied where the field's value is shown ("gbp"). */
  format?: string;
}

export interface CalculationSeries {
  over: string;
  from: string;
  to: string;
  values: Record<string, string>;
}

export interface CalculationSet {
  tables?: Record<string, CalculationTable>;
  fields: Record<string, CalculationField>;
  series?: Record<string, CalculationSeries>;
}

export interface CalculationOutput {
  fields: Record<string, CalcValue>;
  series: Record<string, Array<Record<string, CalcValue>>>;
}

// ─── Parser ────────────────────────────────────────────────────────────────────

type Node_ =
  | { kind: 'number'; value: Dec }
  | { kind: 'text'; value: string }
  | { kind: 'bool'; value: boolean }
  | { kind: 'identifier'; path: string }
  | { kind: 'unary'; op: string; operand: Node_ }
  | { kind: 'binary'; op: string; left: Node_; right: Node_ }
  | { kind: 'call'; name: string; args: Node_[] };

interface Token {
  kind: 'number' | 'identifier' | 'string' | 'op';
  value: string;
  position: number;
}

export function parseExpression(expression: string): unknown {
  return parse(expression);
}

/**
 * Evaluates a single standalone expression (e.g. a component's showWhen) against a
 * scope that already contains inputs and calculated fields. Tables from `set` are
 * available to lookup(). Mirrors CalculationEvaluator.EvaluateExpression in C#.
 */
export function evaluateExpression(expression: string, scope: CalcScope, set?: CalculationSet): CalcValue {
  return evaluateNode(parse(expression), scope, set ?? { fields: {} }, `expression '${expression}'`);
}

function parse(expression: string): Node_ {
  if (!expression || !expression.trim()) {
    throw new CalculationError('Expression is empty.');
  }

  const tokens = tokenize(expression);
  const state = { tokens, index: 0 };
  const node = parseOr(state);
  if (state.index < tokens.length) {
    throw new CalculationError(
      `Unexpected '${tokens[state.index].value}' at position ${tokens[state.index].position} in: ${expression}`,
    );
  }

  return node;
}

function tokenize(text: string): Token[] {
  const tokens: Token[] = [];
  let i = 0;
  while (i < text.length) {
    const c = text[i];
    if (/\s/.test(c)) {
      i++;
      continue;
    }

    if (/[0-9]/.test(c)) {
      const start = i;
      while (i < text.length && /[0-9.]/.test(text[i])) i++;
      tokens.push({ kind: 'number', value: text.slice(start, i), position: start });
      continue;
    }

    if (/[A-Za-z_]/.test(c)) {
      const start = i;
      while (i < text.length && /[A-Za-z0-9_.]/.test(text[i])) i++;
      tokens.push({ kind: 'identifier', value: text.slice(start, i), position: start });
      continue;
    }

    if (c === "'") {
      const start = ++i;
      while (i < text.length && text[i] !== "'") i++;
      if (i >= text.length) {
        throw new CalculationError(`Unterminated string starting at position ${start - 1}.`);
      }

      tokens.push({ kind: 'string', value: text.slice(start, i), position: start - 1 });
      i++;
      continue;
    }

    if (c === '<' && (text[i + 1] === '=' || text[i + 1] === '>')) {
      tokens.push({ kind: 'op', value: text.slice(i, i + 2), position: i });
      i += 2;
      continue;
    }

    if (c === '>' && text[i + 1] === '=') {
      tokens.push({ kind: 'op', value: '>=', position: i });
      i += 2;
      continue;
    }

    if ('+-*/()=<>,'.includes(c)) {
      tokens.push({ kind: 'op', value: c, position: i });
      i++;
      continue;
    }

    throw new CalculationError(`Unexpected character '${c}' at position ${i}.`);
  }

  return tokens;
}

interface ParseState {
  tokens: Token[];
  index: number;
}

function peekIdentifier(state: ParseState): string | null {
  const token = state.tokens[state.index];
  return token && token.kind === 'identifier' ? token.value : null;
}

function peekOp(state: ParseState, ...values: string[]): string | null {
  const token = state.tokens[state.index];
  return token && token.kind === 'op' && values.includes(token.value) ? token.value : null;
}

function parseOr(state: ParseState): Node_ {
  let left = parseAnd(state);
  while (peekIdentifier(state) === 'or') {
    state.index++;
    left = { kind: 'binary', op: 'or', left, right: parseAnd(state) };
  }

  return left;
}

function parseAnd(state: ParseState): Node_ {
  let left = parseNot(state);
  while (peekIdentifier(state) === 'and') {
    state.index++;
    left = { kind: 'binary', op: 'and', left, right: parseNot(state) };
  }

  return left;
}

function parseNot(state: ParseState): Node_ {
  if (peekIdentifier(state) === 'not') {
    state.index++;
    return { kind: 'unary', op: 'not', operand: parseNot(state) };
  }

  return parseComparison(state);
}

function parseComparison(state: ParseState): Node_ {
  const left = parseAdditive(state);
  const op = peekOp(state, '=', '<>', '<', '<=', '>', '>=');
  if (op) {
    state.index++;
    return { kind: 'binary', op, left, right: parseAdditive(state) };
  }

  return left;
}

function parseAdditive(state: ParseState): Node_ {
  let left = parseMultiplicative(state);
  for (let op = peekOp(state, '+', '-'); op; op = peekOp(state, '+', '-')) {
    state.index++;
    left = { kind: 'binary', op, left, right: parseMultiplicative(state) };
  }

  return left;
}

function parseMultiplicative(state: ParseState): Node_ {
  let left = parseUnary(state);
  for (let op = peekOp(state, '*', '/'); op; op = peekOp(state, '*', '/')) {
    state.index++;
    left = { kind: 'binary', op, left, right: parseUnary(state) };
  }

  return left;
}

function parseUnary(state: ParseState): Node_ {
  if (peekOp(state, '-')) {
    state.index++;
    return { kind: 'unary', op: '-', operand: parseUnary(state) };
  }

  return parsePrimary(state);
}

function parsePrimary(state: ParseState): Node_ {
  const token = state.tokens[state.index];
  if (!token) {
    throw new CalculationError('Unexpected end of expression.');
  }

  if (token.kind === 'number') {
    state.index++;
    return { kind: 'number', value: Dec.fromString(token.value) };
  }

  if (token.kind === 'string') {
    state.index++;
    return { kind: 'text', value: token.value };
  }

  if (token.kind === 'identifier') {
    if (token.value === 'true' || token.value === 'false') {
      state.index++;
      return { kind: 'bool', value: token.value === 'true' };
    }

    const next = state.tokens[state.index + 1];
    if (next && next.kind === 'op' && next.value === '(') {
      if (token.value.includes('.')) {
        throw new CalculationError(`'${token.value}' is not a valid function name.`);
      }

      state.index += 2;
      const args: Node_[] = [];
      if (!peekOp(state, ')')) {
        for (;;) {
          args.push(parseOr(state));
          if (peekOp(state, ',')) {
            state.index++;
            continue;
          }

          break;
        }
      }

      if (!peekOp(state, ')')) {
        throw new CalculationError(`Missing ')' for function '${token.value}'.`);
      }

      state.index++;
      return { kind: 'call', name: token.value, args };
    }

    state.index++;
    return { kind: 'identifier', path: token.value };
  }

  if (token.kind === 'op' && token.value === '(') {
    state.index++;
    const inner = parseOr(state);
    if (!peekOp(state, ')')) {
      throw new CalculationError("Missing closing ')'.");
    }

    state.index++;
    return inner;
  }

  throw new CalculationError(`Unexpected '${token.value}' at position ${token.position}.`);
}

// ─── Evaluator ─────────────────────────────────────────────────────────────────

const MAX_SERIES_ROWS = 1000;

/** Converts host-supplied scope values (plain JSON) into evaluator values. */
export function toScope(inputs: Record<string, unknown>): CalcScope {
  const scope: CalcScope = {};
  for (const [key, value] of Object.entries(inputs)) {
    scope[key] = toScopeValue(value);
  }

  return scope;
}

function toScopeValue(value: unknown): unknown {
  if (typeof value === 'number') return Dec.fromNumber(value);
  if (value !== null && typeof value === 'object' && !(value instanceof Dec)) {
    return toScope(value as Record<string, unknown>);
  }

  return value;
}

export function evaluateCalculations(set: CalculationSet, inputs: CalcScope): CalculationOutput {
  const scope: CalcScope = { ...inputs };
  const fields: Record<string, CalcValue> = {};

  for (const [name, field] of Object.entries(set.fields ?? {})) {
    if ((field.source ?? '').toLowerCase() === 'service') {
      if (!(name in scope)) {
        throw new CalculationError(`Field '${name}' is service-sourced but the host did not supply it.`);
      }

      continue;
    }

    if (!field.expr || !field.expr.trim()) {
      throw new CalculationError(`Field '${name}' has no expression and no service source.`);
    }

    if (name in scope) {
      throw new CalculationError(`Field '${name}' collides with an input or earlier field.`);
    }

    const value = evaluateNode(parse(field.expr), scope, set, name);
    scope[name] = value;
    fields[name] = value;
  }

  const series: Record<string, Array<Record<string, CalcValue>>> = {};
  for (const [name, definition] of Object.entries(set.series ?? {})) {
    series[name] = evaluateSeries(name, definition, scope, set);
  }

  return { fields, series };
}

function evaluateSeries(
  seriesName: string,
  definition: CalculationSeries,
  scope: CalcScope,
  set: CalculationSet,
): Array<Record<string, CalcValue>> {
  if (!definition.over || !definition.over.trim()) {
    throw new CalculationError(`Series '${seriesName}' has no loop variable ('over').`);
  }

  if (definition.over in scope) {
    throw new CalculationError(
      `Series '${seriesName}' loop variable '${definition.over}' collides with an existing name.`,
    );
  }

  const from = toInteger(evaluateNode(parse(definition.from), scope, set, seriesName), `series '${seriesName}' 'from'`);
  const to = toInteger(evaluateNode(parse(definition.to), scope, set, seriesName), `series '${seriesName}' 'to'`);

  if (to - from + 1 > MAX_SERIES_ROWS) {
    throw new CalculationError(
      `Series '${seriesName}' would produce ${to - from + 1} rows; the limit is ${MAX_SERIES_ROWS}.`,
    );
  }

  const parsedValues = Object.entries(definition.values).map(
    ([column, expr]) => [column, parse(expr)] as const,
  );

  const rows: Array<Record<string, CalcValue>> = [];
  const rowScope: CalcScope = { ...scope };
  for (let step = from; step <= to; step++) {
    const loopValue = Dec.fromNumber(step);
    rowScope[definition.over] = loopValue;
    const row: Record<string, CalcValue> = { [definition.over]: loopValue };
    for (const [column, node] of parsedValues) {
      row[column] = evaluateNode(node, rowScope, set, `${seriesName}.${column}`);
    }

    rows.push(row);
  }

  return rows;
}

function evaluateNode(node: Node_, scope: CalcScope, set: CalculationSet, context: string): CalcValue {
  switch (node.kind) {
    case 'number':
    case 'text':
    case 'bool':
      return node.value;

    case 'identifier':
      return resolvePath(node.path, scope, context);

    case 'unary':
      return node.op === '-'
        ? toDec(evaluateNode(node.operand, scope, set, context), context).neg()
        : !toBool(evaluateNode(node.operand, scope, set, context), context);

    case 'binary':
      return evaluateBinary(node, scope, set, context);

    case 'call':
      return evaluateCall(node, scope, set, context);
  }
}

function evaluateBinary(
  node: Extract<Node_, { kind: 'binary' }>,
  scope: CalcScope,
  set: CalculationSet,
  context: string,
): CalcValue {
  if (node.op === 'and' || node.op === 'or') {
    const left = toBool(evaluateNode(node.left, scope, set, context), context);
    if (node.op === 'and' && !left) return false;
    if (node.op === 'or' && left) return true;
    return toBool(evaluateNode(node.right, scope, set, context), context);
  }

  const left = evaluateNode(node.left, scope, set, context);
  const right = evaluateNode(node.right, scope, set, context);

  switch (node.op) {
    case '=':
      return valuesEqual(left, right);
    case '<>':
      return !valuesEqual(left, right);
    case '<':
      return toDec(left, context).cmp(toDec(right, context)) < 0;
    case '<=':
      return toDec(left, context).cmp(toDec(right, context)) <= 0;
    case '>':
      return toDec(left, context).cmp(toDec(right, context)) > 0;
    case '>=':
      return toDec(left, context).cmp(toDec(right, context)) >= 0;
    case '+':
      return toDec(left, context).add(toDec(right, context));
    case '-':
      return toDec(left, context).sub(toDec(right, context));
    case '*':
      return toDec(left, context).mul(toDec(right, context));
    case '/': {
      const divisor = toDec(right, context);
      if (divisor.eq(Dec.zero)) {
        throw new CalculationError(`Division by zero in ${context}.`);
      }

      return toDec(left, context).div(divisor);
    }
    default:
      throw new CalculationError(`Unknown operator '${node.op}' in ${context}.`);
  }
}

function evaluateCall(
  node: Extract<Node_, { kind: 'call' }>,
  scope: CalcScope,
  set: CalculationSet,
  context: string,
): CalcValue {
  const arg = (i: number) => evaluateNode(node.args[i], scope, set, context);
  const num = (i: number) => toDec(arg(i), context);
  const requireArgs = (count: number) => {
    if (node.args.length !== count) {
      throw new CalculationError(`${node.name}() expects ${count} argument(s), got ${node.args.length}, in ${context}.`);
    }
  };

  switch (node.name) {
    case 'if':
      requireArgs(3);
      return toBool(arg(0), context) ? arg(1) : arg(2);

    case 'min':
    case 'max': {
      if (node.args.length < 2) {
        throw new CalculationError(`${node.name}() expects at least 2 arguments in ${context}.`);
      }

      let result = num(0);
      for (let i = 1; i < node.args.length; i++) {
        const next = num(i);
        const takeNext = node.name === 'min' ? next.cmp(result) < 0 : next.cmp(result) > 0;
        if (takeNext) result = next;
      }

      return result;
    }

    case 'clamp': {
      requireArgs(3);
      const value = num(0);
      const low = num(1);
      const high = num(2);
      return value.cmp(low) < 0 ? low : value.cmp(high) > 0 ? high : value;
    }

    case 'abs':
      requireArgs(1);
      return num(0).abs();

    case 'floor':
      requireArgs(1);
      return num(0).floor();

    case 'round': {
      if (node.args.length !== 1 && node.args.length !== 2) {
        throw new CalculationError(`round() expects 1 or 2 arguments in ${context}.`);
      }

      const places = node.args.length === 2 ? Number(num(1).toString()) : 0;
      return num(0).round(places);
    }

    case 'pow':
      requireArgs(2);
      return Dec.fromNumber(Math.pow(num(0).toNumber(), num(1).toNumber()));

    case 'lookup': {
      requireArgs(2);
      const tableRef = node.args[0];
      if (tableRef.kind !== 'identifier') {
        throw new CalculationError(`lookup() requires a table name as its first argument in ${context}.`);
      }

      return lookup(tableRef.path, num(1), set, context);
    }

    default:
      throw new CalculationError(`Unknown function '${node.name}' in ${context}.`);
  }
}

function lookup(tableName: string, key: Dec, set: CalculationSet, context: string): Dec {
  const table = set.tables?.[tableName];
  if (!table) {
    throw new CalculationError(`Unknown table '${tableName}' in ${context}.`);
  }

  const points = Object.entries(table.values)
    .map(([k, v]) => ({ key: Dec.fromString(k), value: Dec.fromNumber(v) }))
    .sort((a, b) => a.key.cmp(b.key));

  if (points.length === 0) {
    throw new CalculationError(`Table '${tableName}' is empty, in ${context}.`);
  }

  if (key.cmp(points[0].key) <= 0) return points[0].value;
  if (key.cmp(points[points.length - 1].key) >= 0) return points[points.length - 1].value;

  for (let i = 1; i < points.length; i++) {
    if (key.cmp(points[i].key) > 0) continue;

    const low = points[i - 1];
    const high = points[i];

    if ((table.interpolate ?? '').toLowerCase() === 'step') {
      return key.eq(high.key) ? high.value : low.value;
    }

    return low.value.add(
      high.value.sub(low.value).mul(key.sub(low.key)).div(high.key.sub(low.key)),
    );
  }

  return points[points.length - 1].value;
}

function resolvePath(path: string, scope: CalcScope, context: string): CalcValue {
  const segments = path.split('.');
  let current: unknown = scope;
  for (const segment of segments) {
    if (current !== null && typeof current === 'object' && !(current instanceof Dec)) {
      const map = current as Record<string, unknown>;
      if (!(segment in map)) {
        throw new CalculationError(`Unknown name '${path}' in ${context}.`);
      }

      current = map[segment];
      continue;
    }

    throw new CalculationError(`'${path}' cannot be resolved ('${segment}' is not a group) in ${context}.`);
  }

  return current as CalcValue;
}

function valuesEqual(left: CalcValue, right: CalcValue): boolean {
  if (left instanceof Dec || right instanceof Dec) {
    return left instanceof Dec && right instanceof Dec && left.eq(right);
  }

  return left === right;
}

function toDec(value: CalcValue, context: string): Dec {
  if (value instanceof Dec) return value;
  throw new CalculationError(
    `Expected a number but got ${value === null || value === undefined ? 'nothing' : `'${value}'`} in ${context}.`,
  );
}

function toBool(value: CalcValue, context: string): boolean {
  if (typeof value === 'boolean') return value;
  throw new CalculationError(
    `Expected true/false but got ${value === null || value === undefined ? 'nothing' : `'${value}'`} in ${context}.`,
  );
}

function toInteger(value: CalcValue, context: string): number {
  const dec = toDec(value, context);
  if (!dec.eq(dec.floor())) {
    throw new CalculationError(`Expected a whole number in ${context}.`);
  }

  return dec.toNumber();
}
