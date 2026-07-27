import { test, expect, type Page, type Locator } from '@playwright/test';
import { execFileSync } from 'node:child_process';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { beat, showSlate, clearSlate, moveNarrationTo, startNarrationTimeline, getNarrationTimeline } from './support/narration';
import { humanClick, humanType, humanMoveTo } from './support/human-interactions';
import {
  startDemoTerminalSession,
  showTerminalMirror,
  stopTerminalMirror,
  sendTerminalText,
  sendTerminalKey
} from './support/tmux-terminal';

// Sibling of garden-waste-demo.spec.ts and (the now-removed) pension-bereavement-demo.spec.ts —
// same one-page/one-video technique, same narration/human-interaction helpers. The story this one
// tells is deliberately generic and GDS-precedented (see the project-generic-examples-only
// decision this repo follows): an AI agent designs and builds "Transfer a Professional Juggling
// Licence" — branching eligibility, a guidance checklist you must acknowledge, real document
// upload — against Prism's **CMS ServiceBlueprint** MCP surface, which (unlike MockBusinessApp's open
// one) requires real backoffice client-credentials auth. That auth setup is genuinely new
// territory versus the other two demos and gets its own act.
const __dirname = path.dirname(fileURLToPath(import.meta.url));
const footageDir = path.join(__dirname, '..', '..', 'demo-footage');
mkdirSync(footageDir, { recursive: true });

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
//   npm run demo:record:licence-transfer
// The terminal acts run in a plain tmux session mirrored into the recorded page as styled DOM
// (see support/tmux-terminal.ts for why that replaced ttyd/xterm). See
// docs/demos/licence-transfer-mcp-walkthrough.md for the full narrated storyboard this spec
// follows act-for-act.
//
// Before a real take: restart Aspire with BOTH a clean runtime and a longer backoffice session
// timeout (the default gives OpenIddict client-credentials tokens a 5-minute lifetime — plenty
// for a human, not for a real multi-step agent build):
//   PRISM_TESTSITE_RESET_RUNTIME=true Umbraco__CMS__Global__TimeOut=02:00:00 \
//     dotnet run --project src/UmbracoPrism.AppHost

const serviceBlueprintKey = 'transfer-a-juggling-licence';
const testSiteOrigin = 'https://localhost:44345';
const adminCredentials = { username: 'admin@prism.local', password: 'PrismLocal!12345' };

// A fixed, published local-dev-only secret — same convention as every other credential in this
// repo's demo set (CLAUDE.md's own credentials table). Only ever valid against a throwaway local
// backoffice behind Keycloak/localhost; not a real secret.
const mcpServiceAccountEmail = 'prism-mcp-agent@prism.local';
const mcpClientId = 'prism-mcp-agent';
const mcpClientSecret = 'prism-mcp-agent-demo-secret-2026';
const prefixedMcpClientId = `umbraco-back-office-${mcpClientId}`;

const claudeSessionLogPath = '/tmp/claude-session.log';
const scratchDir = path.join(process.env.HOME ?? '/tmp', 'prism-demo-scratch');

interface ServiceBlueprintComponentNode {
  type?: string;
  children?: ServiceBlueprintComponentNode[];
}

interface ServiceBlueprintStateDefinition {
  stateKey: string;
  displayName?: string;
  queueKey?: string;
  components?: ServiceBlueprintComponentNode[];
}

interface ServiceBlueprintDefinition {
  definitionKey: string;
  displayName?: string;
  version?: number;
  states?: ServiceBlueprintStateDefinition[];
  queues?: Array<{ key?: string }>;
}

/** Wrapper components (fieldset, accordion) nest their real fields under `children` — a flat
 * per-state scan misses anything grouped inside one, so this walks the whole tree. */
function collectComponentTypes(nodes: ServiceBlueprintComponentNode[] | undefined): Set<string> {
  const types = new Set<string>();
  for (const node of nodes ?? []) {
    if (node.type) types.add(node.type);
    for (const t of collectComponentTypes(node.children)) types.add(t);
  }
  return types;
}

// The brief handed to the agent in one turn — verified live end-to-end before this spec was
// written (see the project-juggling-licence-transfer-demo memory). Deliberately fictional-but-
// GDS-precedented, industry-agnostic: no live research needed, unlike the removed pensions demo.
const brief = [
  'You\'re acting as a service designer with access to Umbraco Prism\'s CMS ServiceBlueprint authoring',
  'MCP toolkit (server name "prism-cms-service-blueprint"). Your task: design and build "Transfer a',
  'Professional Juggling Licence" — a fictional but structurally real GDS-style public service',
  'for someone who already holds a professional juggling licence from another juggling authority',
  'and wants to transfer it to the National Juggling Authority.',
  'Read service-blueprint-docs://authoring-guide for the contract shape, and use',
  'list_service_blueprints/read_service_blueprint to look at the existing apply-for-a-juggling-licence definition as',
  'your style reference for this host\'s conventions (it\'s the same fictional domain, a simpler',
  'application rather than a transfer) — including how it defaults a field from the visitor\'s real',
  'membership data via a service input and a calculated pass-through field; do the same here.',
  'Design and save a new definition under the key "transfer-a-juggling-licence" with this shape:',
  '1. Eligibility — three real branching questions (previously performed professionally? licence',
  'issued outside the UK? overseas authority recognised by the "International Juggling',
  'Accreditation Register"?), each "no"/failing answer routing to its own distinct ineligible-',
  'outcome state, not just a validation message.',
  '2. Guidance — a guidance-checklist component with these four items, required: true (all four',
  'must be acknowledged before continuing):',
  '   - "Transfer Rules" -> /transfer-rules',
  '   - "International Transfers" -> /international-transfers',
  '   - "Supporting Evidence" -> /supporting-evidence',
  '   - "Professional Standards" -> /professional-standards',
  '   (These are real, already-published CMS pages — link to them exactly as given, don\'t',
  'invent different URLs.)',
  '3. Existing licence details — current authority, licence reference, issue date, expiry date,',
  'professional category. Default professional category from the visitor\'s real Juggling Society',
  'membership tier, exactly the way apply-for-a-juggling-licence defaults its own licence-type',
  'field — a visitor who isn\'t a member simply gets no default, same as that reference service blueprint.',
  '4. Upload evidence — file-upload fields: current licence, proof of identity, proof of address,',
  'and a professional portfolio (all required: true), plus optional video evidence (required:',
  'false).',
  '5. Check your answers — a summary-list reviewing everything captured, with changeStateKey (or',
  'per-row overrides) so the applicant can go back and fix an answer before submitting.',
  '6. Declaration — three required boolean statements (information is accurate; authorise the',
  'National Juggling Authority to contact the current licensing body; understands misleading',
  'information may cause rejection).',
  '7. Confirmation — a simple submitted panel. Don\'t build any post-submission case tracking —',
  'that\'s explicitly out of scope for this version.',
  'Validate with validate_service_blueprint, dry-run the eligibility branches and the full happy path with',
  'simulate_service_blueprint, fix anything it flags, then save_service_blueprint. Finish with a short summary of',
  'the design decisions you made.'
].join(' ');

// ------------------------------------------------------------------ Act 1 helpers: real backoffice auth

/**
 * The backoffice SPA's own access token lives only in the SPA's in-memory JS runtime — not
 * localStorage/sessionStorage/IndexedDB. Capture it off an outgoing authenticated request instead
 * of trying to read it from browser storage.
 */
async function captureBearerToken(page: Page): Promise<string> {
  let captured: string | null = null;
  const handler = (request: import('@playwright/test').Request) => {
    if (captured) return;
    const auth = request.headers()['authorization'];
    if (auth?.startsWith('Bearer ')) captured = auth.slice('Bearer '.length);
  };
  page.on('request', handler);
  await page.goto(`${testSiteOrigin}/umbraco/section/content`);
  await page.waitForLoadState('networkidle', { timeout: 20_000 }).catch(() => {});
  await page.waitForTimeout(2_000);
  page.off('request', handler);
  if (!captured) {
    throw new Error('Could not capture a backoffice bearer token from any outgoing request.');
  }
  return captured;
}

/**
 * Idempotent: creates the dedicated Kind=Api service-account user and registers client
 * credentials for it if they don't already exist. A regular interactive-login user (like
 * admin@prism.local) cannot have client-credentials registered — Umbraco's AddClientIdAsync
 * rejects anything but UserKind.Api.
 */
async function ensureMcpServiceAccount(page: Page, backofficeToken: string): Promise<void> {
  const authHeaders = { Authorization: `Bearer ${backofficeToken}`, 'Content-Type': 'application/json' };

  // The Management API's `filter` param is a loose/fuzzy match, not an exact-email lookup — on a
  // fresh DB with only the admin user present, filtering for the service account's email still
  // returned the admin user, so this must check the actual `email` field itself rather than
  // trusting a non-empty result to mean "the service account already exists".
  const existingResponse = await page.request.get(
    `${testSiteOrigin}/umbraco/management/api/v1/user?filter=${encodeURIComponent(mcpServiceAccountEmail)}&skip=0&take=100`,
    { ignoreHTTPSErrors: true, headers: authHeaders }
  );
  const existing = existingResponse.ok() ? await existingResponse.json() : { items: [] };
  const alreadyExists = (existing.items ?? []).some((u: { email?: string }) => u.email === mcpServiceAccountEmail);
  if (alreadyExists) {
    return; // Already set up from a previous run — same fixed secret, nothing to redo.
  }

  const groupsResponse = await page.request.get(
    `${testSiteOrigin}/umbraco/management/api/v1/user-group?skip=0&take=100`,
    { ignoreHTTPSErrors: true, headers: authHeaders }
  );
  const groups = await groupsResponse.json();
  const adminGroup = groups.items?.find((g: { alias?: string; name?: string }) => g.alias === 'admin' || g.name === 'Administrators');
  if (!adminGroup) {
    throw new Error('Could not find an admin user group to assign the MCP service account to.');
  }

  const createResponse = await page.request.post(`${testSiteOrigin}/umbraco/management/api/v1/user`, {
    ignoreHTTPSErrors: true,
    headers: authHeaders,
    data: {
      email: mcpServiceAccountEmail,
      userName: mcpServiceAccountEmail,
      name: 'Prism MCP Agent',
      userGroupIds: [{ id: adminGroup.id }],
      kind: 'Api'
    }
  });
  if (createResponse.status() !== 201) {
    throw new Error(`Could not create the MCP service account user: ${createResponse.status()} ${await createResponse.text()}`);
  }
  const userId = createResponse.headers()['location']?.split('/').pop();

  const registerResponse = await page.request.post(
    `${testSiteOrigin}/umbraco/management/api/v1/user/${userId}/client-credentials`,
    { ignoreHTTPSErrors: true, headers: authHeaders, data: { clientId: mcpClientId, clientSecret: mcpClientSecret } }
  );
  if (registerResponse.status() !== 200) {
    throw new Error(`Could not register client credentials: ${registerResponse.status()} ${await registerResponse.text()}`);
  }
}

/** Finds TestSite's real plain-HTTP MCP port — dynamic per Aspire run, distinct from the fixed HTTPS one. */
/**
 * True only if this port hosts the *live* MCP endpoint AND accepts the given bearer token — a
 * full authenticated `initialize` round trip, not a weaker liveness signal. A bare unauthenticated
 * probe (does it answer 401?) once picked a stale, orphaned TestSite from an earlier boot: it
 * happily answered 401 (any backoffice-auth'd ASP.NET app does), but rejected the freshly-minted
 * token (different signing keys), so the agent's MCP client showed "Failed to connect" and the
 * agent launched with no tools at all — hallucinating tool-call syntax as plain text.
 */
function probeMcpInitialize(port: number, bearerToken: string): boolean {
  try {
    const status = execFileSync('curl', [
      '-s', '-o', '/dev/null', '-w', '%{http_code}', '--max-time', '3',
      '-X', 'POST', `http://localhost:${port}/prism/service-blueprint-authoring/mcp`,
      '-H', `Authorization: Bearer ${bearerToken}`,
      '-H', 'Content-Type: application/json',
      '-H', 'Accept: application/json, text/event-stream',
      '-d', '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"demo-probe","version":"1.0"}}}'
    ]).toString();
    return status === '200';
  } catch {
    return false;
  }
}

function discoverCmsServiceBlueprintMcpHttpPort(bearerToken: string): number {
  // Every matching pid, not just the first — a stale orphaned TestSite from an earlier boot can
  // legitimately coexist with the live one for a while, and which `ps` lists first is luck.
  const pids = execFileSync('bash', ['-c', "ps aux | grep 'UmbracoPrism.TestSite/bin' | grep -v grep | awk '{print $2}'"])
    .toString()
    .trim()
    .split('\n')
    .filter(Boolean);
  if (pids.length === 0) throw new Error('Could not find a running TestSite process to discover its MCP port.');

  const uniquePorts = new Set<number>();
  for (const pid of pids) {
    try {
      const lsofOutput = execFileSync('bash', ['-c', `lsof -p ${pid} -a -i -P 2>/dev/null | grep LISTEN`]).toString();
      for (const match of lsofOutput.matchAll(/localhost:(\d+)/g)) {
        uniquePorts.add(Number(match[1]));
      }
    } catch {
      // Process may have just exited — skip it.
    }
  }

  for (const port of uniquePorts) {
    if (probeMcpInitialize(port, bearerToken)) return port;
  }
  throw new Error(
    `No port among [${Array.from(uniquePorts).join(', ')}] completed an authenticated MCP initialize — ` +
    'is the token valid against the currently-running TestSite?'
  );
}

/**
 * The one-time BypassPermissions consent gate can only be detected via the `script`-tee'd
 * session log — and should only be answered if it's actually showing (blindly sending "2"+Enter
 * on a visit where the gate isn't showing types a literal "2" into a live empty prompt and
 * visibly confuses whatever's running).
 */
async function handleBypassPermissionsGateIfShowing(page: Page): Promise<void> {
  let recentLog = '';
  try {
    recentLog = readFileSync(claudeSessionLogPath, 'utf8').slice(-4000);
  } catch {
    // No log yet — nothing to detect either way.
  }
  if (/Yes, I accept|No, exit/i.test(recentLog)) {
    sendTerminalKey('2');
    sendTerminalKey('Enter');
    await page.waitForTimeout(1_000);
  }
}

/** Types a real command into the tmux session, character by character, then presses Enter. */
async function typeInTerminal(page: Page, command: string, delay = 10): Promise<void> {
  await sendTerminalText(command, delay);
  await page.waitForTimeout(300);
  sendTerminalKey('Enter');
}

// ------------------------------------------------------------------ Act 5 helpers: generic form filling

/**
 * The agent designs its own field keys each run — these can't be hardcoded the way a fixed-schema
 * spec could. Fills every ordinary text-like input/select/date-group generically by label
 * heuristics, exactly like the removed pension-bereavement demo's fillGdsFormGenerically, plus two
 * things this service blueprint specifically needs that no prior demo did: real file uploads, and
 * acknowledging every checkbox inside a guidance-checklist container (not just one).
 */
async function fillGdsFormGenerically(page: Page, testFilesDir: string): Promise<void> {
  // A cross-origin round trip (e.g. Keycloak sign-in) has been observed to leave the recorded
  // video frozen on the pre-redirect frame for tens of seconds afterward, even in headed mode —
  // the underlying automation keeps working correctly the whole time (assertions still pass), but
  // the video capture itself stalls, plausibly because the window loses real OS-level foreground
  // focus during the redirect. Forcing focus back at the start of every stage-fill is cheap
  // insurance against that recurring mid-take.
  await page.bringToFront();

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
    await page.locator(`input[name="fields[${key}-day]"]`).fill('1');
    await page.locator(`input[name="fields[${key}-month]"]`).fill('1');
    await page.locator(`input[name="fields[${key}-year]"]`).fill('2022');
  }

  // Real file uploads for every file-upload field on the page — this is the one thing this
  // service blueprint needs that no prior demo's generic filler handled.
  //
  // A bare, un-paced loop of setInputFiles calls (like an equally bare loop of .check() calls
  // below) executes in a handful of milliseconds — on a 25fps recording, ticking 4 checkboxes or
  // attaching 5 files can complete within a single frame, effectively invisible to anyone
  // watching. Unlike answerEligibilityQuestion (which already moves the visible cursor via
  // humanMoveTo before each .check()), this generic filler had none of that, making entire pages
  // — guidance, upload evidence — appear to flash past with nothing visibly happening. Moving
  // the cursor to each field and pausing briefly between actions is what actually makes the
  // recording show the interaction, not just its instant end state.
  const fileInputs = page.locator('input[type="file"][name^="fields["]');
  const fileCount = await fileInputs.count();
  for (let i = 0; i < fileCount; i++) {
    const input = fileInputs.nth(i);
    const box = await input.boundingBox();
    if (box) await humanMoveTo(page, box.x + box.width / 2, box.y + box.height / 2);
    await input.setInputFiles(path.join(testFilesDir, 'evidence.pdf'));
    await page.waitForTimeout(500);
  }

  // Acknowledge every checkbox inside a guidance-checklist container — required: true here means
  // ALL must be checked, unlike an ordinary checkboxlist where any one would do. .check() rather
  // than humanClick's raw coordinate click — GDS checkboxes visually replace the native input the
  // same way radios do, so a coordinate click can miss the real toggle target. See the file-upload
  // loop above for why the cursor move + pause per item matter for the recording.
  const guidanceCheckboxes = page.locator('[data-prism-guidance-checklist] input[type="checkbox"]');
  const guidanceCount = await guidanceCheckboxes.count();
  for (let i = 0; i < guidanceCount; i++) {
    const checkbox = guidanceCheckboxes.nth(i);
    const box = await checkbox.boundingBox();
    if (box) await humanMoveTo(page, box.x + box.width / 2, box.y + box.height / 2);
    await checkbox.check();
    await page.waitForTimeout(400);
  }

  const textLikeFields = await page.evaluate(() => {
    const results: Array<{ name: string; tag: string; type: string; label: string; currentValue: string }> = [];
    const els = document.querySelectorAll<HTMLInputElement | HTMLTextAreaElement>(
      'input[name^="fields["], textarea[name^="fields["]'
    );
    for (const el of Array.from(els)) {
      if (/^fields\[.+-(day|month|year)\]$/.test(el.name)) continue;
      if (el instanceof HTMLInputElement && (el.type === 'radio' || el.type === 'checkbox' || el.type === 'hidden' || el.type === 'file')) continue;

      let label = document.querySelector(`label[for="${el.id}"]`)?.textContent?.trim() ?? '';
      if (!label) {
        const wrapping = el.closest('label');
        if (wrapping) {
          const clone = wrapping.cloneNode(true) as HTMLElement;
          clone.querySelectorAll('input, textarea, select').forEach(child => child.remove());
          label = clone.textContent?.trim() ?? '';
        }
      }
      results.push({ name: el.name, tag: el.tagName.toLowerCase(), type: el instanceof HTMLInputElement ? el.type : 'textarea', label, currentValue: el.value });
    }
    return results;
  });
  for (const field of textLikeFields) {
    // A field can arrive already populated by a real defaultFrom binding (e.g. professional
    // category defaulted from a logged-in member's real data) — a genuine, overridable default,
    // not a lock, so a generic filler should leave it alone rather than clobber it.
    if (field.currentValue.trim().length > 0) continue;
    const label = field.label.toLowerCase();
    let value = 'Not applicable';
    if (/authority/.test(label)) value = 'International Juggling Federation';
    else if (/reference/.test(label)) value = 'IJF-2024-00123';
    else if (/category/.test(label)) value = 'Fire juggling';
    else if (/email/.test(label) || field.type === 'email') value = 'demo@prism.local';
    await page.locator(`[name="${field.name}"]`).fill(value);
  }

  const selects = page.locator('select[name^="fields["]');
  const selectCount = await selects.count();
  for (let i = 0; i < selectCount; i++) {
    const options = await selects.nth(i).locator('option').all();
    if (options.length > 1) await selects.nth(i).selectOption({ index: 1 });
  }

  // Declaration-style booleans (checkboxes not inside a guidance-checklist container).
  const declarationCheckboxes = page.locator('input[type="checkbox"][name^="fields["]:not([data-prism-guidance-checklist] *)');
  const declarationCount = await declarationCheckboxes.count();
  for (let i = 0; i < declarationCount; i++) {
    await declarationCheckboxes.nth(i).check();
  }
}

/**
 * The primary submit action for the current stage — never a per-row "Change" link. Check
 * Answers renders each row's Change trigger as its own `<button name="Action" value="change:...">`
 * (see _PrismComponent-SummaryList.cshtml), appearing before the real continue/confirm button in
 * DOM order, so a plain `.first()` on `button[name="Action"]` picks a Change link instead and
 * silently routes back to an earlier stage.
 */
function primaryActionButton(page: Page): Locator {
  return page.locator('button[name="Action"]:not([value^="change:"])').first();
}

/**
 * Clicks a locator and waits for the resulting navigation — hardened against two failure modes
 * found the hard way:
 * 1. waitForLoadState/waitForNavigation called *after* a click races the click itself, if the
 *    resulting navigation hasn't started yet — starting the wait before the click closes that.
 * 2. humanClick's own Playwright actions (scrollIntoViewIfNeeded etc.) have no explicit timeout,
 *    so if the locator goes stale mid-action it can hang for the *entire remaining test budget*
 *    instead of failing fast — bounding it here with a fallback to a plain, timeout-bounded
 *    locator.click() keeps one bad click from burning the whole take. Originally only applied to
 *    Act 5's own stage-to-stage clicks; retrofitted onto answerEligibilityQuestion's button click
 *    too after this exact gap caused an inconsistent eligibility answer under headed rendering.
 */
async function clickAndWaitForNavigation(page: Page, locator: Locator): Promise<void> {
  const boundedHumanClick = Promise.race([
    humanClick(page, locator),
    new Promise<void>((_, reject) => setTimeout(() => reject(new Error('humanClick timed out')), 12_000))
  ]);
  try {
    await Promise.all([page.waitForNavigation({ timeout: 20_000 }).catch(() => {}), boundedHumanClick]);
  } catch {
    await locator.click({ timeout: 10_000 }).catch(() => {});
  }
}

/**
 * Answers a single-radio eligibility question via whichever action button reads as the given
 * intent — the agent's own trigger keys/labels aren't guaranteed verbatim across runs, so this
 * matches by "yes"/"no" substring on either the button's visible text or its posted value, the
 * same defensive approach the removed pension-bereavement demo used for its own action-picking.
 */
async function answerEligibilityQuestion(page: Page, eligible: boolean): Promise<void> {
  const radios = page.locator('input[type="radio"][name^="fields["]');
  const radioCount = await radios.count();
  if (radioCount > 0) {
    const wantedValue = eligible ? /yes/i : /no/i;
    let target = radios.first();
    for (let i = 0; i < radioCount; i++) {
      const value = await radios.nth(i).getAttribute('value');
      if (value && wantedValue.test(value)) {
        target = radios.nth(i);
        break;
      }
    }
    // GOV.UK Design System radios visually replace the native input with styled pseudo-elements
    // — a raw coordinate click (humanClick) can land just outside what the browser considers the
    // actual toggle target. .check() is the reliable, semantically-correct way to select it;
    // still move the visible cursor there first purely for the recording's sake.
    await target.scrollIntoViewIfNeeded();
    const box = await target.boundingBox();
    if (box) await humanMoveTo(page, box.x + box.width / 2, box.y + box.height / 2);
    await target.check();
  }

  const buttons = page.locator('button[name="Action"]');
  const buttonCount = await buttons.count();
  const entries = await Promise.all(
    (await buttons.all()).map(async button => ({
      button,
      text: (await button.textContent()) ?? '',
      value: (await button.getAttribute('value')) ?? ''
    }))
  );
  const wanted = eligible ? /yes/i : /no/i;
  const chosen = entries.find(e => wanted.test(e.text) || wanted.test(e.value)) ?? entries[0];
  const target = (!chosen && buttonCount > 0) ? buttons.first() : chosen?.button;
  if (target) {
    await clickAndWaitForNavigation(page, target);
  }
}

// ------------------------------------------------------------------

test.describe.serial('licence transfer demo', () => {
  let page: Page;
  let mintedToken: string;
  let mcpPort: number;

  test.beforeAll(async ({ browser }) => {
    const recordingSize = { width: 1920, height: 1080 };
    const context = await browser.newContext({
      viewport: recordingSize,
      recordVideo: { dir: footageDir, size: recordingSize }
    });
    page = await context.newPage();
    startNarrationTimeline();
  });

  test.afterAll(async () => {
    stopTerminalMirror();
    const video = page?.video();
    await page?.close();
    if (video) {
      const finalPath = path.join(footageDir, 'licence-transfer-demo.webm');
      await video.saveAs(finalPath);
      await video.delete();
      tryConvertToMp4(finalPath);
      writeFileSync(
        path.join(footageDir, 'licence-transfer-narration-timeline.json'),
        JSON.stringify(getNarrationTimeline(), null, 2)
      );
    }
  });

  test('Cold open — introduce the demo', async () => {
    await showSlate(page, {
      eyebrow: 'UMBRACO PRISM',
      title: 'CMS ServiceBlueprint: wiring up a complex service, simply',
      body:
        "This is Prism's CMS ServiceBlueprint — a backoffice-hosted, single-actor service blueprint engine built " +
        "entirely inside Umbraco. To show how it really works, we'll hand an AI agent one brief " +
        "— design and build a real GDS-style transfer service, with branching eligibility, a " +
        "guidance checklist, and real document upload — using nothing but Prism's documented MCP " +
        "toolkit. We need real backoffice authentication for this, so we'll demonstrate exactly " +
        "how that's done too.",
      holdMs: 18_000
    });
    await clearSlate(page);
  });

  test('Act 1 — getting the agent real access', async () => {
    test.setTimeout(2 * 60_000);

    await page.goto(`${testSiteOrigin}/umbraco`);
    await beat(
      page,
      'setup',
      "We need real backoffice authentication for this. Prism's CMS ServiceBlueprint MCP talks to the " +
        "same live engine a human editor uses, not an open sandbox endpoint — so an agent needs " +
        "to log in exactly the way a new team member would. Let's show you exactly how that works."
    );

    await humanType(page, page.getByLabel('E-mail'), adminCredentials.username);
    await humanType(page, page.locator('#password-input'), adminCredentials.password);
    await humanClick(page, page.getByRole('button', { name: 'Login' }));
    await page.waitForLoadState('networkidle', { timeout: 30_000 }).catch(() => {});
    await page.waitForTimeout(2_000);

    await beat(
      page,
      'intent',
      "Behind that login is a dedicated service-account identity with its own client credentials " +
        "— provisioned once, ahead of time, the same way any integration would be."
    );

    const backofficeToken = await captureBearerToken(page);
    await ensureMcpServiceAccount(page, backofficeToken);
    // Discovery probes with a throwaway client-credentials token minted off-camera — the SAME
    // grant the on-camera terminal command mints moments later. The browser session's own
    // backoffice token does NOT pass the MCP endpoint's auth (confirmed live: UI token → 401,
    // client-credentials token → 200), and probing with the exact token kind the agent will use
    // is what proves the port belongs to the currently-running TestSite rather than a stale
    // orphan from an earlier boot (see probeMcpInitialize).
    const probeTokenJson = execFileSync('curl', [
      '-sk', '-X', 'POST', `${testSiteOrigin}/umbraco/management/api/v1/security/back-office/token`,
      '-d', 'grant_type=client_credentials',
      '-d', `client_id=${prefixedMcpClientId}`,
      '-d', `client_secret=${mcpClientSecret}`
    ]).toString();
    const probeToken = (JSON.parse(probeTokenJson) as { access_token?: string }).access_token;
    if (!probeToken) throw new Error(`Off-camera client-credentials mint failed: ${probeTokenJson.slice(0, 200)}`);
    const port = discoverCmsServiceBlueprintMcpHttpPort(probeToken);
    mcpPort = port;

    // The terminal is a plain tmux session wrapping `script` + bash — Act 1's real auth setup
    // (token exchange, `claude mcp add`) happens as real, visible commands in it before claude
    // itself ever launches, and `script` captures everything (including the later claude launch
    // and its one-time consent gate) to the session log from the very first command.
    startDemoTerminalSession(claudeSessionLogPath, scratchDir);
    await page.waitForTimeout(2_000);
    await showTerminalMirror(page);
    await moveNarrationTo(page, 'top');

    await beat(
      page,
      'note',
      "Here's the real terminal. First, exchange those credentials for a genuine access token — " +
        "the exact same OAuth client-credentials flow the backoffice's own login uses.",
      { position: 'top' }
    );

    const tokenFilePath = path.join(scratchDir, 'mcp-token.json');
    await typeInTerminal(
      page,
      `curl -sk -X POST ${testSiteOrigin}/umbraco/management/api/v1/security/back-office/token ` +
        `-d grant_type=client_credentials -d client_id=${prefixedMcpClientId} ` +
        `-d client_secret=${mcpClientSecret} -o ${tokenFilePath} && cat ${tokenFilePath} | jq .`
    );
    await page.waitForTimeout(2_000);

    await beat(
      page,
      'note',
      "That's a real, short-lived token. Now register it with the agent's own MCP client — one " +
        "command, and it's connected to nothing but this service blueprint authoring surface.",
      { position: 'top' }
    );

    await typeInTerminal(
      page,
      'claude mcp remove prism-cms-service-blueprint 2>/dev/null; claude mcp add --transport http prism-cms-service-blueprint ' +
        `http://localhost:${port}/prism/service-blueprint-authoring/mcp ` +
        `--header "Authorization: Bearer $(jq -r .access_token ${tokenFilePath})"`
    );
    await page.waitForTimeout(2_000);
    await typeInTerminal(page, 'claude mcp list');
    await page.waitForTimeout(1_500);

    // Reading the same file the curl command really just wrote — not scraping the terminal's
    // rendered canvas (unreliable, per this repo's other recorded demos), just the real token the
    // real command really produced, for this script's own later polling.
    const tokenFileContents = JSON.parse(readFileSync(tokenFilePath, 'utf8')) as { access_token: string };
    mintedToken = tokenFileContents.access_token;

    // Fail the take in seconds, not after a 40-minute poll: the token the terminal just
    // registered with claude must complete a real MCP initialize against the same port.
    expect(
      probeMcpInitialize(port, mintedToken),
      `the minted token failed an MCP initialize against port ${port} — the agent would launch with no working tools`
    ).toBe(true);

    await beat(
      page,
      'recap',
      "Done — a real identity, a real token, a real MCP connection, entirely reproducible from " +
        "the command line. From here it works exactly like giving a new starter their login."
    );
  });

  test('Act 2 — hand over the brief', async ({ request }) => {
    // Real agent call: design + validate/fix + save, all in one turn. No live research needed
    // (unlike the removed pensions demo) since the domain is deliberately fictional, but a real
    // multi-step validate-simulate-save loop still reliably takes several minutes.
    // Real agent runs vary a lot in duration — one bisected the host's known gateway
    // when-condition unreliability (see project memory) extensively before falling back to
    // trigger-based routing, taking 26+ minutes; another took under 8. Budget generously.
    test.setTimeout(45 * 60_000);

    // Same guard as Act 1's end — don't let the agent launch against a connection that has
    // silently gone bad in between (expired token, restarted host).
    expect(
      probeMcpInitialize(mcpPort, mintedToken),
      `MCP initialize against port ${mcpPort} stopped working between acts — aborting before wasting an agent run`
    ).toBe(true);

    await showTerminalMirror(page);
    await handleBypassPermissionsGateIfShowing(page);
    await moveNarrationTo(page, 'top');

    await beat(
      page,
      'setup',
      "Now let's actually launch the agent — scoped to exactly this one MCP connection, nothing more.",
      { position: 'top' }
    );
    await typeInTerminal(
      page,
      // --model pinned explicitly: the spawned agent otherwise inherits whatever default the
      // operator's own claude config happens to have at the time — a take died mid-build when a
      // freshly-switched personal default model stalled after its first tool call. The demo's
      // agent should behave identically regardless of who records it.
      'claude --model sonnet --tools "mcp__prism-cms-service-blueprint__*,ListMcpResourcesTool,ReadMcpResourceDirTool,ReadMcpResourceTool" --permission-mode bypassPermissions'
    );
    await page.waitForTimeout(3_000);
    await handleBypassPermissionsGateIfShowing(page);

    await beat(
      page,
      'intent',
      "We'll hand it one brief: design and save a real transfer service — branching eligibility, a guidance checklist, document upload — entirely on its own.",
      { position: 'top' }
    );

    await sendTerminalText(brief, 10);
    await page.waitForTimeout(400);
    sendTerminalKey('Enter');

    await beat(
      page,
      'note',
      "It's checking what this host can actually render, reading the existing juggling-licence service blueprint as a style guide, then designing against the real contract.",
      { position: 'top', holdMs: 5_000 }
    );

    // The real completion signal is the saved definition itself, authenticated with the same
    // token Act 1 minted — not anything printed in the terminal (matching agent output text is
    // inherently fragile; the saved definition is the fact that can only become true for real).
    await expect.poll(
      async () => {
        const response = await request.get(
          `${testSiteOrigin}/umbraco/management/api/v1/prism/cms-service-blueprints/${serviceBlueprintKey}`,
          { ignoreHTTPSErrors: true, headers: { Authorization: `Bearer ${mintedToken}` } }
        );
        if (!response.ok()) return false;
        const definition = (await response.json()) as ServiceBlueprintDefinition;
        const componentTypes = collectComponentTypes(
          (definition.states ?? []).flatMap(s => s.components ?? [])
        );
        return (
          (definition.states?.length ?? 0) >= 10 &&
          definition.queues?.length === 1 &&
          componentTypes.has('file-upload') &&
          componentTypes.has('guidance-checklist')
        );
      },
      { timeout: 40 * 60_000, intervals: [10_000] }
    ).toBe(true);

    await beat(
      page,
      'recap',
      'And there it is — designed, validated, simulated, and saved to the live engine. No one wrote a line of this service blueprint by hand.',
      { position: 'top' }
    );

    // The recording is done with the terminal from here on — later acts are all backoffice/site.
    stopTerminalMirror();
  });

  const navLinkLabel = 'Transfer your licence';

  test('Act 3 — wire it into the live site', async () => {
    await page.goto(`${testSiteOrigin}/umbraco`);
    // Act 1 already logged in for real on this same shared page — its session cookie is still
    // live here, so the login form may not appear at all. Only fill it in if it's actually shown.
    const emailField = page.getByLabel('E-mail');
    if (await emailField.isVisible({ timeout: 5_000 }).catch(() => false)) {
      await humanType(page, emailField, adminCredentials.username);
      await humanType(page, page.locator('#password-input'), adminCredentials.password);
      await humanClick(page, page.getByRole('button', { name: 'Login' }));
      await page.waitForLoadState('networkidle', { timeout: 30_000 }).catch(() => {});
    }
    await expect(page.getByText('Home', { exact: true }).first()).toBeVisible({ timeout: 30_000 });

    await beat(page, 'setup', "Here's the Umbraco back office — this is where a service designer wires a new service into the real site.");
    await beat(page, 'intent', 'A page with a ServiceBlueprint Key connects it to the engine — the same one property every CMS ServiceBlueprint page uses.');

    await page.getByText('Home', { exact: true }).first().hover();
    await humanClick(page, page.getByRole('button', { name: 'Create item for Home' }));
    await humanClick(page, page.locator('uui-ref-node-document-type').filter({ hasText: 'CMS ServiceBlueprint Page' }));
    await page.waitForLoadState('networkidle', { timeout: 30_000 }).catch(() => {});

    await humanType(page, page.getByRole('textbox', { name: 'Enter a name...' }), 'Transfer your existing juggling licence');
    await humanType(page, page.getByLabel('ServiceBlueprint Key'), serviceBlueprintKey);

    await humanClick(page, page.getByRole('button', { name: 'Save and publish', exact: true }));
    await expect(page.getByRole('alert').getByText('Document published')).toBeVisible({ timeout: 15_000 });

    const publishedUrl = '/transfer-your-existing-juggling-licence/';
    const check = await page.request.get(publishedUrl, { ignoreHTTPSErrors: true });
    expect(check.ok(), `published page did not resolve at ${publishedUrl}`).toBeTruthy();

    await beat(
      page,
      'recap',
      "One page, one key, published — and it's already backed by the definition the agent just designed. No restart, no redeploy. Now a real navigation link, so a visitor can actually find it."
    );

    // TestSite's desktop nav (webNavLinks) is a fixed set seeded once by DemoMobileNavSeeder —
    // extending it is a code change, not a content one. The mobile nav (mobileNavLinks) is the
    // content-driven list a service designer actually adds to live, matching the reference demo's
    // own pattern — its <prism-mobile-nav> element renders real semantic links regardless of its
    // "mobile" styling, so Act 5 can find it the same way it would any other nav link.
    await page.goto(`${testSiteOrigin}/umbraco/section/content`);
    await humanClick(page, page.locator('uui-menu-item').filter({ hasText: 'Settings' }).first());
    await page.waitForLoadState('networkidle', { timeout: 30_000 }).catch(() => {});

    await humanClick(page, page.getByText('Add Mobile Nav Item').nth(1));
    await humanClick(page, page.getByText('Mobile Nav Item', { exact: true }));
    await humanType(page, page.getByLabel('Label'), navLinkLabel);
    await humanType(page, page.getByLabel('URL'), publishedUrl);
    await humanClick(page, page.getByRole('button', { name: 'Create', exact: true }));
    await page.getByRole('button', { name: 'Submit' }).click({ timeout: 5_000 }).catch(() => {});

    await humanClick(page, page.getByRole('button', { name: 'Save and publish', exact: true }));
    await expect(page.getByRole('alert').getByText('Document published')).toBeVisible({ timeout: 15_000 });

    await beat(page, 'recap', 'Published, and linked from the site navigation — no hardcoded URL, same as any other Prism service.');
  });

  test('Act 4 — review what it built', async () => {
    test.setTimeout(120_000);
    await beat(page, 'intent', "Let's open the editor and look at what it actually designed — the same editor a human would use to adjust any of this by hand.");

    // CMS ServiceBlueprint's editor is a backoffice workspace, not a standalone public route — unlike
    // MockBusinessApp's /service-blueprint-editor page, it lives under the Prism section
    // (UMB_CMS_SERVICE_BLUEPRINT_EDIT_PATH_PREFIX in src/backoffice/cms-service-blueprint/entity.ts), keyed by the
    // service blueprint's own definitionKey rather than a separate entity id.
    await page.goto(`${testSiteOrigin}/umbraco/section/prism/workspace/prism-cms-service-blueprint/edit/${serviceBlueprintKey}`);
    await page.locator('prism-service-blueprint-editor').waitFor({ state: 'attached', timeout: 30_000 });
    await page.waitForLoadState('networkidle', { timeout: 20_000 }).catch(() => {});

    await beat(page, 'note', "First the canvas — let's zoom to fit so we can see the whole graph.", { position: 'top' });
    await humanClick(page, page.getByRole('button', { name: 'Fit' }).first());
    await page.waitForTimeout(600);

    await beat(
      page,
      'note',
      "Every one of those nodes — the eligibility questions, the guidance stage, the upload " +
        "stage — is a real, editable state with its own routes. A service designer could add a " +
        "fourth eligibility question here without touching any code. The agent names its own " +
        "states each time it designs, so we won't script exact clicks against its wording — " +
        "let's look at the actual definition instead.",
      { position: 'top', holdMs: 5_000 }
    );

    // For a developer/implementation audience: the actual JSON contract behind the graph, not
    // just the visual canvas — the same "Definition" tab a human editor would use to inspect or
    // hand-tweak the raw definition.
    await beat(
      page,
      'setup',
      "Now the part an implementer will actually care about — the raw definition itself.",
      { position: 'top' }
    );
    await humanClick(page, page.locator('[data-prism-confidence-tab="definition"]'));
    await page.waitForTimeout(1_000);
    await beat(
      page,
      'note',
      "This is the exact ServiceBlueprintDefinitionFile JSON the agent saved — states, routes, and each " +
        "component's own properties. Notice the upload-evidence state's fields: file-upload " +
        "components with acceptedFileTypes and required flags, nothing bespoke. And the existing-" +
        "licence-details state's professional-category field carries a defaultFrom pointing at a " +
        "calculated membershipTier value — that's the hook a real member's data flows through, " +
        "which we'll see fire for real in a moment.",
      { position: 'top', holdMs: 5_000 }
    );

    await beat(
      page,
      'recap',
      'Branching eligibility, a guidance gate, real document upload, a member-data hook — every one of those is a normal, editable, inspectable part of the definition, not a special case.',
      { position: 'top' }
    );
  });

  test('Act 5 — run it as a visitor', async () => {
    test.setTimeout(7 * 60_000);
    const testFilesDir = path.join(__dirname, 'fixtures');

    await beat(page, 'setup', "Now let's actually run it — first anonymously, the way most applicants would.");
    await page.goto('/');
    await clickAndWaitForNavigation(page, page.getByRole('link', { name: navLinkLabel }));

    await beat(page, 'intent', "One eligibility question answered the ineligible way — to show the dead end is a real, tailored outcome, not just an error.");
    await answerEligibilityQuestion(page, false);
    await beat(page, 'recap', "A real, tailored explanation — not a generic validation message.");

    await beat(
      page,
      'note',
      "And a real \"Start again\" link — this service blueprint only ever keeps one active instance per visitor, so without this, a dead end here would be permanent. It genuinely creates a fresh instance, not just a page reload.",
      { holdMs: 5_000 }
    );
    await clickAndWaitForNavigation(page, page.locator('[data-prism-start-again]'));

    await beat(
      page,
      'setup',
      "Now let's sign in as a real member and go through it properly — that also proves this same service works just as well for a signed-in visitor as an anonymous one.",
      { position: 'top' }
    );
    // No need to clear cookies here: CmsServiceRequestVisitorIdentityResolver.Resolve() keys a
    // logged-in visitor's instance by their real member email, entirely independent of the
    // anonymous PrismCmsServiceRequestVisitor cookie — signing in below already gives this a genuinely
    // fresh identity (and so a fresh requestPolicy: "single" instance), exactly the way a real
    // visitor moving from anonymous browsing to a real account would.
    await page.goto('/');
    await humanClick(page, page.getByRole('link', { name: 'Sign In' }));
    await page.locator('#username').waitFor({ state: 'visible', timeout: 60_000 });
    await humanType(page, page.locator('#username'), 'demo@prism.local');
    await humanType(page, page.locator('#password'), 'password');
    await humanClick(page, page.locator('#kc-login'));
    await page.waitForLoadState('networkidle', { timeout: 30_000 }).catch(() => {});
    await page.waitForURL(url => !url.pathname.includes('signin-oidc'), { timeout: 30_000 }).catch(() => {});
    // The Keycloak round trip is a real cross-origin redirect — exactly where the recorded video
    // has been observed to lose focus and freeze for tens of seconds afterward, even though the
    // underlying page keeps working correctly. Force focus back explicitly right after it.
    await page.bringToFront();

    await beat(page, 'recap', 'Signed in for real via Keycloak — the same visitor identity every other part of this site already uses.', { position: 'top' });

    await page.goto('/');
    await clickAndWaitForNavigation(page, page.getByRole('link', { name: navLinkLabel }));

    for (let round = 0; round < 3; round++) {
      // The agent renders eligibility questions as either a radio group or a plain Yes/No button
      // pair depending on the run (both are valid GDS patterns, and the exact trigger key varies
      // too — "yes"/"no" one run, "confirm-yes"/"confirm-no" another) — gating on "has a radio"
      // alone silently skipped every button-only question, leaving it unanswered and letting the
      // subsequent generic-fill loop click whatever default action came first (sometimes "no"),
      // landing on the ineligible dead-end instead of answering "yes" as intended. Detect an
      // eligibility question by matching its actual button text/values loosely, the same way
      // answerEligibilityQuestion itself does, rather than assuming a specific input shape.
      const actionValues = await page.locator('button[name="Action"]').evaluateAll(
        buttons => buttons.map(b => `${b.textContent ?? ''} ${b.getAttribute('value') ?? ''}`)
      );
      const hasEligibilityQuestion =
        (await page.locator('input[type="radio"][name^="fields["]').count()) > 0 ||
        (actionValues.some(v => /yes/i.test(v)) && actionValues.some(v => /no/i.test(v)));
      if (hasEligibilityQuestion) {
        await answerEligibilityQuestion(page, true);
      }
    }

    await beat(page, 'note', "Here's the guidance checklist — it won't let us continue until every article is acknowledged.");
    await fillGdsFormGenerically(page, testFilesDir);
    await clickAndWaitForNavigation(page, primaryActionButton(page));

    await beat(
      page,
      'note',
      "And here's the member-data hook from the definition we looked at earlier, firing for real: " +
        "professional category is already filled in — pulled from this signed-in member's real " +
        "Juggling Society membership tier the moment the page rendered, not typed by us.",
      { holdMs: 5_000 }
    );
    await fillGdsFormGenerically(page, testFilesDir);
    await clickAndWaitForNavigation(page, primaryActionButton(page));

    await beat(page, 'note', "Now the upload stage — five real file fields. Let's actually attach real files to each of them.");
    await fillGdsFormGenerically(page, testFilesDir);
    await clickAndWaitForNavigation(page, primaryActionButton(page));

    await beat(
      page,
      'note',
      "Check your answers — and notice the uploaded files show a real \"View\" link, not just a " +
        "filename. That's a genuine download endpoint backed by real disk storage, ownership-" +
        "checked against this same visitor identity.",
      { holdMs: 5_000 }
    );
    const firstChangeButton = page.getByRole('button', { name: /^Change/i }).first();
    if (await firstChangeButton.count() > 0) {
      await clickAndWaitForNavigation(page, firstChangeButton);
      await beat(page, 'note', "A real \"Change\" link — a genuine round trip to an earlier stage and back, not just a static filename.", { position: 'top' });
    }

    // However many hops the Change round-trip and Declaration actually take (the agent's own
    // routing, not something to hardcode a hop-count against) — keep filling and continuing
    // generically until either the real confirmation panel appears or something needs attention.
    await beat(page, 'note', "Filling in the rest — including Declaration — and submitting for real.");
    for (let step = 0; step < 6; step++) {
      if (await page.locator('.govuk-panel').count() > 0) break;
      const errorCount = await page.locator('.govuk-error-summary').count();
      if (errorCount > 0) break;
      await fillGdsFormGenerically(page, testFilesDir);
      await clickAndWaitForNavigation(page, primaryActionButton(page));
    }

    await expect(page.locator('.govuk-panel')).toBeVisible({ timeout: 15_000 });
    await beat(
      page,
      'recap',
      'Branching eligibility, a real acknowledgement gate, real document upload, a genuine member-data default, an editable review step — designed and saved by an agent that only ever spoke MCP, running exactly as a real applicant would experience it.'
    );
  });

  test('Closing slate', async () => {
    await showSlate(page, {
      eyebrow: 'UMBRACO PRISM',
      title: "That's the whole loop",
      body:
        'A complex, multi-capability public service — branching logic, gated guidance, real ' +
        'document upload, a live member-data default, an editable review step — designed and ' +
        'saved from one conversation and one access token, then wired into a real site by a ' +
        'human in minutes. Thanks for watching.'
    });
  });
});
