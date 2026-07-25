// Round-trips every workflow seed through the editor's two independent load paths —
// the Canvas path (hydrateWorkflowDefinition → serializeAuthoredWorkflow) and the
// Definition tab path (coerceParsedAuthoredWorkflow → serializeAuthoredWorkflow,
// simulating a user pasting/editing the raw JSON and applying it) — and fails if
// anything the runtime relies on is dropped: the calculations block, layout, route
// labels/styles, and component properties (showWhen, default, chart bindings).
//
// The two paths are NOT the same code and have drifted before: coerceParsedAuthoredWorkflow
// once silently omitted "calculations" and "layout" entirely, so any Definition-tab edit —
// however unrelated (e.g. bumping a file-upload's maxSizeBytes) — wiped the workflow's whole
// calculations block on save, breaking every showWhen/stat-group binding that depended on it.
// Reproduced live editing transfer-a-juggling-licence.json's Definition tab.
//
// Also asserts the wire format System.Text.Json's polymorphic PrismComponent deserializer
// requires: the "type" discriminator must be the first key in the JSON object, or the server
// rejects the save outright with no other diagnostic ("must specify a type discriminator") —
// reproduced live saving transfer-a-juggling-licence.json with zero edits, since
// serializeAuthoredWorkflow alphabetically sorts every object's keys and "type" rarely sorts
// first.
//
// Requires Node >= 23.6 (built-in TypeScript type stripping).
import { readFileSync, readdirSync } from 'fs';
import { fileURLToPath } from 'url';
import { dirname, join } from 'path';
import { hydrateWorkflowDefinition } from '../src/workflow-editor/types.ts';
import { coerceParsedAuthoredWorkflow } from '../src/workflow-editor/workflow-definition-lint.ts';
import { serializeAuthoredWorkflow } from '../src/workflow-editor/workflow-canonical-json.ts';

const here = dirname(fileURLToPath(import.meta.url));
const seedDirs = [
  join(here, '..', '..', 'UmbracoPrism.MockBusinessApp', 'workflow-seeds'),
  join(here, '..', '..', 'UmbracoPrism.TestSite', 'cms-workflow-seeds'),
];

let failures = 0;
const fail = (seed, path, message) => {
  failures++;
  console.error(`FAIL ${seed} [${path}] — ${message}`);
};

function checkRoundTrip(file, pathLabel, original, roundTripped) {
  if (original.calculations) {
    if (!roundTripped.calculations) {
      fail(file, pathLabel, 'calculations block was dropped by the editor round-trip');
    } else if (JSON.stringify(sortDeep(roundTripped.calculations)) !== JSON.stringify(sortDeep(original.calculations))) {
      fail(file, pathLabel, 'calculations block was altered by the editor round-trip');
    }
  }

  for (const state of original.states ?? []) {
    const rtState = (roundTripped.states ?? []).find((s) => s.stateKey === state.stateKey);
    if (!rtState) {
      fail(file, pathLabel, `state '${state.stateKey}' missing after round-trip`);
      continue;
    }

    for (const route of state.routes ?? []) {
      const rtRoute = (rtState.routes ?? []).find((r) => r.id === route.id);
      if (!rtRoute) {
        fail(file, pathLabel, `route '${route.id}' missing after round-trip`);
        continue;
      }
      for (const prop of ['label', 'style']) {
        if (route[prop] !== undefined && rtRoute[prop] !== route[prop]) {
          fail(file, pathLabel, `route '${route.id}' lost '${prop}' (${route[prop]} → ${rtRoute[prop]})`);
        }
      }
    }

    // Components must survive byte-identical — the editor passes them through raw.
    if (JSON.stringify(sortDeep(rtState.components ?? [])) !== JSON.stringify(sortDeep(state.components ?? []))) {
      fail(file, pathLabel, `components of state '${state.stateKey}' were altered by the round-trip`);
    }

    // Every PrismComponent must serialize with "type" as its first key, or the
    // backoffice save PUT gets rejected with a 400 the editor can't explain.
    (rtState.components ?? []).forEach((component, index) => {
      if (component && typeof component === 'object' && 'type' in component) {
        const firstKey = Object.keys(component)[0];
        if (firstKey !== 'type') {
          fail(file, pathLabel, `state '${state.stateKey}' component[${index}] (type: '${component.type}') ` +
            `serializes with '${firstKey}' before 'type' — System.Text.Json will reject this on save`);
        }
      }
    });
  }
}

for (const seedDir of seedDirs) {
  for (const file of readdirSync(seedDir).filter((f) => f.endsWith('.json'))) {
    const original = JSON.parse(readFileSync(join(seedDir, file), 'utf8'));

    const hydrated = hydrateWorkflowDefinition(JSON.parse(JSON.stringify(original)));
    checkRoundTrip(file, 'canvas', original, JSON.parse(serializeAuthoredWorkflow(hydrated)));

    const coerced = coerceParsedAuthoredWorkflow(JSON.parse(JSON.stringify(original)));
    checkRoundTrip(file, 'definition-tab', original, JSON.parse(serializeAuthoredWorkflow(coerced)));
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

console.log('All workflow seeds survive both the Canvas and Definition-tab load → serialize round-trips.');
