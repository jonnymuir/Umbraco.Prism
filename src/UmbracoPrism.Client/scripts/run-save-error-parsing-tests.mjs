// Regression coverage for buildSaveErrorFromPayload (cms-workflow-source.ts): the backoffice
// PUT save can fail three structurally different ways — a real business-validation failure
// (WorkflowSaveOutcome, status: "Invalid"), a version conflict (WorkflowSaveOutcome, status:
// "Conflict", HTTP 409), or a framework-level 400 (ASP.NET ValidationProblemDetails, e.g. a
// JSON deserialization failure). Before this fix, a plain 400 always fell into the
// ProblemDetails parser, which only reads `.errors`/`.extensions.errors` — a real
// WorkflowSaveOutcome's `.diagnostics` array (containing every "why this can't save" message)
// was silently dropped, and the editor showed only a generic "backoffice rejected the request"
// line. The actual diagnostics were visible only via browser devtools. Reproduced live saving
// transfer-a-juggling-licence.json after a Definition-tab edit broke a stat-group binding.
//
// Requires Node >= 23.6 (built-in TypeScript type stripping).
import { buildSaveErrorFromPayload } from '../src/backoffice/cms-workflow-source.ts';

let failures = 0;
const assert = (label, condition) => {
  if (!condition) {
    failures++;
    console.error(`FAIL — ${label}`);
  }
};

const workflow = {
  definitionKey: 'transfer-a-juggling-licence',
  displayName: 'Transfer a Professional Juggling Licence',
  version: 4,
  initialState: 'eligibility-professional',
  instancePolicy: 'single',
  states: [
    { stateKey: 'licence-details', displayName: 'Your existing licence', components: [], routes: [] },
  ],
};

// A real WorkflowSaveOutcome "Invalid" response (the actual shape WorkflowAuthoringService
// returns) must surface every diagnostic message, not just a generic summary.
{
  const payload = JSON.stringify({
    status: 'Invalid',
    diagnostics: [
      {
        code: 'DATA_DISPLAY_UNKNOWN_FIELD',
        path: 'states.licence-details.components[0].items[0].fieldKey',
        message:
          "stat-group item 'Membership tier' binds to field 'membershipTier', which is neither a captured input field nor a calculations.fields entry.",
        severity: 'Error',
      },
      {
        code: 'SHOW_WHEN_EVAL_ERROR',
        path: 'states.licence-details.components[0].showWhen',
        message: "Unknown name 'isMember' in expression 'isMember'.",
        severity: 'Error',
      },
    ],
    currentVersion: null,
    newVersion: null,
    isSaved: false,
  });

  const error = buildSaveErrorFromPayload(payload, 400, 'Bad Request', 'application/json', workflow.definitionKey, workflow);

  // The first diagnostic becomes the headline `summary` (matching the existing conflict-parsing
  // convention) — `details` holds the rest, so nothing duplicates between summary and list.
  assert('Invalid outcome is not mistaken for a generic ProblemDetails', error.title === 'This workflow can’t be saved yet');
  assert(
    'the first diagnostic becomes the summary, prefixed with its stage name',
    error.summary.startsWith('Your existing licence:') && error.summary.includes("binds to field 'membershipTier'")
  );
  assert('the summary itself is jumpable to the stage it came from', error.summaryStageKey === 'licence-details');
  assert('the remaining diagnostic survives as a detail line', error.details.length === 1);
  assert(
    'a diagnostic naming a real stage resolves a jumpable stageKey, prefixed with the stage name',
    error.details[0]?.stageKey === 'licence-details' && error.details[0]?.message.startsWith('Your existing licence:')
  );
  assert(
    'the second diagnostic message is shown verbatim',
    error.details[0]?.message.includes("Unknown name 'isMember'")
  );
}

// A diagnostic path with no resolvable stage (e.g. a workflow-level calculations error) must
// still show its message, just without a dead "jump" affordance.
{
  const payload = JSON.stringify({
    status: 'Invalid',
    diagnostics: [
      {
        code: 'CALC_FIELD_ERROR',
        path: 'calculations.fields.isMember',
        message: "Unknown name 'member' in expression 'member.tier <> \\'\\''.",
        severity: 'Error',
      },
      {
        code: 'CALC_FIELD_ERROR',
        path: 'calculations.fields.membershipTier',
        message: "Unknown name 'member' in expression 'member.tier'.",
        severity: 'Error',
      },
    ],
  });

  const error = buildSaveErrorFromPayload(payload, 400, 'Bad Request', 'application/json', workflow.definitionKey, workflow);
  assert('workflow-level diagnostic (no state in its path) has no stageKey', error.details[0]?.stageKey === undefined);
  assert('workflow-level diagnostic message still shown, unprefixed', error.details[0]?.message === "Unknown name 'member' in expression 'member.tier'.");
}

// A genuine framework-level 400 (e.g. the JSON body itself failed to deserialize) must still
// go through the ProblemDetails path — it has no "diagnostics" array at all.
{
  const payload = JSON.stringify({
    type: 'https://tools.ietf.org/html/rfc9110#section-15.5.1',
    title: 'One or more validation errors occurred.',
    status: 400,
    errors: [{ $: ["The JSON payload for polymorphic interface or abstract type 'PrismComponent' must specify a type discriminator."] }],
    traceId: '00-abc-def-00',
  });

  const error = buildSaveErrorFromPayload(payload, 400, 'Bad Request', 'application/json', workflow.definitionKey, workflow);
  assert('framework ProblemDetails is not mistaken for a WorkflowSaveOutcome', error.title === 'One or more validation errors occurred.');
  assert('traceId is preserved for ProblemDetails', error.traceId === '00-abc-def-00');
}

// A 409 conflict must still resolve via the conflict path, not the new Invalid path (both
// shapes carry a "diagnostics" array).
{
  const payload = JSON.stringify({
    status: 'Conflict',
    diagnostics: [
      {
        code: 'SAVE_VERSION_CONFLICT',
        path: 'version',
        message: 'Workflow has changed since it was loaded — current version is 5.',
        severity: 'Error',
      },
    ],
    currentVersion: 5,
  });

  const error = buildSaveErrorFromPayload(payload, 409, 'Conflict', 'application/json', workflow.definitionKey, workflow);
  assert('409 still resolves as a conflict, not a validation failure', error.isConflict === true);
  assert('conflict currentVersion is preserved', error.currentVersion === 5);
}

if (failures > 0) {
  console.error(`\n${failures} save-error parsing failure(s).`);
  process.exit(1);
}

console.log('Save-error parsing correctly distinguishes validation failures, conflicts, and framework errors.');
