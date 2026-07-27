// Round-trips every service blueprint seed through the editor's two independent load paths —
// the Canvas path (hydrateServiceBlueprintDefinition → serializeAuthoredServiceBlueprint) and the
// Definition tab path (coerceParsedAuthoredServiceBlueprint → serializeAuthoredServiceBlueprint,
// simulating a user pasting/editing the raw JSON and applying it) — and fails if
// anything the runtime relies on is dropped: the calculations block, layout, route
// labels/styles, and component properties (showWhen, default, chart bindings).
//
// The two paths are NOT the same code and have drifted before: coerceParsedAuthoredServiceBlueprint
// once silently omitted "calculations" and "layout" entirely, so any Definition-tab edit —
// however unrelated (e.g. bumping a file-upload's maxSizeBytes) — wiped the service blueprint's whole
// calculations block on save, breaking every showWhen/stat-group binding that depended on it.
// Reproduced live editing transfer-a-juggling-licence.json's Definition tab.
//
// Also asserts the wire format System.Text.Json's polymorphic PrismComponent deserializer
// requires: the "type" discriminator must be the first key in the JSON object, or the server
// rejects the save outright with no other diagnostic ("must specify a type discriminator") —
// reproduced live saving transfer-a-juggling-licence.json with zero edits, since
// serializeAuthoredServiceBlueprint alphabetically sorts every object's keys and "type" rarely sorts
// first.
//
// Requires Node >= 23.6 (built-in TypeScript type stripping).
import { readFileSync, readdirSync } from 'fs';
import { fileURLToPath } from 'url';
import { dirname, join } from 'path';
import { hydrateServiceBlueprintDefinition } from '../src/service-blueprint-editor/types.ts';
import { coerceParsedAuthoredServiceBlueprint } from '../src/service-blueprint-editor/service-blueprint-lint.ts';
import { serializeAuthoredServiceBlueprint } from '../src/service-blueprint-editor/service-blueprint-canonical-json.ts';

const here = dirname(fileURLToPath(import.meta.url));
const seedDirs = [
  join(here, '..', '..', 'UmbracoPrism.MockBusinessApp', 'service-blueprints'),
  join(here, '..', '..', 'UmbracoPrism.TestSite', 'cms-service-blueprints'),
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

  const originalStages = original.stages ?? [];
  const roundTrippedStages = roundTripped.stages ?? [];
  if (originalStages.length === 0) {
    fail(file, pathLabel, `seed has no "stages" array — round-trip check has nothing to verify (check the seed's own shape)`);
    return;
  }
  for (const stage of originalStages) {
    const stageKey = stage.stageKey;
    const rtStage = roundTrippedStages.find((s) => s.stageKey === stageKey);
    if (!rtStage) {
      fail(file, pathLabel, `stage '${stageKey}' missing after round-trip`);
      continue;
    }

    for (const route of stage.routes ?? []) {
      const rtRoute = (rtStage.routes ?? []).find((r) => r.id === route.id);
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
    if (JSON.stringify(sortDeep(rtStage.components ?? [])) !== JSON.stringify(sortDeep(stage.components ?? []))) {
      fail(file, pathLabel, `components of stage '${stageKey}' were altered by the round-trip`);
    }

    // Every PrismComponent must serialize with "type" as its first key, or the
    // backoffice save PUT gets rejected with a 400 the editor can't explain.
    (rtStage.components ?? []).forEach((component, index) => {
      if (component && typeof component === 'object' && 'type' in component) {
        const firstKey = Object.keys(component)[0];
        if (firstKey !== 'type') {
          fail(file, pathLabel, `stage '${stageKey}' component[${index}] (type: '${component.type}') ` +
            `serializes with '${firstKey}' before 'type' — System.Text.Json will reject this on save`);
        }
      }
    });
  }
}

for (const seedDir of seedDirs) {
  for (const file of readdirSync(seedDir).filter((f) => f.endsWith('.json'))) {
    const original = JSON.parse(readFileSync(join(seedDir, file), 'utf8'));

    const hydrated = hydrateServiceBlueprintDefinition(JSON.parse(JSON.stringify(original)));
    checkRoundTrip(file, 'canvas', original, JSON.parse(serializeAuthoredServiceBlueprint(hydrated)));

    const coerced = coerceParsedAuthoredServiceBlueprint(JSON.parse(JSON.stringify(original)));
    checkRoundTrip(file, 'definition-tab', original, JSON.parse(serializeAuthoredServiceBlueprint(coerced)));
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

console.log('All service blueprint seeds survive both the Canvas and Definition-tab load → serialize round-trips.');
