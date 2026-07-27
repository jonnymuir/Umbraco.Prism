import { test, expect, type Page } from '@playwright/test';
import { execFileSync } from 'node:child_process';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { signIn, resetServiceBlueprints, businessAppOrigin } from '../walkthroughs/support/walkthrough';
import { beat, showSlate, clearSlate, moveNarrationTo, startNarrationTimeline, getNarrationTimeline } from './support/narration';
import { humanClick, humanType } from './support/human-interactions';

// Playwright's automatic video-to-test-result attachment only works for pages scoped to a single
// test (the built-in page/context fixtures). This spec deliberately shares ONE page across every
// act via beforeAll/afterAll — not for narrative continuity alone, but because Playwright records
// one video per page: as long as nothing ever opens a second page, "Act 6" is just a later
// timestamp in the same file as "Act 1", not a separate clip that needs stitching together
// afterward. Saving explicitly in afterAll is the only way to get that one file back.
const __dirname = path.dirname(fileURLToPath(import.meta.url));
const footageDir = path.join(__dirname, '..', '..', 'demo-footage');
mkdirSync(footageDir, { recursive: true });

function tryConvertToMp4(webmPath: string): void {
  // Best-effort convenience: .webm plays fine in a browser or VLC, but Keynote/PowerPoint support
  // is patchy. If ffmpeg happens to be on PATH, also emit an .mp4 alongside; if not, say so and
  // move on — the .webm is still the real deliverable either way.
  const mp4Path = webmPath.replace(/\.webm$/, '.mp4');
  try {
    // crf 20 was a reasonable default for the previous ~800x450 source, but that resolution was
    // the actual cause of "grainy" video (see recordVideo.size above) — now that the source is
    // real 1920x1080, crf 18 (near-visually-lossless) is worth the larger file to not throw detail
    // away twice.
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
//   TTYD_PASSWORD=<password> npm run demo:record
// See tests/demo/README.md for the full operator setup (warming the stack, ttyd, etc.).
//
// One test() per act inside a serial describe block, sharing a page created once in beforeAll:
// a failed later act still leaves everything recorded so far on disk (afterAll always runs), and
// the page's own state (logins, open tabs' worth of context) persists across acts the way a real
// browser session would.

const serviceBlueprintKey = 'garden-waste-permit';
const adminCredentials = { username: 'admin@prism.local', password: 'PrismLocal!12345' };
const ttydUrl = process.env.TTYD_URL ?? 'http://127.0.0.1:7681';
const claudeSessionLogPath = '/tmp/claude-session.log';

// simulate_serviceBlueprint's `calculations` is a parallel array, one entry per trace step — `null` for
// any step that doesn't produce a fresh calculation (confirmed live: a step routed through a
// gateway can add an extra trace entry for the passthrough with no calc of its own, making the
// *last* entry null even though the calculation from the actual field-bearing step is very much
// present one entry earlier). Blindly taking `.at(-1)` intermittently returned undefined depending
// on the agent-authored gateway shape — search backward for the last non-null entry instead.
function lastCalculatedFields(calculations: unknown): Record<string, unknown> {
  const entries = (calculations as Array<{ fields?: Record<string, unknown> } | null> | undefined) ?? [];
  for (let i = entries.length - 1; i >= 0; i--) {
    if (entries[i]) {
      return entries[i]!.fields ?? {};
    }
  }
  return {};
}

// Used at every visit to the terminal (Act 4 and Act 7) — ttyd's launch command (see README) wraps
// the claude process in `tmux new-session -A`, so the SAME conversation survives across visits:
// ttyd spawns a fresh client connection each time, but tmux reattaches to the session already
// running rather than starting a new `claude` process. That also means the one-time
// "Claude Code running in BypassPermissions mode... 1. No, exit  2. Yes, I accept" consent gate can
// only ever appear on the very FIRST visit, when the tmux session doesn't exist yet — and on some
// Claude Code versions it doesn't appear at all. Blindly sending "2"+Enter on every visit typed "2"
// straight into a live, empty prompt box and confused the agent (observed live) when the gate
// wasn't showing. Check the tail of the session log for the gate's own text before answering it.
async function connectToClaudeTerminal(page: Page): Promise<void> {
  await page.goto(ttydUrl, { waitUntil: 'networkidle', timeout: 15_000 });
  await page.waitForTimeout(2_000);
  await humanClick(page, page.locator('.xterm-screen'));

  await page.waitForTimeout(500);
  let recentLog = '';
  try {
    // Tail only — checking the whole (possibly large, multi-visit) log risks a false match against
    // something the agent printed earlier in the conversation rather than the gate itself.
    recentLog = readFileSync(claudeSessionLogPath, 'utf8').slice(-4000);
  } catch {
    // No log yet on the very first connection before `script` has flushed anything — fine, means
    // there's nothing to detect either way.
  }
  if (/Yes, I accept|No, exit/i.test(recentLog)) {
    await page.keyboard.press('2');
    await page.keyboard.press('Enter');
    await page.waitForTimeout(1_000);
  }
}

test.describe.serial('garden waste permit demo', () => {
  test.beforeAll(async ({ request }) => {
    await resetServiceBlueprints(request);
  });

  let page: Page;
  let publishedUrl: string;

  test.beforeAll(async ({ browser }) => {
    // Fail fast, before recording a single frame, rather than discovering the missing password
    // partway through a take (TTYD_PASSWORD is needed for the whole session now, not just Act 4,
    // since the browser context — and its one continuous video — is created here up front).
    if (!process.env.TTYD_PASSWORD) {
      throw new Error('Set TTYD_PASSWORD to the password used when launching ttyd — see README.md.');
    }

    // recordVideo isn't inherited from the config's `use.video` for a manually-created context the
    // way baseURL/ignoreHTTPSErrors are — confirmed live: page.video() came back null without this.
    // httpCredentials.origin scopes the Basic Auth challenge response to ttyd's origin only, so the
    // other three origins (TestSite, MockBusinessApp, Keycloak) never see an Authorization header.
    // recordVideo.size is NOT optional if you actually want viewport-resolution footage: per
    // Playwright's own docs, omitting it scales the recording down to fit inside 800x800 — for a
    // 1920x1080 viewport that's ~800x450, a good deal blurrier than it looks live. This was the
    // actual cause of "grainy on my screen", not an under-tuned ffmpeg encode.
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
    // Video capture starts the moment this page exists — starting the timeline here (not at the
    // first beat) is what makes its atMs values line up with the actual output file's timeline.
    startNarrationTimeline();

    // Bigger text for the ttyd terminal (Act 4), and why this specific mechanism:
    // - The ttyd server's own `-t fontSize=` flag (tried first) destabilizes xterm.js's
    //   column/reflow math once real streamed content arrives — reproduced reliably as corrupted,
    //   overlapping text.
    // - CSS `zoom` on <html> (tried second) fails two different ways: (a) applied via
    //   page.evaluate() *after* page.goto/networkidle, it's too late — ttyd/xterm negotiate a
    //   column count with the server exactly once, at connection time, so the PTY keeps the old
    //   (wider, pre-zoom) column count and long lines overflow off the right edge instead of
    //   wrapping; (b) even registered early via addInitScript, document.documentElement doesn't
    //   exist yet at the point addInitScript fires (confirmed live: the assignment throws
    //   "Cannot read properties of null" and is silently swallowed) — and even once fixed to apply
    //   on DOMContentLoaded instead, zoom on the root element shrinks the whole terminal into a
    //   small corner of the viewport rather than enlarging it (a real, reproduced Chromium zoom
    //   quirk), which is visually worse than the original bug.
    // - What actually works: ttyd assigns its live xterm.js Terminal instance to the global
    //   `window.term` (confirmed by reading ttyd's own bundled JS) synchronously, *before* it
    //   calls fitAddon.fit() to compute columns from the container/font metrics. Hooking that one
    //   assignment with a property setter lets us bump `term.options.fontSize` in between — the
    //   same option xterm's own renderer and its own column/reflow math both read, so both stay in
    //   sync (unlike the CLI-flag route, which reportedly doesn't). Confirmed against a full-length
    //   real prompt: correct wrapping, no overflow, no corner-shrink, genuinely larger glyphs.
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
      // saveAs() copies rather than moves — delete the original auto-hash-named recording
      // afterward, or every run leaves a duplicate multi-MB file behind in footageDir.
      const finalPath = path.join(footageDir, 'garden-waste-permit-demo.webm');
      await video.saveAs(finalPath);
      await video.delete();
      tryConvertToMp4(finalPath);
      // The line-by-line script + timestamps a voiced-narration pass (recording v2) needs —
      // see support/narration.ts's timeline tracking.
      writeFileSync(
        path.join(footageDir, 'narration-timeline.json'),
        JSON.stringify(getNarrationTimeline(), null, 2)
      );
    }
  });

  test('Cold open — introduce the demo', async () => {
    await showSlate(page, {
      eyebrow: 'UMBRACO PRISM',
      title: 'Standing up a new council service in minutes',
      body:
        "We're going to show you how easy it is to build a real service on Prism's service blueprint " +
        'engine. Our system of record is a mock business application; our AI tooling is Claude. ' +
        "We'll move across the Umbraco back office, the business app's service blueprint admin, the " +
        'service blueprint editor, and the Claude CLI — to build a garden waste collection permit for a ' +
        'local council.',
      // The word-count-based default comfortably exceeds this for a paragraph this length —
      // explicit override rather than trimming the copy itself.
      holdMs: 15_000
    });
    await clearSlate(page);
  });

  test('Act 1 — create the Umbraco page and wire up a real nav link', async () => {
    // Selectors below were confirmed live against a real running backoffice, not guessed —
    // see the "Act 1 manual-fallback cue sheet" in README.md if any of these prove fragile on
    // a different Umbraco/UUI version.
    await page.goto('/umbraco');
    await humanType(page, page.getByLabel('E-mail'), adminCredentials.username);
    await humanType(page, page.locator('#password-input'), adminCredentials.password);
    await humanClick(page, page.getByRole('button', { name: 'Login' }));
    await page.waitForLoadState('networkidle', { timeout: 30_000 }).catch(() => {});
    await expect(page.getByText('Home', { exact: true }).first()).toBeVisible({ timeout: 30_000 });

    await beat(page, 'setup', "Here's the Umbraco back office — this is where a service designer starts.");
    await beat(
      page,
      'intent',
      "We're going to create a page for our new service, and give it a ServiceBlueprint Key — the one " +
        'property that connects it to the runtime engine.'
    );

    // "Home" allows serviceBlueprintPage/serviceBlueprintHub/memberDashboard as children (see
    // PrismContentTypeSeeder.EnsureHomeAllowedChildrenAsync). The "+" button only renders on
    // hover — hovering the row first is required, not just a visual nicety.
    await page.getByText('Home', { exact: true }).first().hover();
    await humanClick(page, page.getByRole('button', { name: 'Create item for Home' }));
    await humanClick(page, page.locator('uui-ref-node-document-type').filter({ hasText: 'ServiceBlueprint Page' }));
    await page.waitForLoadState('networkidle', { timeout: 30_000 }).catch(() => {});

    await humanType(page, page.getByRole('textbox', { name: 'Enter a name...' }), 'Garden Waste Permit');
    await humanType(page, page.getByLabel('ServiceBlueprint Key'), serviceBlueprintKey);

    await humanClick(page, page.getByRole('button', { name: 'Save and publish', exact: true }));
    await expect(page.getByRole('alert').getByText('Document published')).toBeVisible({ timeout: 15_000 });

    publishedUrl = `/${serviceBlueprintKey}/`;
    // Confirm the guessed slug actually resolves rather than assuming it silently.
    const check = await page.request.get(publishedUrl, { ignoreHTTPSErrors: true });
    expect(check.ok(), `published page did not resolve at ${publishedUrl}`).toBeTruthy();

    await beat(
      page,
      'recap',
      'One content page, one key, and Garden Waste Permit is already wired to the service blueprint ' +
        "engine. Now let's give it a real navigation link so a visitor can actually find it."
    );

    // "Settings" is ambiguous by plain text (also the top-nav section label) — scope to the
    // content-tree menu item specifically.
    await page.goto('/umbraco/section/content');
    await humanClick(page, page.locator('uui-menu-item').filter({ hasText: 'Settings' }).first());
    await page.waitForLoadState('networkidle', { timeout: 30_000 }).catch(() => {});

    await humanClick(page, page.getByText('Add Mobile Nav Item').nth(1));
    await humanClick(page, page.getByText('Mobile Nav Item', { exact: true }));
    await humanType(page, page.getByLabel('Label'), 'Garden Waste Permit');
    await humanType(page, page.getByLabel('URL'), publishedUrl);
    await humanClick(page, page.getByRole('button', { name: 'Create', exact: true }));
    // The "Add content" drawer's Submit button may or may not still be open here depending on
    // timing — bounded click+catch avoids the check-then-act race of isVisible() followed by a
    // separate click (the button can detach between the two).
    await page.getByRole('button', { name: 'Submit' }).click({ timeout: 5_000 }).catch(() => {});

    await humanClick(page, page.getByRole('button', { name: 'Save and publish', exact: true }));
    await expect(page.getByRole('alert').getByText('Document published')).toBeVisible({ timeout: 15_000 });

    await beat(page, 'recap', 'Published, and linked from the site navigation — no hardcoded URL.');
  });

  test('Act 2 — add the new service via the service blueprint admin dashboard', async () => {
    await page.goto(`${businessAppOrigin}/admin/service-desk`);
    await page.waitForLoadState('networkidle', { timeout: 15_000 }).catch(() => {});

    await beat(
      page,
      'setup',
      "This is the mock business app's service blueprint admin — every authored service, plus who's " +
        "queued up in each one."
    );
    // The "Add a new service" form sits at the bottom of this page — right where the bar was
    // about to describe it from. Move up before drawing attention to (and filling in) that form.
    await beat(
      page,
      'intent',
      "We'll add our new service here, then jump straight into the editor to build it.",
      { position: 'top' }
    );

    await humanType(page, page.getByLabel('Definition key'), serviceBlueprintKey);
    await humanType(page, page.getByLabel('Display name'), 'Garden Waste Permit');
    await humanClick(page, page.getByRole('button', { name: 'Create service blueprint' }));

    // The create form is a plain HTML POST (no client JS), so submitting it is a real navigation
    // — the server scaffolds a zero-states shell (initialStage left blank; the graph's own "add
    // stage" affordance fills it in the moment a first stage is created) and redirects straight
    // into the editor for it.
    await page.waitForURL(/service-blueprint-editor/, { timeout: 15_000 });
  });

  test('Act 3 — build the first stage by hand in the editor', async () => {
    await page.goto(`${businessAppOrigin}/service-blueprint-editor?service blueprint=${serviceBlueprintKey}`);
    await expect(page.locator('[data-prism-component="service-blueprint-editor-shell"]'))
      .toHaveAttribute('data-prism-active-service-blueprint', serviceBlueprintKey, { timeout: 30_000 });
    await expect(page.locator('prism-service-blueprint-editor'))
      .toHaveAttribute('data-prism-service-blueprint-loaded', serviceBlueprintKey, { timeout: 30_000 });

    await beat(page, 'setup', 'This is the visual service blueprint editor — stages and routes are fully UI-authorable today.');
    await beat(
      page,
      'intent',
      "We'll build the first stage by hand: a simple question asking how many bins the resident has."
    );

    // Empty-canvas "Create stage" affordance (tests/service-blueprint-editor/service-blueprint-graph-keyboard.spec.ts).
    await humanClick(page, page.locator('[data-prism-empty-add-stage]'));
    const dialog = page.locator('[data-prism-create-stage-dialog]');
    await humanType(page, dialog.locator('[data-prism-create-stage-title]'), 'How many bins do you have?');
    await humanType(page, dialog.locator('[data-prism-create-stage-key]'), 'how-many-bins');
    await humanType(page, dialog.locator('[data-prism-create-stage-queue]'), 'web-user');
    await humanClick(page, dialog.getByRole('button', { name: 'Create stage' }));
    await expect(dialog).toBeHidden();
    await expect(page.locator('[data-prism-stage="how-many-bins"]')).toBeVisible();

    await beat(
      page,
      'recap',
      "That's stage one — but it doesn't go anywhere yet. That's deliberate: we're about to hand " +
        'this off to an AI agent to build the rest.'
    );

    await beat(page, 'note', "Let's zoom to fit so we can see the whole graph.");
    await humanClick(page, page.getByRole('button', { name: 'Fit' }));
    await page.waitForTimeout(400);

    // prism-definition-editor lives inside TWO nested shadow roots (service-blueprint-editor-shell →
    // service-blueprint-editor → definition-editor) — Playwright's own locators auto-pierce open shadow
    // roots, but a raw page.evaluate() callback runs plain DOM APIs that don't, so the walk has
    // to be explicit here. Off-camera plumbing (adding the one input field) — not worth narrating
    // keystroke by keystroke.
    await page.locator('[data-prism-confidence-tab="definition"]').click();
    // prism-definition-editor is lazy-loaded — wait for it to actually mount rather than a fixed
    // sleep, since first-load JS-chunk fetch time varies (this raced and failed intermittently
    // with a fixed 500ms wait).
    await page.waitForFunction(() => {
      const shell = document.querySelector('prism-service-blueprint-editor-shell');
      const editor = shell?.shadowRoot?.querySelector('prism-service-blueprint-editor');
      return !!editor?.shadowRoot?.querySelector('prism-definition-editor');
    }, { timeout: 15_000 });
    await page.evaluate(({ key, field }) => {
      const shell = document.querySelector('prism-service-blueprint-editor-shell')!;
      const editor = shell.shadowRoot!.querySelector('prism-service-blueprint-editor')!;
      const def = editor.shadowRoot!.querySelector('prism-definition-editor') as unknown as { value: string };
      const current = JSON.parse(def.value);
      const stage = current.states.find((s: { stateKey: string }) => s.stateKey === key);
      stage.components = [
        // `default` matters beyond seeding the form: validate_serviceBlueprint has no submitted values to
        // work with, so a calculation the agent later writes against `binCount` (in Act 4) can only
        // be statically verified if this field has one — see docs/guides/calculation-language.md's
        // "validate_serviceBlueprint has no submitted values" section. Omitting it doesn't break the
        // *runtime*, but it's exactly the trap that sends an agent chasing a phantom validator bug.
        { type: 'number', fieldKey: field, label: 'How many bins do you have?', required: true, min: 1, default: '1' }
      ];
      def.value = JSON.stringify(current);
      def.dispatchEvent(new CustomEvent('definition-input', { detail: { value: def.value }, bubbles: true, composed: true }));
    }, { key: 'how-many-bins', field: 'binCount' });
    await page.waitForTimeout(600); // 250ms auto-apply debounce, plus margin

    // The Definition tab has no save button of its own ("Edits apply when valid" — auto-applies
    // to editor state); the actual persist-to-server Save button lives back on the Canvas tab.
    await page.locator('[data-prism-confidence-tab="canvas"]').click();
    await humanClick(page, page.getByRole('button', { name: 'Save', exact: true }));
    await page.waitForTimeout(1_000);

    await page.locator('[data-prism-confidence-tab="validation"]').click();
  });

  test('Act 4 — hand off to the agent over MCP', async ({ request }) => {
    // Real agent call, not mocked, doing real iterative debugging (validate → fix → re-validate,
    // sometimes cross-checking other seeded service blueprints to isolate a tool-usage mistake) — observed
    // live to need well over the initial 150s poll budget.
    test.setTimeout(12 * 60_000);

    await connectToClaudeTerminal(page);

    // Introduce this act only once the CLI is actually the thing on screen — saying "this is the
    // Claude CLI" while still looking at the editor read as disconnected from what's shown.
    await beat(
      page,
      'setup',
      'This is the Claude CLI, connected to our mock business app through nothing but the MCP ' +
        'toolkit Prism ships — no special access, no shortcuts.'
    );

    await beat(
      page,
      'intent',
      "We'll ask it to read the service blueprint we just started, add a second stage that calculates the " +
        'collection fee, and route the first stage into it — completely on its own.'
    );

    // The bar's been at the bottom for every beat so far — right where the CLI's own prompt line
    // sits, and about to have real typed text streaming under it. Slide it out of the way before
    // that starts, rather than covering the thing the audience most needs to read in this act.
    await moveNarrationTo(page, 'top');

    // This act's job is authoring, not proving the math — the agent designs and saves the
    // second stage (a real calculations block + a component that renders it, gateway-routed
    // from the first stage). Whether the fee is actually correct on screen is Act 6's job: a
    // real signed-in TestSite user submitting a bin count and seeing the live engine render it.
    // Asking the agent to also call simulate_serviceBlueprint and read back the numbers here conflates
    // the two beats, so it's deliberately not part of this prompt.
    const prompt = [
      "I'm designing a garden waste collection permit service (definitionKey: garden-waste-permit).",
      'There is already a first stage "how-many-bins" capturing a number field "binCount".',
      'Read the service blueprint, then add a second stage "collection-fee" that calculates the fee:',
      '£40 base charge plus £10 per bin over 2, capped at £120 — and include a visible component',
      'that actually displays the calculated fee (a calculation with nothing rendering it is',
      'invisible to the user). Route the first stage through to it via a gateway. Validate before',
      'you save, and fix anything it flags, then save the service blueprint.'
    ].join(' ');

    // Typed at a legible, deliberately human pace for an audience actually reading along —
    // faster than this and the on-screen prompt is gone before anyone's finished the first line.
    await page.keyboard.type(prompt, { delay: 45 });
    await page.waitForTimeout(300);
    await page.keyboard.press('Enter');

    // The real completion signal is the saved definition itself, not anything printed in the
    // terminal — the terminal is just the visual of the agent working. Poll the same
    // read_serviceBlueprint endpoint the agent itself calls, via the plain REST toolkit (not MCP), for a
    // second state carrying a non-empty `calculations` block. That's the one fact that can only
    // become true via a real save_serviceBlueprint call reaching the live engine.
    await expect.poll(
      async () => {
        const response = await request.get(
          `${businessAppOrigin}/prism/service-blueprint-authoring/service-blueprints/${serviceBlueprintKey}`,
          { ignoreHTTPSErrors: true }
        );
        if (!response.ok()) return false;
        const definition = await response.json();
        return (
          definition.states?.some((s: { stateKey: string }) => s.stateKey === 'collection-fee') &&
          definition.calculations?.fields &&
          Object.keys(definition.calculations.fields).length > 0
        );
      },
      { timeout: 540_000, intervals: [3_000] }
    ).toBe(true);

    await beat(
      page,
      'recap',
      'And there it is — the agent read the definition, wrote a real calculation, validated it, ' +
        'fixed what it flagged, and saved it back to the live engine.',
      { position: 'top' }
    );
  });

  test('Act 5 — go back to the editor and see what it built', async () => {
    await beat(page, 'intent', "Let's go back to the editor and see what it actually built — no restart, no redeploy.");

    await page.goto(`${businessAppOrigin}/service-blueprint-editor?service blueprint=${serviceBlueprintKey}`);
    await expect(page.locator('[data-prism-component="service-blueprint-editor-shell"]'))
      .toHaveAttribute('data-prism-active-service-blueprint', serviceBlueprintKey, { timeout: 30_000 });
    await expect(page.locator('prism-service-blueprint-editor'))
      .toHaveAttribute('data-prism-service-blueprint-loaded', serviceBlueprintKey, { timeout: 30_000 });

    await page.locator('[data-prism-confidence-tab="canvas"]').click();
    await expect(page.locator('[data-prism-stage="how-many-bins"]')).toBeVisible();
    await expect(page.locator('[data-prism-stage="collection-fee"]')).toBeVisible();

    await beat(page, 'note', "Let's zoom to fit and see the whole graph.");
    await humanClick(page, page.getByRole('button', { name: 'Fit' }));
    await page.waitForTimeout(400);

    await beat(
      page,
      'recap',
      "Both stages, the gateway between them, and the calculation the agent wrote — all there.",
      { position: 'top' }
    );

    // Point at the specific thing the agent built, not just the overall shape — selecting it
    // populates the Properties panel with its real details.
    await beat(page, 'intent', "Here's the new stage it added — Collection Fee.", { position: 'top' });
    await humanClick(page, page.locator('[data-prism-stage="collection-fee"]'));
    await page.waitForTimeout(600);
    await beat(
      page,
      'recap',
      'A question in, a fee calculated, and a gateway routing straight from the first stage into it.',
      { position: 'top' }
    );

    // Definition tab: show the calculations block the agent actually wrote, not just the canvas
    // shape — the maths itself is the point of this whole toolkit. Switch tabs *before* narrating
    // what's on it — saying "here's the calculation" while still looking at the canvas is the
    // same mismatch the Act 4 CLI intro had before it was reordered.
    await page.locator('[data-prism-confidence-tab="definition"]').click();
    await page.waitForFunction(() => {
      const shell = document.querySelector('prism-service-blueprint-editor-shell');
      const editor = shell?.shadowRoot?.querySelector('prism-service-blueprint-editor');
      return !!editor?.shadowRoot?.querySelector('prism-definition-editor');
    }, { timeout: 15_000 });
    await page.waitForTimeout(800);
    await beat(page, 'intent', "And here's the actual calculation it wrote — not just the shape, the real maths.");
    const hasCalculations = await page.evaluate(() => {
      const shell = document.querySelector('prism-service-blueprint-editor-shell')!;
      const editor = shell.shadowRoot!.querySelector('prism-service-blueprint-editor')!;
      const def = editor.shadowRoot!.querySelector('prism-definition-editor') as unknown as { value: string };
      const parsed = JSON.parse(def.value);
      return Boolean(parsed.calculations?.fields && Object.keys(parsed.calculations.fields).length > 0);
    });
    expect(hasCalculations, 'reloaded definition should carry the calculations block the agent wrote').toBeTruthy();

    await beat(
      page,
      'recap',
      '£40 base charge, £10 for every bin over two, capped at £120 — exactly what we asked for, ' +
        "sitting right there in the service blueprint's own calculations block."
    );
  });

  test('Act 6 — run it as a real logged-in user', async ({ request }) => {
    // Ground truth from the engine itself, independent of the UI: whatever field name/label the
    // agent chose, this is the fee a real submission of 5 bins must produce. Comparing the live
    // page against this (rather than a hardcoded "£70") keeps the assertion honest across
    // different agent runs that all correctly implement the same formula.
    const currentDefinition = await (
      await request.get(`${businessAppOrigin}/prism/service-blueprint-authoring/service-blueprints/${serviceBlueprintKey}`, { ignoreHTTPSErrors: true })
    ).json();
    // The agent chooses its own route trigger each run (empty string, "continue", "submit", ...)
    // — discover the real one from the initial render's own availableActions rather than
    // guessing, so this stays correct across different (equally valid) agent-authored shapes.
    const initialRender = await (
      await request.post(`${businessAppOrigin}/prism/service-blueprint-authoring/service-blueprints/simulate`, {
        ignoreHTTPSErrors: true,
        data: { serviceBlueprint: currentDefinition, steps: [] }
      })
    ).json();
    const realActionKey = initialRender.trace?.[0]?.render?.availableActions?.[0]?.actionKey ?? '';

    const simulation = await (
      await request.post(`${businessAppOrigin}/prism/service-blueprint-authoring/service-blueprints/simulate`, {
        ignoreHTTPSErrors: true,
        data: { serviceBlueprint: currentDefinition, steps: [{ action: realActionKey, fieldValues: { binCount: 5 } }] }
      })
    ).json();
    const feeFields = lastCalculatedFields(simulation.calculations);
    const expectedFee = feeFields.fee ?? Object.values(feeFields)[0];
    expect(expectedFee, 'could not determine the expected fee from a direct simulate_serviceBlueprint call').not.toBeUndefined();

    await beat(page, 'setup', "Now let's be an actual resident.");
    await signIn(page);

    await beat(page, 'intent', 'We\'ll click the real nav link from Act 1 — the way anyone actually would — and submit a bin count.');

    // The real point of Act 1's nav-link beat: reach the page by clicking the link a real user
    // would use, not a direct URL.
    await humanClick(page, page.getByRole('link', { name: 'Garden Waste Permit' }));
    await page.waitForLoadState('networkidle', { timeout: 15_000 }).catch(() => {});
    // networkidle doesn't mean *rendered* — GOV.UK Frontend's Transport webfont loads with
    // font-display behaviour that keeps text invisible for the first render of a page, which
    // reliably produced a ~1s flash of a fully-laid-out but completely textless page right here
    // (confirmed by frame-by-frame inspection). Wait for the real heading instead of guessing at
    // a fixed delay.
    await expect(page.getByRole('heading', { name: /how many bins/i })).toBeVisible({ timeout: 10_000 });

    // A breath before typing — this is the exact first stage from Act 3, reached the way a real
    // resident actually would, not a direct URL. Worth a beat of its own rather than rushing
    // straight into filling the field.
    await beat(page, 'note', "This is the exact question we built by hand — reached the way a real resident actually would.");

    await humanType(page, page.getByLabel(/how many bins/i), '5');
    await page.waitForTimeout(500);
    // The agent-authored route's trigger/label may be blank (an empty-string action key) — fall
    // back from a named continue/submit button to the sole form button if nothing named matches.
    const namedSubmit = page.getByRole('button', { name: /continue|submit|next/i });
    if (await namedSubmit.count() > 0) {
      await humanClick(page, namedSubmit.first());
    } else {
      await humanClick(page, page.locator('form button[type="submit"], form button').first());
    }

    await expect(page.getByText(String(expectedFee))).toBeVisible({ timeout: 15_000 });
    await beat(
      page,
      'recap',
      `And there's the calculated fee — £${expectedFee}.`
    );
    // The payoff: connect this number explicitly back to the calculations block Act 5 showed in
    // the Definition tab, rather than leaving "computed live by the engine" as an unexplained
    // assertion — restates the actual formula we asked the agent for, not the intermediate
    // arithmetic (which would need recomputing dynamically to stay honest across different agent
    // runs; the formula itself is fixed by the prompt, so it's safe to state directly).
    await beat(
      page,
      'recap',
      "That's not a coincidence — it's the exact calculation we watched the agent write: £40 base " +
        'charge, plus £10 for every bin over two, capped at £120. Same formula, same engine, now ' +
        'run for a real submission instead of a preview.'
    );
  });

  test('Act 7 — go back to the agent to refine the design', async ({ request }) => {
    // Same budget rationale as Act 4 — one real agent call, iterative validate/fix included.
    test.setTimeout(12 * 60_000);

    // Same tmux session as Act 4 (see connectToClaudeTerminal) — this really is a second turn in
    // the same conversation, not a fresh agent invocation, even though the browser navigated away
    // to the editor and back to the live site in between.
    await connectToClaudeTerminal(page);

    await beat(
      page,
      'setup',
      "This is the same Claude session from earlier — we've been to the editor and run it as a " +
        "resident since, but the conversation's still right here.",
      { position: 'top' }
    );

    await beat(
      page,
      'intent',
      "Now let's build it out further: a proper address question, and a review screen before the " +
        'fee — the kind of back-and-forth a real service design actually goes through.',
      { position: 'top' }
    );

    await moveNarrationTo(page, 'top');

    // Deliberately specific about summary-list mechanics, not just "make it nicer": an earlier
    // version of this prompt asked the agent to break the fee into a summary-list of base
    // charge/per-bin surcharge/cap — technically valid, but nonsensical UX (a resident can't
    // sensibly "change" a calculated intermediate value). The fix is in the toolkit itself now —
    // ServiceBlueprintDefinitionFile.ValidateDataDisplayBindings() and each summary-list child's own
    // ChangeStateKey (UmbracoPrism.Shared/Models/ServiceBlueprint/Components/InputComponents.cs) — but the
    // agent still needs to be told to use it correctly: review CAPTURED INPUTS with working
    // per-row Change links, and show the CALCULATED fee separately via stat-group.
    const refinementPrompt = [
      "That's working well. Let's refine it based on what we've learned: first, split the property",
      'address into its own stage called "property-address" (a text field, fieldKey',
      '"propertyAddress", label "What\'s the property address?", required) — routed after',
      'how-many-bins and before the fee stage, rather than bundled onto the first stage. Second, on',
      'the collection-fee stage, add a summary-list reviewing what the resident entered — the bin',
      'count and the address — with each row\'s own changeStateKey pointing back to the stage that',
      'actually captured it (bin count → how-many-bins, address → property-address), so the',
      "\"Change\" link on each row goes to the right place. Third, present the fee itself clearly and",
      'prominently — a stat-group with the total fee is enough; the raw calculation internals (base',
      'charge, per-bin surcharge, the cap) aren\'t something a resident should be asked to "change" in',
      'a summary list, so leave those out of it. Validate before you save, fix anything it flags,',
      'then save the service blueprint.'
    ].join(' ');

    await page.keyboard.type(refinementPrompt, { delay: 45 });
    await page.waitForTimeout(300);
    await page.keyboard.press('Enter');

    // Three independent, specific facts this refinement must produce: a separate property-address
    // stage with its own field, a summary-list on the fee stage, and a stat-group on the fee stage
    // (the fee shown as a clear result, not buried in the review rows).
    await expect.poll(
      async () => {
        const response = await request.get(
          `${businessAppOrigin}/prism/service-blueprint-authoring/service-blueprints/${serviceBlueprintKey}`,
          { ignoreHTTPSErrors: true }
        );
        if (!response.ok()) return false;
        const definition = await response.json();
        const addressStage = definition.states?.find((s: { stateKey: string }) => s.stateKey === 'property-address');
        const feeStage = definition.states?.find((s: { stateKey: string }) => s.stateKey === 'collection-fee');
        const hasAddressField = addressStage?.components?.some(
          (c: { fieldKey?: string }) => c.fieldKey === 'propertyAddress'
        );
        const hasSummaryList = feeStage?.components?.some((c: { type?: string }) => c.type === 'summary-list');
        const hasStatGroup = feeStage?.components?.some((c: { type?: string }) => c.type === 'stat-group');
        return Boolean(hasAddressField && hasSummaryList && hasStatGroup);
      },
      { timeout: 600_000, intervals: [3_000] }
    ).toBe(true);

    await beat(
      page,
      'recap',
      "Same conversation, a follow-up ask, and it reshaped the design — a new stage, a proper " +
        'review screen, a clearer fee. No restart, no redeploy, just iterating.',
      { position: 'top' }
    );
  });

  test('Act 8 — go back to the editor and see the refined design', async () => {
    await beat(page, 'intent', "Let's see what that refinement actually changed.", { position: 'top' });

    await page.goto(`${businessAppOrigin}/service-blueprint-editor?service blueprint=${serviceBlueprintKey}`);
    await expect(page.locator('[data-prism-component="service-blueprint-editor-shell"]'))
      .toHaveAttribute('data-prism-active-service-blueprint', serviceBlueprintKey, { timeout: 30_000 });
    await expect(page.locator('prism-service-blueprint-editor'))
      .toHaveAttribute('data-prism-service-blueprint-loaded', serviceBlueprintKey, { timeout: 30_000 });

    await page.locator('[data-prism-confidence-tab="canvas"]').click();
    await expect(page.locator('[data-prism-stage="property-address"]')).toBeVisible();

    await beat(page, 'note', "Let's zoom to fit and see the whole graph.");
    await humanClick(page, page.getByRole('button', { name: 'Fit' }));
    await page.waitForTimeout(400);

    await beat(
      page,
      'recap',
      'A new stage in the middle of the flow — the address question — slotted in between the ones ' +
        'we already had.',
      { position: 'top' }
    );

    await beat(page, 'intent', "And here's the fee stage again, rebuilt.", { position: 'top' });
    await humanClick(page, page.locator('[data-prism-stage="collection-fee"]'));
    await page.waitForTimeout(600);

    // Definition tab: the real payoff of this act — show the actual JSON the agent wrote,
    // specifically the per-row changeStateKey values that make a "Change" link on each summary-list
    // row navigate back to the CORRECT earlier stage (not a shared single target) — the toolkit
    // capability this whole refinement exists to demonstrate.
    await page.locator('[data-prism-confidence-tab="definition"]').click();
    await page.waitForFunction(() => {
      const shell = document.querySelector('prism-service-blueprint-editor-shell');
      const editor = shell?.shadowRoot?.querySelector('prism-service-blueprint-editor');
      return !!editor?.shadowRoot?.querySelector('prism-definition-editor');
    }, { timeout: 15_000 });
    await page.waitForTimeout(800);
    await beat(
      page,
      'intent',
      "Look closely at the summary-list it wrote — each row points its own \"Change\" link back to " +
        'the stage that actually captured it.'
    );
    const feeStageComponents = await page.evaluate(() => {
      const shell = document.querySelector('prism-service-blueprint-editor-shell')!;
      const editor = shell.shadowRoot!.querySelector('prism-service-blueprint-editor')!;
      const def = editor.shadowRoot!.querySelector('prism-definition-editor') as unknown as { value: string };
      const parsed = JSON.parse(def.value);
      const feeStage = parsed.states?.find((s: { stateKey: string }) => s.stateKey === 'collection-fee');
      return feeStage?.components ?? [];
    });
    const summaryList = feeStageComponents.find((c: { type?: string }) => c.type === 'summary-list');
    expect(summaryList, 'collection-fee should carry the summary-list the agent wrote').toBeTruthy();
    const hasPerRowChangeKeys = (summaryList.children ?? []).some(
      (child: { changeStateKey?: string }) => Boolean(child.changeStateKey)
    );
    expect(hasPerRowChangeKeys, 'summary-list rows should carry their own changeStateKey').toBeTruthy();
    const hasStatGroup = feeStageComponents.some((c: { type?: string }) => c.type === 'stat-group');
    expect(hasStatGroup, 'collection-fee should also carry a stat-group for the fee itself').toBeTruthy();

    await beat(
      page,
      'recap',
      "A review screen that actually makes sense — captured answers you can go back and change, " +
        'and the calculated fee shown clearly on its own, not mixed in with them.'
    );
  });

  test('Act 9 — run it again as a real user, and change our mind', async ({ request }) => {
    // More sequential real interactions than any other act (bins → address → summary → Change →
    // bins again → address again → summary again) — the 5-minute config default has no margin
    // left once each humanClick/humanType's deliberate human-paced delay is added up.
    test.setTimeout(6 * 60_000);

    // requestPolicy is "single" for this resident — Act 6 already ran a submission and left its
    // instance parked wherever it stopped (the collection-fee review stage). Without a reset,
    // clicking the nav link here just RESUMES that instance at its current state instead of
    // starting fresh (confirmed live: landed straight on the review stage, bin count already "5"
    // from Act 6, address "Not answered" since that field didn't exist yet when Act 6 ran) —
    // exactly wrong for "let's run it again" framing, which means starting over. Only the
    // instance needs clearing; resetServiceBlueprints() doesn't touch the definition Act 7 just saved.
    await resetServiceBlueprints(request);

    // Two-stage ground truth now: how-many-bins then property-address before the fee stage, so the
    // expected-fee simulation has to walk both steps (discovering each stage's own action key from
    // the trace, never assuming a trigger name) rather than the single-step call Act 6 used.
    const currentDefinition = await (
      await request.get(`${businessAppOrigin}/prism/service-blueprint-authoring/service-blueprints/${serviceBlueprintKey}`, { ignoreHTTPSErrors: true })
    ).json();

    async function simulate(steps: Array<{ action: string; fieldValues: Record<string, unknown> }>) {
      return (
        await request.post(`${businessAppOrigin}/prism/service-blueprint-authoring/service-blueprints/simulate`, {
          ignoreHTTPSErrors: true,
          data: { serviceBlueprint: currentDefinition, steps }
        })
      ).json();
    }

    async function expectedFeeFor(binCount: number, address: string): Promise<string> {
      const initial = await simulate([]);
      const binsActionKey = initial.trace?.[0]?.render?.availableActions?.[0]?.actionKey ?? '';
      const afterBins = await simulate([{ action: binsActionKey, fieldValues: { binCount } }]);
      const addressActionKey = afterBins.trace?.at(-1)?.render?.availableActions?.[0]?.actionKey ?? '';
      const afterAddress = await simulate([
        { action: binsActionKey, fieldValues: { binCount } },
        { action: addressActionKey, fieldValues: { propertyAddress: address } }
      ]);
      const feeFields = lastCalculatedFields(afterAddress.calculations);
      const fee = feeFields.fee ?? feeFields.totalFee ?? feeFields.total ?? Object.values(feeFields).at(-1);
      expect(fee, 'could not determine the expected fee from a direct simulate_serviceBlueprint call').not.toBeUndefined();
      return String(fee);
    }

    const address = '14 Orchard Close, Newtown';
    const expectedFeeFor5Bins = await expectedFeeFor(5, address);
    const expectedFeeFor6Bins = await expectedFeeFor(6, address);

    await beat(page, 'setup', "Let's run it again as that same resident.", { position: 'top' });

    // Acts 7 and 8 navigated away to the terminal and the editor — still the same signed-in
    // TestSite session (cookies persist on this one shared page/context), but the nav link only
    // exists on a TestSite page, so land back on the home page before looking for it, same as
    // signIn() itself does right after login.
    await page.goto('/');
    await humanClick(page, page.getByRole('link', { name: 'Garden Waste Permit' }));
    await page.waitForLoadState('networkidle', { timeout: 15_000 }).catch(() => {});
    await expect(page.getByRole('heading', { name: /how many bins/i })).toBeVisible({ timeout: 10_000 });

    await beat(page, 'intent', "Bin count first, same as before.", { position: 'top' });
    await humanType(page, page.getByLabel(/how many bins/i), '5');
    await page.waitForTimeout(500);
    await humanClick(page, page.getByRole('button', { name: /continue|submit|next/i }).first());

    await expect(page.getByRole('heading', { name: /address/i })).toBeVisible({ timeout: 10_000 });
    await beat(
      page,
      'intent',
      "And here's the new question the agent added — the property address, on its own stage.",
      { position: 'top' }
    );
    await humanType(page, page.getByLabel(/address/i), address);
    await page.waitForTimeout(500);
    await humanClick(page, page.getByRole('button', { name: /continue|submit|next/i }).first());

    await expect(page.getByText(expectedFeeFor5Bins)).toBeVisible({ timeout: 15_000 });
    await beat(
      page,
      'recap',
      'A summary of what we told it, and the fee, clearly laid out — not a wall of raw calculation.'
    );

    // The actual payoff of this whole refinement: click "Change" on the bin-count row and confirm
    // it goes back to the RIGHT stage (how-many-bins, pre-filled with the value we gave it), not
    // just back to "the previous stage" or nowhere at all.
    await beat(
      page,
      'intent',
      "Let's see if we can actually change our mind — click Change on the bin count.",
      { position: 'top' }
    );
    await humanClick(page, page.getByRole('button', { name: /change.*bins|change.*bin count/i }).first());
    await expect(page.getByRole('heading', { name: /how many bins/i })).toBeVisible({ timeout: 10_000 });
    const binsField = page.getByLabel(/how many bins/i);
    await expect(binsField).toHaveValue('5');

    await beat(
      page,
      'recap',
      "Right back to that exact stage, previous answer still there. Let's change it to six bins " +
        'and see the fee update.'
    );
    await humanType(page, binsField, '6');
    await page.waitForTimeout(500);
    await humanClick(page, page.getByRole('button', { name: /continue|submit|next/i }).first());

    // Address stage is revisited on the way back through — already filled from before, so just
    // continue rather than retyping it.
    await expect(page.getByRole('heading', { name: /address/i })).toBeVisible({ timeout: 10_000 });
    await humanClick(page, page.getByRole('button', { name: /continue|submit|next/i }).first());

    await expect(page.getByText(expectedFeeFor6Bins)).toBeVisible({ timeout: 15_000 });
    await beat(
      page,
      'recap',
      `And there it is — the fee recalculated for six bins. A real "change your mind" loop, not ` +
        'just a static review screen.'
    );
  });

  test('Closing slate', async () => {
    await showSlate(page, {
      eyebrow: 'UMBRACO PRISM',
      title: "That's the whole loop",
      body:
        'Content, service blueprint, calculation, and a real page — authored partly by hand, partly by an ' +
        'AI agent talking to nothing but a documented MCP toolkit. Thanks for watching.'
    });
  });
});
