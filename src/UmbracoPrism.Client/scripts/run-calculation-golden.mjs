// Runs the shared calculation-language conformance fixtures against the TypeScript
// evaluator. The C# evaluator runs the identical file (CalculationGoldenTests) — a
// divergence between runtimes must fail one of the two suites.
//
// Requires Node >= 23.6 (built-in TypeScript type stripping).
import { readFileSync } from 'fs';
import { fileURLToPath } from 'url';
import { dirname, join } from 'path';
import {
  Dec,
  CalculationError,
  evaluateCalculations,
  toScope,
} from '../src/calculations/calculation-engine.ts';

const here = dirname(fileURLToPath(import.meta.url));
const fixturePath = join(
  here,
  '..', '..', 'UmbracoPrism.Shared', 'calculation-fixtures', 'calculation-golden.json',
);
const { cases } = JSON.parse(readFileSync(fixturePath, 'utf8'));

let failures = 0;

for (const testCase of cases) {
  const name = testCase.name;
  try {
    const set = testCase.expr
      ? { tables: testCase.tables, fields: { result: { expr: testCase.expr } } }
      : { tables: testCase.tables, fields: testCase.fields ?? {}, series: testCase.series };
    const inputs = toScope(testCase.inputs ?? {});

    if (testCase.expectError) {
      try {
        evaluateCalculations(set, inputs);
        fail(name, 'expected an error but evaluation succeeded');
      } catch (error) {
        if (!(error instanceof CalculationError)) throw error;
      }
      continue;
    }

    const result = evaluateCalculations(set, inputs);

    if ('expect' in testCase) {
      assertValue(name, 'result', result.fields.result, testCase.expect);
    }

    for (const [field, expected] of Object.entries(testCase.expectFields ?? {})) {
      assertValue(name, field, result.fields[field], expected);
    }

    for (const [seriesName, expectedRows] of Object.entries(testCase.expectSeries ?? {})) {
      const rows = result.series[seriesName] ?? [];
      if (rows.length !== expectedRows.length) {
        fail(name, `series '${seriesName}' expected ${expectedRows.length} rows, got ${rows.length}`);
        continue;
      }

      expectedRows.forEach((expectedRow, i) => {
        for (const [column, expected] of Object.entries(expectedRow)) {
          assertValue(name, `${seriesName}[${i}].${column}`, rows[i][column], expected);
        }
      });
    }
  } catch (error) {
    fail(name, String(error));
  }
}

function assertValue(caseName, label, actual, expected) {
  if (typeof expected === 'boolean') {
    if (actual !== expected) {
      fail(caseName, `${label}: expected ${expected}, got ${actual}`);
    }
    return;
  }

  // Numbers are asserted as invariant strings compared by value (1.0 equals 1).
  if (actual instanceof Dec) {
    if (!Dec.fromString(String(expected)).eq(actual)) {
      fail(caseName, `${label}: expected ${expected}, got ${actual.toString()}`);
    }
    return;
  }

  if (actual !== expected) {
    fail(caseName, `${label}: expected '${expected}', got '${actual}'`);
  }
}

function fail(caseName, message) {
  failures++;
  console.error(`FAIL ${caseName} — ${message}`);
}

if (failures > 0) {
  console.error(`\n${failures} golden case(s) failed out of ${cases.length}.`);
  process.exit(1);
}

console.log(`All ${cases.length} calculation golden cases passed (TypeScript evaluator).`);
