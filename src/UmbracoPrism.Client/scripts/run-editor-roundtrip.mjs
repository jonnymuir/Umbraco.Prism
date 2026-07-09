// Round-trips every workflow seed through the editor's exact load → serialize path
// (hydrateWorkflowDefinition → serializeAuthoredWorkflow) and fails if anything the
// runtime relies on is dropped: the calculations block, route labels/styles, and
// component properties (showWhen, default, chart bindings).
//
// Requires Node >= 23.6 (built-in TypeScript type stripping).
import { readFileSync, readdirSync } from 'fs';
import { fileURLToPath } from 'url';
import { dirname, join } from 'path';
import { hydrateWorkflowDefinition } from '../src/workflow-editor/types.ts';
import { serializeAuthoredWorkflow } from '../src/workflow-editor/workflow-canonical-json.ts';

const here = dirname(fileURLToPath(import.meta.url));
const seedDir = join(here, '..', '..', 'UmbracoPrism.MockBusinessApp', 'workflow-seeds');

let failures = 0;
const fail = (seed, message) => {
  failures++;
  console.error(`FAIL ${seed} — ${message}`);
};

for (const file of readdirSync(seedDir).filter((f) => f.endsWith('.json'))) {
  const original = JSON.parse(readFileSync(join(seedDir, file), 'utf8'));
  const hydrated = hydrateWorkflowDefinition(JSON.parse(JSON.stringify(original)));
  const roundTripped = JSON.parse(serializeAuthoredWorkflow(hydrated));

  if (original.calculations) {
    if (!roundTripped.calculations) {
      fail(file, 'calculations block was dropped by the editor round-trip');
    } else if (JSON.stringify(sortDeep(roundTripped.calculations)) !== JSON.stringify(sortDeep(original.calculations))) {
      fail(file, 'calculations block was altered by the editor round-trip');
    }
  }

  for (const state of original.states ?? []) {
    const rtState = (roundTripped.states ?? []).find((s) => s.stateKey === state.stateKey);
    if (!rtState) {
      fail(file, `state '${state.stateKey}' missing after round-trip`);
      continue;
    }

    for (const route of state.routes ?? []) {
      const rtRoute = (rtState.routes ?? []).find((r) => r.id === route.id);
      if (!rtRoute) {
        fail(file, `route '${route.id}' missing after round-trip`);
        continue;
      }
      for (const prop of ['label', 'style']) {
        if (route[prop] !== undefined && rtRoute[prop] !== route[prop]) {
          fail(file, `route '${route.id}' lost '${prop}' (${route[prop]} → ${rtRoute[prop]})`);
        }
      }
    }

    // Components must survive byte-identical — the editor passes them through raw.
    if (JSON.stringify(sortDeep(rtState.components ?? [])) !== JSON.stringify(sortDeep(state.components ?? []))) {
      fail(file, `components of state '${state.stateKey}' were altered by the round-trip`);
    }
  }
}

function sortDeep(value) {
  if (Array.isArray(value)) return value.map(sortDeep);
  if (value && typeof value === 'object') {
    return Object.fromEntries(Object.keys(value).sort().map((k) => [k, sortDeep(value[k])]));
  }
  return value;
}

if (failures > 0) {
  console.error(`\n${failures} editor round-trip failure(s).`);
  process.exit(1);
}

console.log('All workflow seeds survive the editor load → serialize round-trip.');
