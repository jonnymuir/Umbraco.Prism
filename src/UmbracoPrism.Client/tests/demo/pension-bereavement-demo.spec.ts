import { test, expect, type Page, type Locator } from '@playwright/test';
import { execFileSync } from 'node:child_process';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { resetWorkflows, businessAppOrigin } from '../walkthroughs/support/walkthrough';
import { beat, showSlate, clearSlate, moveNarrationTo, startNarrationTimeline, getNarrationTimeline } from './support/narration';
import { humanClick, humanType } from './support/human-interactions';

// Sibling of garden-waste-demo.spec.ts — same one-page/one-video technique (see that file's own
// header comment), same narration/human-interaction helpers. The story this one tells is
// deliberately different: garden-waste hand-builds a first stage before ever calling the agent;
// this one hands the ENTIRE design — research included — to the agent in a single turn, and only
// afterward tours the surfaces (backoffice, business admin, editor, live run) that now reflect it.
const __dirname = path.dirname(fileURLToPath(import.meta.url));
const footageDir = path.join(__dirname, '..', '..', 'demo-footage');
mkdirSync(footageDir, { recursive: true });

// Act 5's loop bounces between two origins several times a round; an admin action's own
// server-side redirect (POST /admin/workflow/{id}/advance -> 302 -> GET /admin/workflow) can
// still be in flight when the very next page.goto() fires, and Chromium aborts whichever
// navigation loses that race — surfacing as net::ERR_ABORTED on the goto call, not the click that
// actually caused it. A short pause plus one retry is enough since the redirect itself is local
// and fast; this isn't papering over a real failure — assertNoValidationErrors/assertFormIsValid
// still run against whatever page we land on afterwards.
async function gotoWithRetry(page: Page, url: string): Promise<void> {
  try {
    await page.goto(url);
  } catch (error) {
    if (!(error instanceof Error) || !error.message.includes('ERR_ABORTED')) {
      throw error;
    }
    await page.waitForTimeout(500);
    await page.goto(url);
  }
}

function tryConvertToMp4(webmPath: string): void {
  const mp4Path = webmPath.replace(/\.webm$/, '.mp4');
  try {
    execFileSync(
      'ffmpeg',
      ['-y', '-i', webmPath, '-c:v', 'libx264', '-preset', 'medium', '-crf', '18', '-c:a', 'aac', mp4Path],
      { stdio: 'ignore' }
    );
    console.log(`Also wrote ${mp4Path} (more portable for Keynote/PowerPoint than .webm).`);
  } catch {
    console.log('ffmpeg not found on PATH — skipping the .mp4 convenience copy. The .webm is the real output.');
  }
}

// Not a CI test — a demo-recording tool. Run with:
//   TTYD_PASSWORD=<password> npm run demo:record:bereavement
// See tests/demo/README.md for the operator setup shared with garden-waste-demo.spec.ts.

const workflowKey = 'pension-bereavement';
const adminCredentials = { username: 'admin@prism.local', password: 'PrismLocal!12345' };
const ttydUrl = process.env.TTYD_URL ?? 'http://127.0.0.1:7681';
const claudeSessionLogPath = '/tmp/claude-session.log';

interface WorkflowStateDefinition {
  stateKey: string;
  title?: string;
  queueKey?: string;
}

interface WorkflowDefinition {
  definitionKey: string;
  displayName?: string;
  states?: WorkflowStateDefinition[];
  queues?: Array<{ key?: string; name?: string; displayName?: string }>;
}

// The brief handed to the agent in one turn — deliberately industry-agnostic on Prism's side
// (Double Diamond / GOV.UK Service Standard / Good Services, via the workflow-docs:// resource);
// all the pensions-specific knowledge is live web research the agent does for itself, same as a
// real service designer would.
const brief = [
  "You're acting as a service designer with access to Umbraco Prism's workflow authoring",
  'MCP toolkit (server name "prism-workflow"). Your task: design and build a working',
  'proof-of-concept end-to-end service for handling what happens when a UK pension scheme',
  'member dies — sometimes called a bereavement or death-benefits process.',
  'Before drafting anything: first, read the workflow-docs://service-design-principles MCP',
  'resource for the general service design grounding Prism expects (the Design Council Double',
  "Diamond, the GOV.UK Service Standard, and Lou Downe's Good Services principles) — it is",
  'deliberately industry-agnostic and will not tell you anything about pensions. Second,',
  'research the UK pensions industry specifics yourself using web search: PASA best practice',
  "for handling a member's death, FCA Consumer Duty expectations for bereaved or vulnerable",
  'customers, and the discretionary nature of UK pension death benefits (an expression of wish',
  'or nomination form is not binding on the scheme administrator or trustee) — Prism',
  'deliberately does not bake industry knowledge into its MCP toolkit, that is the service',
  "designer's job. Third, read workflow-docs://authoring-guide and",
  'workflow-docs://calculation-language, and use list_workflows/read_workflow to look at the',
  'existing seeded workflows as style references, particularly community-enquiry and',
  'information-request for their two-queue applicant/reviewer patterns. Then design the',
  'service: identify the queues involved (the bereaved informant or next of kin, and the',
  'pensions administration team), the states each side needs, where a discretionary decision',
  'point sits, and where the design should loop in a human per the Good Services checklist.',
  'Draft a new WorkflowDefinitionFile, validate it with validate_workflow, dry-run it with',
  'simulate_workflow, and once it looks right, save it with save_workflow under a new',
  `definition key "${workflowKey}". Finish with a short summary of the key service design`,
  'decisions you made and why, naming the specific principle or standard that drove each one.'
].join(' ');

// Same rationale as garden-waste-demo.spec.ts's connectToClaudeTerminal: ttyd's xterm.js renders
// to canvas, not DOM text, so the only way to detect the one-time BypassPermissions consent gate
// is via the tee'd session log, and only answer it if it's actually showing.
async function connectToClaudeTerminal(page: Page): Promise<void> {
  await page.goto(ttydUrl, { waitUntil: 'networkidle', timeout: 15_000 });
  await page.waitForTimeout(2_000);
  await humanClick(page, page.locator('.xterm-screen'));
  await page.waitForTimeout(500);
  let recentLog = '';
  try {
    recentLog = readFileSync(claudeSessionLogPath, 'utf8').slice(-4000);
  } catch {
    // No log yet — nothing to detect either way.
  }
  if (/Yes, I accept|No, exit/i.test(recentLog)) {
    await page.keyboard.press('2');
    await page.keyboard.press('Enter');
    await page.waitForTimeout(1_000);
  }
}

// Generic best-effort form filler for whatever schema the agent actually invented this run — the
// exact field keys/labels are the agent's own creative choice each time, so this can't hardcode
// them the way a fixed-schema spec could. Heuristic by label text and input type/attributes.
async function fillGdsFormGenerically(page: Page): Promise<void> {
  const dateGroups = await page.evaluate(() => {
    const inputs = Array.from(document.querySelectorAll<HTMLInputElement>('input[name^="fields["]'));
    const groups = new Set<string>();
    for (const input of inputs) {
      const match = input.name.match(/^fields\[(.+)-(day|month|year)\]$/);
      if (match) groups.add(match[1]);
    }
    return Array.from(groups);
  });
  for (const key of dateGroups) {
    await humanType(page, page.locator(`input[name="fields[${key}-day]"]`), '3');
    await humanType(page, page.locator(`input[name="fields[${key}-month]"]`), '7');
    await humanType(page, page.locator(`input[name="fields[${key}-year]"]`), '2026');
  }

  // Two field-naming conventions exist across the surfaces this filler has to handle: the
  // informant-facing GDS pages use `fields[key]` with a separate `label[for]`; MockBusinessApp's
  // admin surface (Program.cs RenderComponent) uses `field:key` with no `id` at all — the label
  // wraps the input directly instead. Missing the second convention previously left every admin
  // required field unfilled, which the browser's own HTML5 `required` then silently blocked at
  // submit time (no navigation, no error markup) — nothing downstream ever saw a failure.
  const textLikeFields = await page.evaluate(() => {
    const results: Array<{ name: string; tag: string; type: string; label: string }> = [];
    const els = document.querySelectorAll<HTMLInputElement | HTMLTextAreaElement>(
      'input[name^="fields["], textarea[name^="fields["], input[name^="field:"], textarea[name^="field:"]'
    );
    for (const el of Array.from(els)) {
      if (/^fields\[.+-(day|month|year)\]$/.test(el.name)) continue;
      if (el instanceof HTMLInputElement && (el.type === 'radio' || el.type === 'checkbox' || el.type === 'hidden')) continue;

      let label = document.querySelector(`label[for="${el.id}"]`)?.textContent?.trim() ?? '';
      if (!label) {
        const wrapping = el.closest('label');
        if (wrapping) {
          const clone = wrapping.cloneNode(true) as HTMLElement;
          clone.querySelectorAll('input, textarea, select').forEach(child => child.remove());
          label = clone.textContent?.trim() ?? '';
        }
      }

      results.push({ name: el.name, tag: el.tagName.toLowerCase(), type: el instanceof HTMLInputElement ? el.type : 'textarea', label });
    }
    return results;
  });
  for (const field of textLikeFields) {
    const label = field.label.toLowerCase();
    let value = 'Not applicable';
    if (/name/.test(label)) value = /informant|next of kin|your/.test(label) ? 'Molly Weasley' : 'Arthur Weasley';
    else if (/reference|scheme|member.*number|policy/.test(label)) value = 'PEN-778211';
    else if (/email/.test(label) || field.type === 'email') value = 'demo@prism.local';
    else if (/phone|contact number|telephone/.test(label) || field.type === 'tel') value = '07700 900123';
    else if (/relationship/.test(label)) value = 'Spouse';
    else if (/note|detail|comment|additional/.test(label) || field.tag === 'textarea') {
      value = 'Please handle sensitively — recently bereaved.';
    } else if (field.type === 'number') value = '1';

    await humanType(page, page.locator(`[name="${field.name}"]`), value);
  }

  const selects = page.locator('select[name^="fields["]');
  const selectCount = await selects.count();
  for (let i = 0; i < selectCount; i++) {
    const select = selects.nth(i);
    const options = await select.locator('option').all();
    if (options.length > 1) {
      await select.selectOption({ index: 1 });
    }
  }

  const radioNames = await page.evaluate(() =>
    Array.from(new Set(
      Array.from(document.querySelectorAll<HTMLInputElement>('input[type="radio"][name^="fields["]')).map(r => r.name)
    ))
  );
  for (const name of radioNames) {
    await humanClick(page, page.locator(`input[type="radio"][name="${name}"]`).first());
  }

  // Checkboxes were previously skipped entirely (excluded from textLikeFields, never handled
  // anywhere else) — a real gap: a required GDS declaration checkbox ("I confirm this
  // information is correct") or a required checkbox-list ("select at least one") would silently
  // fail server-side validation with nothing in this filler ever attempting to check one.
  // Checking the first option per name group covers both shapes without over-selecting a list.
  const checkboxNames = await page.evaluate(() =>
    Array.from(new Set(
      Array.from(document.querySelectorAll<HTMLInputElement>('input[type="checkbox"][name^="fields["]')).map(c => c.name)
    ))
  );
  for (const name of checkboxNames) {
    await humanClick(page, page.locator(`input[type="checkbox"][name="${name}"]`).first());
  }
}

// Complements assertNoValidationErrors: the informant's GDS pages re-render a server-side
// `.govuk-error-summary` on failure, but MockBusinessApp's bare admin surface has no such
// markup — a required field left empty there just makes the browser silently refuse to submit
// the form at all (no navigation, no error, nothing for a DOM-scraping check to see). Checking
// native HTML5 validity directly, before ever clicking submit, catches that class of failure on
// *either* surface regardless of what (if anything) the failure would otherwise render as.
async function assertFormIsValid(page: Page, context: string): Promise<void> {
  const invalidFields = await page.evaluate(() =>
    Array.from(document.querySelectorAll<HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement>(
      'input[name^="fields["], textarea[name^="fields["], select[name^="fields["], ' +
      'input[name^="field:"], textarea[name^="field:"]'
    ))
      .filter(el => !el.checkValidity())
      .map(el => ({ name: el.name, validationMessage: el.validationMessage }))
  );
  if (invalidFields.length > 0) {
    throw new Error(
      `${context}: the generic form filler left ${invalidFields.length} field(s) failing native ` +
      `validation before submit was even clicked — ${invalidFields.map(f => `'${f.name}' (${f.validationMessage})`).join(', ')}.`
    );
  }
}

// GDS's own pattern: a failed submission re-renders the same page with a
// `.govuk-error-summary` at the top (`role="alert"`) — the CLAUDE.md testing convention this
// project follows is to always check both the summary and field-level errors, so a silent
// validation failure never gets mistaken for a real step forward. Throws instead of returning
// a boolean so a broken run stops loudly right where it broke, with the page still open for
// inspection, rather than sailing on to a closing slate that claims success.
async function assertNoValidationErrors(page: Page, context: string): Promise<void> {
  const errorSummary = page.locator('.govuk-error-summary');
  const count = await errorSummary.count();
  if (count > 0) {
    const text = await errorSummary.first().innerText().catch(() => '(could not read error summary text)');
    throw new Error(
      `${context}: submission failed validation instead of advancing — a required field was ` +
      `left unfilled or mis-filled by the generic form filler. Error summary:\n${text}`
    );
  }
}

// Prefer an action whose label doesn't read as a loop-back/request for more information —
// otherwise a blind first-button click can pick exactly the branch that never reaches a
// completed state (the real defect found reviewing an earlier recording of this same demo).
// Falls back to the first not-yet-tried button, then to the very first button, since a fully
// generic script can't always know an agent's exact wording for "this is the forward path".
async function pickForwardAction(
  page: Page,
  alreadyTried: Set<string>
): Promise<Locator> {
  const buttons = page.locator('button.btn-queue-action');
  const count = await buttons.count();
  // Each button's visible text is the route's `label`; its `value` attribute (the form posts
  // `name="action" value="{actionKey}"`) is the real route/trigger key — checking both catches a
  // loop-back action whose programmatic key reads as one (e.g. "request-more-info") even if an
  // agent gave it a friendlier display label, and vice versa.
  const entries = await Promise.all(
    (await buttons.all()).map(async button => ({
      label: (await button.textContent()) ?? '',
      actionKey: (await button.getAttribute('value')) ?? ''
    }))
  );
  const loopBackPattern = /more info|need more|further detail|request|reject|declin|return|re-?open|query|escalat|concern/i;
  const identity = (entry: { label: string; actionKey: string }) => `${entry.actionKey}::${entry.label.trim()}`;

  let candidateIndex = entries.findIndex(entry =>
    !loopBackPattern.test(entry.label) && !loopBackPattern.test(entry.actionKey) && !alreadyTried.has(identity(entry)));
  if (candidateIndex === -1) {
    candidateIndex = entries.findIndex(entry => !alreadyTried.has(identity(entry)));
  }
  if (candidateIndex === -1) {
    candidateIndex = 0;
  }

  alreadyTried.add(identity(entries[candidateIndex] ?? { label: '', actionKey: '' }));
  return buttons.nth(Math.min(candidateIndex, count - 1));
}

// Authoritative completion signal, not a guess at which component an agent chose to render a
// terminal stage with: the runtime computes `IsCompleted`/responseState "complete" from the
// state's `stageType` being "Confirmation" (see WorkflowRuntimeEngine's effectiveStepType switch),
// and /my-workflows already buckets each of the informant's own cases into "In Progress" or
// "Completed" sections using exactly that flag. Walks the DOM backwards from the case's card to
// find the nearest preceding section heading, rather than assuming a specific wrapper markup.
async function isCaseCompletedOnMyWorkflows(page: Page, workflowKey: string): Promise<boolean> {
  await gotoWithRetry(page, '/my-workflows');
  await page.waitForLoadState('networkidle', { timeout: 15_000 }).catch(() => {});
  return page.evaluate(key => {
    const card = document.querySelector(`[data-workflow-key="${key}"]`);
    if (!card) return false;
    let el: Element | null = card;
    while (el) {
      let sibling: Element | null = el.previousElementSibling;
      while (sibling) {
        if (sibling.id === 'completed-heading') return true;
        if (sibling.id === 'active-heading') return false;
        sibling = sibling.previousElementSibling;
      }
      el = el.parentElement;
    }
    return false;
  }, workflowKey);
}

test.describe.serial('pension bereavement demo', () => {
  test.beforeAll(async ({ request }) => {
    await resetWorkflows(request);
  });

  let page: Page;

  test.beforeAll(async ({ browser }) => {
    if (!process.env.TTYD_PASSWORD) {
      throw new Error('Set TTYD_PASSWORD to the password used when launching ttyd — see README.md.');
    }
    const recordingSize = { width: 1920, height: 1080 };
    const context = await browser.newContext({
      viewport: recordingSize,
      recordVideo: { dir: footageDir, size: recordingSize },
      httpCredentials: {
        username: 'demo',
        password: process.env.TTYD_PASSWORD,
        origin: new URL(ttydUrl).origin
      }
    });
    page = await context.newPage();
    startNarrationTimeline();

    // Bigger ttyd terminal text — same window.term hook as garden-waste-demo.spec.ts (see that
    // file's comment for why the `-t fontSize=`/CSS-zoom alternatives don't work).
    await page.addInitScript(ttydOrigin => {
      if (window.location.origin !== ttydOrigin) return;
      let liveTerm: { options: Record<string, unknown> } | undefined;
      Object.defineProperty(window, 'term', {
        configurable: true,
        get: () => liveTerm,
        set: (value: { options: Record<string, unknown> }) => {
          liveTerm = value;
          if (value?.options) {
            value.options.fontSize = 26;
          }
        }
      });
    }, new URL(ttydUrl).origin);
  });

  test.afterAll(async () => {
    const video = page?.video();
    await page?.close();
    if (video) {
      const finalPath = path.join(footageDir, 'pension-bereavement-demo.webm');
      await video.saveAs(finalPath);
      await video.delete();
      tryConvertToMp4(finalPath);
      writeFileSync(
        path.join(footageDir, 'pension-bereavement-narration-timeline.json'),
        JSON.stringify(getNarrationTimeline(), null, 2)
      );
    }
  });

  test('Cold open — introduce the demo', async () => {
    await showSlate(page, {
      eyebrow: 'UMBRACO PRISM',
      title: 'Handing an entire service to an AI agent',
      body:
        "This time we're not hand-building a single stage first. We're going to hand an AI agent " +
        "a single brief — build a UK pension bereavement service — and watch it research a real " +
        'regulated process, design a two-sided workflow, and save it, before we ever open the ' +
        "editor ourselves. Then we'll tour every surface that now reflects it: the back office, " +
        'the business app, the editor, and a real end-to-end run.',
      holdMs: 16_000
    });
    await clearSlate(page);
  });

  test('Act 1 — hand off the whole design to the agent over MCP', async ({ request }) => {
    // Real agent call: research (web search) + design + validate/fix + save, all in one turn.
    // Observed to take well over half an hour end to end on prior runs.
    test.setTimeout(40 * 60_000);

    await connectToClaudeTerminal(page);
    await moveNarrationTo(page, 'top');

    await beat(
      page,
      'setup',
      'This is the Claude CLI, connected to our mock business app through nothing but the MCP ' +
        'toolkit Prism ships — no special access, no shortcuts.',
      { position: 'top' }
    );
    await beat(
      page,
      'intent',
      "We're going to hand it one brief: research how a UK pension scheme handles a member's " +
        'death, then design and save a real two-sided workflow for it — entirely on its own.',
      { position: 'top' }
    );

    await page.keyboard.type(brief, { delay: 28 });
    await page.waitForTimeout(400);
    await page.keyboard.press('Enter');

    await beat(
      page,
      'note',
      "It's now reading Prism's service design principles, researching PASA and FCA guidance " +
        'live, and drafting a workflow no one has seeded for it.',
      { position: 'top', holdMs: 6_000 }
    );

    // The real completion signal is the saved definition itself, not anything printed in the
    // terminal — poll the plain REST toolkit (not MCP) for the new definition to actually exist
    // with real shape (more than a trivial scaffold), same pattern as garden-waste-demo.spec.ts.
    await expect.poll(
      async () => {
        const response = await request.get(
          `${businessAppOrigin}/prism/workflow-authoring/workflows/${workflowKey}`,
          { ignoreHTTPSErrors: true }
        );
        if (!response.ok()) return false;
        const definition = (await response.json()) as WorkflowDefinition;
        return (definition.states?.length ?? 0) > 2 && (definition.queues?.length ?? 0) >= 2;
      },
      { timeout: 35 * 60_000, intervals: [10_000] }
    ).toBe(true);

    await beat(
      page,
      'recap',
      'And there it is — researched, designed, validated, and saved to the live engine. No one ' +
        'wrote a line of this workflow by hand.',
      { position: 'top' }
    );
  });

  test('Act 2 — wire it into the live site', async () => {
    await page.goto('/umbraco');
    await humanType(page, page.getByLabel('E-mail'), adminCredentials.username);
    await humanType(page, page.locator('#password-input'), adminCredentials.password);
    await humanClick(page, page.getByRole('button', { name: 'Login' }));
    await page.waitForLoadState('networkidle', { timeout: 30_000 }).catch(() => {});
    await expect(page.getByText('Home', { exact: true }).first()).toBeVisible({ timeout: 30_000 });

    await beat(page, 'setup', "Here's the Umbraco back office — this is where a service designer wires a new service into the real site.");
    await beat(
      page,
      'intent',
      'A page with a Workflow Key connects it to the engine — the same one property every ' +
        'workflow-backed page in Prism uses.'
    );

    await page.getByText('Home', { exact: true }).first().hover();
    await humanClick(page, page.getByRole('button', { name: 'Create item for Home' }));
    await humanClick(page, page.locator('uui-ref-node-document-type').filter({ hasText: 'Workflow Page' }));
    await page.waitForLoadState('networkidle', { timeout: 30_000 }).catch(() => {});

    await humanType(page, page.getByRole('textbox', { name: 'Enter a name...' }), 'Bereavement and Death Benefits');
    await humanType(page, page.getByLabel('Workflow Key'), workflowKey);

    await humanClick(page, page.getByRole('button', { name: 'Save and publish', exact: true }));
    await expect(page.getByRole('alert').getByText('Document published')).toBeVisible({ timeout: 15_000 });

    const publishedUrl = '/bereavement-and-death-benefits/';
    const check = await page.request.get(publishedUrl, { ignoreHTTPSErrors: true });
    expect(check.ok(), `published page did not resolve at ${publishedUrl}`).toBeTruthy();

    await beat(
      page,
      'recap',
      "One content page, one key, and it's wired to the workflow the agent just built. Now a " +
        "real navigation link, so a visitor can actually find it."
    );

    await page.goto('/umbraco/section/content');
    await humanClick(page, page.locator('uui-menu-item').filter({ hasText: 'Settings' }).first());
    await page.waitForLoadState('networkidle', { timeout: 30_000 }).catch(() => {});

    await humanClick(page, page.getByText('Add Mobile Nav Item').nth(1));
    await humanClick(page, page.getByText('Mobile Nav Item', { exact: true }));
    await humanType(page, page.getByLabel('Label'), 'Report a Death');
    await humanType(page, page.getByLabel('URL'), publishedUrl);
    await humanClick(page, page.getByRole('button', { name: 'Create', exact: true }));
    await page.getByRole('button', { name: 'Submit' }).click({ timeout: 5_000 }).catch(() => {});

    await humanClick(page, page.getByRole('button', { name: 'Save and publish', exact: true }));
    await expect(page.getByRole('alert').getByText('Document published')).toBeVisible({ timeout: 15_000 });

    await beat(page, 'recap', 'Published, and linked from the site navigation — no hardcoded URL, same as any other Prism service.');
  });

  test('Act 3 — no manual registration step in the business app', async () => {
    await page.goto(`${businessAppOrigin}/admin/workflow`);
    await page.waitForLoadState('networkidle', { timeout: 15_000 }).catch(() => {});

    await beat(
      page,
      'setup',
      "This is the mock business app's workflow admin — every authored service, plus who's queued up in each one."
    );
    await beat(
      page,
      'recap',
      'The agent saved straight to the live engine over MCP, so the new service is already here — ' +
        'no restart, no redeploy, no manual registration step.'
    );
  });

  test('Act 4 — see what the agent actually designed', async ({ request }) => {
    const definition = (await (
      await request.get(`${businessAppOrigin}/prism/workflow-authoring/workflows/${workflowKey}`, { ignoreHTTPSErrors: true })
    ).json()) as WorkflowDefinition;
    const queueNames = Array.from(new Set((definition.states ?? []).map(s => s.queueKey).filter(Boolean))) as string[];
    const businessState = (definition.states ?? []).find(s => (s.queueKey ?? '').toLowerCase().includes('business'));

    await beat(page, 'intent', "Let's open the editor and see what it actually built — no restart, no redeploy.");

    await page.goto(`${businessAppOrigin}/workflow-editor?workflow=${workflowKey}`);
    await expect(page.locator('[data-prism-component="workflow-editor-shell"]'))
      .toHaveAttribute('data-prism-active-workflow', workflowKey, { timeout: 30_000 });
    await expect(page.locator('prism-workflow-editor'))
      .toHaveAttribute('data-prism-workflow-loaded', workflowKey, { timeout: 30_000 });

    await beat(page, 'note', "Let's zoom to fit so we can see the whole graph.", { position: 'top' });
    // More than one "Fit" button can now match (the editor's per-queue lane toolbars each carry
    // one) — the first is the canvas-level control this beat is actually narrating.
    await humanClick(page, page.getByRole('button', { name: 'Fit' }).first());
    await page.waitForTimeout(400);

    await beat(
      page,
      'recap',
      `${definition.states?.length ?? 0} states across ${queueNames.length} queues (${queueNames.join(', ')}) — ` +
        'a real two-sided service, not a single linear form.',
      { position: 'top' }
    );

    if (businessState) {
      await beat(
        page,
        'intent',
        `Here's the stage on the pensions administration side — "${businessState.title ?? businessState.stateKey}" — ` +
          'where a human makes the discretionary call.',
        { position: 'top' }
      );
      await humanClick(page, page.locator(`[data-prism-stage="${businessState.stateKey}"]`));
      await page.waitForTimeout(600);
      await beat(
        page,
        'recap',
        'A discretionary decision, routed through a real gateway, with a human in the loop — ' +
          'exactly the kind of decision point the Good Services checklist asks for.',
        { position: 'top' }
      );
    }
  });

  test('Act 5 — run it end to end, as both sides', async () => {
    // A real workflow can bounce between informant and administrator more than once (a
    // request-for-more-information round, a discretionary decision round, a final outcome) —
    // fixed "one form, one action" acts silently declared victory after only the first hop even
    // when the case was nowhere near done. This runs bounded round-trips instead, and only
    // narrates completion once /my-workflows itself reports the case as completed — if that
    // never happens within the bound, the test fails loudly rather than the recording quietly
    // implying a finished case that was actually still stuck mid-flow.
    test.setTimeout(10 * 60_000);
    const maxRounds = 6;

    await beat(page, 'setup', "Now let's run it for real — first as the bereaved next of kin.");
    await page.goto('/');
    await page.getByRole('link', { name: 'Sign In' }).click();
    await page.locator('#username').waitFor({ timeout: 120_000 });
    await humanType(page, page.locator('#username'), 'demo@prism.local');
    await humanType(page, page.locator('#password'), 'password');
    await Promise.all([
      page.waitForURL(url => url.pathname !== '/signin-oidc', { timeout: 120_000 }),
      page.locator('#kc-login').click()
    ]);
    await page.goto('/');

    await beat(page, 'intent', "We'll click the real nav link we published a moment ago — the way any visitor actually would.");
    await humanClick(page, page.getByRole('link', { name: 'Report a Death' }));
    await page.waitForLoadState('networkidle', { timeout: 15_000 }).catch(() => {});

    await beat(page, 'note', "This is the first question — reached the way a real visitor would, not a direct URL.");

    const triedAdminLabels = new Set<string>();
    let completed = false;

    for (let round = 0; round < maxRounds && !completed; round++) {
      const informantHasForm = (await page.locator('input[name^="fields["], textarea[name^="fields["], select[name^="fields["]').count()) > 0;
      if (informantHasForm) {
        await fillGdsFormGenerically(page);
        await assertFormIsValid(page, `informant, round ${round + 1}`);
        await page.waitForTimeout(400);
        await humanClick(page, page.locator('form button[type="submit"], form button').first());
        await page.waitForLoadState('networkidle', { timeout: 15_000 }).catch(() => {});
        await assertNoValidationErrors(page, `informant, round ${round + 1}`);
      }

      if (round === 0) {
        await beat(
          page,
          'recap',
          "Submitted — and it's now sitting in a queue on the pensions administration side, " +
            'waiting for a human to pick it up.'
        );
        await beat(page, 'setup', "Let's switch sides and be the pensions administrator.", { position: 'top' });
      }

      if (await isCaseCompletedOnMyWorkflows(page, workflowKey)) {
        completed = true;
        break;
      }

      await gotoWithRetry(page, `${businessAppOrigin}/admin/workflow`);
      await page.waitForLoadState('networkidle', { timeout: 15_000 }).catch(() => {});

      const actionButtons = page.locator('button.btn-queue-action');
      const actionCount = await actionButtons.count();
      if (actionCount > 0) {
        if (round === 0) {
          await beat(page, 'intent', "Here's the case we just submitted, waiting in the admin queue.", { position: 'top' });
        }
        await fillGdsFormGenerically(page);
        await assertFormIsValid(page, `administrator, round ${round + 1}`);
        const chosen = await pickForwardAction(page, triedAdminLabels);
        await humanClick(page, chosen);
        await page.waitForLoadState('networkidle', { timeout: 15_000 }).catch(() => {});
        await assertNoValidationErrors(page, `administrator, round ${round + 1}`);
      }

      // Prep the informant's page for next round's check — if the case is genuinely done, the
      // top of the next iteration's isCaseCompletedOnMyWorkflows call catches it; if there's a
      // further question, this is the same real nav-link path a returning visitor would use.
      await gotoWithRetry(page, '/');
      await humanClick(page, page.getByRole('link', { name: 'Report a Death' }));
      await page.waitForLoadState('networkidle', { timeout: 15_000 }).catch(() => {});
    }

    expect(
      completed,
      `The case never reached a completed state within ${maxRounds} round-trips between the ` +
        'informant and the administrator — the workflow likely has a routing dead end ' +
        '(ValidateReachability should have caught this at save time; if it did not, that check ' +
        'needs another look).'
    ).toBeTruthy();

    // isCaseCompletedOnMyWorkflows already left the page on /my-workflows for its last check —
    // a real, user-facing confirmation that the case genuinely finished, not just an assertion
    // the audience has to take on faith.
    await beat(
      page,
      'recap',
      "And here it is on the informant's own \"My Workflows\" page, moved out of \"In Progress\" " +
        'and into \"Completed\" — the case really did finish, not just render one more screen.'
    );

    await beat(
      page,
      'recap',
      'A real informant on one side, a real administrator on the other, one workflow routing ' +
        'between them, run all the way to completion — designed, built, and saved by an agent ' +
        'that only ever spoke MCP.',
      { position: 'top' }
    );
  });

  test('Closing slate', async () => {
    await showSlate(page, {
      eyebrow: 'UMBRACO PRISM',
      title: "That's the whole loop",
      body:
        'A regulated, two-sided service — researched, designed, and saved by an AI agent talking ' +
        'to nothing but a documented MCP toolkit, then wired into a real site and run end to end ' +
        'by two different people. Thanks for watching.'
    });
  });
});
