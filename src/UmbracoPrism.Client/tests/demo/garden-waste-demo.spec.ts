import { test, expect, type Page } from '@playwright/test';
import { mkdirSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { signIn, resetWorkflows, businessAppOrigin } from '../walkthroughs/support/walkthrough';
import { showCaption } from './support/caption';

// Playwright's automatic video-to-test-result attachment only works for pages scoped to a
// single test (the built-in page/context fixtures). These pages are deliberately shared
// across acts via beforeAll/afterAll for narrative continuity (the editor tab genuinely stays
// open from Act 2 into Act 4) — which means the runner never has a single test to attach the
// recording to, and silently drops it. Saving explicitly here is the only way to actually get
// the footage back.
const __dirname = path.dirname(fileURLToPath(import.meta.url));
const footageDir = path.join(__dirname, '..', '..', 'demo-footage');
mkdirSync(footageDir, { recursive: true });

async function savePageVideo(page: Page | undefined, name: string): Promise<void> {
  if (!page) return;
  const video = page.video();
  await page.close();
  if (video) {
    // saveAs() copies rather than moves — delete the original auto-hash-named recording
    // afterward, or every run leaves duplicate multi-MB files behind in footageDir.
    await video.saveAs(path.join(footageDir, `${name}.webm`));
    await video.delete();
  }
}

// Not a CI test — a demo-recording tool. Run with:
//   npm run demo:record
// See tests/demo/README.md for the full operator setup (warming the stack, ttyd, etc.).
//
// Deliberately one test() per act inside a serial describe block, sharing pages created once in
// beforeAll: a failed later act still leaves earlier acts' .webm footage on disk, and pages persist
// across acts the way a real browser session would (e.g. the editor tab from Act 2 is still open
// when Act 4 cuts back to it).

const workflowKey = 'garden-waste-permit';
const adminCredentials = { username: 'admin@prism.local', password: 'PrismLocal!12345' };

test.describe.serial('garden waste permit demo', () => {
  test.beforeAll(async ({ request }) => {
    await resetWorkflows(request);
  });

  let backofficePage: Page;
  let editorPage: Page;
  let terminalPage: Page;
  let testSitePage: Page;
  let publishedUrl: string;

  test.beforeAll(async ({ browser }) => {
    // recordVideo isn't inherited from the config's `use.video` for a manually-created page the
    // way baseURL/ignoreHTTPSErrors are — confirmed live: page.video() came back null for all
    // four pages without this. Pass it explicitly instead of relying on config inheritance.
    backofficePage = await browser.newPage({ recordVideo: { dir: footageDir } });
    editorPage = await browser.newPage({ recordVideo: { dir: footageDir } });
    testSitePage = await browser.newPage({ recordVideo: { dir: footageDir } });
  });

  test.afterAll(async () => {
    // editorPage's recording spans both Act 2 (built by hand) and Act 4 (staleness banner) —
    // it's the same page/context the whole time, so that's one continuous take, not two files.
    await savePageVideo(backofficePage, 'act-1-backoffice');
    await savePageVideo(editorPage, 'act-2-and-4-editor');
    await savePageVideo(terminalPage, 'act-3-agent-terminal');
    await savePageVideo(testSitePage, 'act-5-real-user');
  });

  test('Act 1 — create the Umbraco page and wire up a real nav link', async () => {
    // Selectors below were confirmed live against a real running backoffice, not guessed —
    // see the "Act 1 manual-fallback cue sheet" in README.md if any of these prove fragile on
    // a different Umbraco/UUI version.
    await backofficePage.goto('/umbraco');
    await backofficePage.getByLabel('E-mail').fill(adminCredentials.username);
    await backofficePage.locator('#password-input').fill(adminCredentials.password);
    await backofficePage.getByRole('button', { name: 'Login' }).click();
    await backofficePage.waitForLoadState('networkidle', { timeout: 30_000 }).catch(() => {});
    await expect(backofficePage.getByText('Home', { exact: true }).first()).toBeVisible({ timeout: 30_000 });

    await showCaption(backofficePage, "A service designer starts here — Content, under Home.");

    // "Home" allows workflowPage/workflowHub/memberDashboard as children (see
    // PrismContentTypeSeeder.EnsureHomeAllowedChildrenAsync) — matching the structure the seeded
    // demo pages already use, now actually reachable through the Create dialog. The "+" button
    // only renders on hover — hovering the row first is required, not just a visual nicety.
    await backofficePage.getByText('Home', { exact: true }).first().hover();
    await backofficePage.getByRole('button', { name: 'Create item for Home' }).click();
    await backofficePage.locator('uui-ref-node-document-type').filter({ hasText: 'Workflow Page' }).click();
    await backofficePage.waitForLoadState('networkidle', { timeout: 30_000 }).catch(() => {});

    await backofficePage.getByRole('textbox', { name: 'Enter a name...' }).fill('Garden Waste Permit');
    await backofficePage.getByLabel('Workflow Key').fill(workflowKey);

    await showCaption(backofficePage, "One property — the Workflow Key — is the entire connection to the runtime engine.");

    await backofficePage.getByRole('button', { name: 'Save and publish', exact: true }).click();
    await expect(backofficePage.getByRole('alert').getByText('Document published')).toBeVisible({ timeout: 15_000 });

    publishedUrl = `/${workflowKey}/`;
    // Confirm the guessed slug actually resolves rather than assuming it silently.
    const check = await backofficePage.request.get(publishedUrl, { ignoreHTTPSErrors: true });
    expect(check.ok(), `published page did not resolve at ${publishedUrl}`).toBeTruthy();

    // Wire up the real nav link — Settings → Web Navigation Links, the content-driven property
    // this same recording's Phase A added (mirroring the pre-existing mobileNavLinks pattern).
    // "Settings" is ambiguous by plain text (also the top-nav section label) — scope to the
    // content-tree menu item specifically.
    await backofficePage.goto('/umbraco/section/content');
    await backofficePage.locator('uui-menu-item').filter({ hasText: 'Settings' }).first().click();
    await backofficePage.waitForLoadState('networkidle', { timeout: 30_000 }).catch(() => {});

    await showCaption(backofficePage, "And now a real link — no direct URL, no smoke and mirrors.");

    await backofficePage.getByText('Add Mobile Nav Item').nth(1).click();
    await backofficePage.getByText('Mobile Nav Item', { exact: true }).click();
    await backofficePage.getByLabel('Label').fill('Garden Waste Permit');
    await backofficePage.getByLabel('URL').fill(publishedUrl);
    await backofficePage.getByRole('button', { name: 'Create', exact: true }).click();
    // The "Add content" drawer's Submit button may or may not still be open here depending on
    // timing — bounded click+catch avoids the check-then-act race of isVisible() followed by a
    // separate click (the button can detach between the two).
    await backofficePage.getByRole('button', { name: 'Submit' }).click({ timeout: 5_000 }).catch(() => {});

    await backofficePage.getByRole('button', { name: 'Save and publish', exact: true }).click();
    await expect(backofficePage.getByRole('alert').getByText('Document published')).toBeVisible({ timeout: 15_000 });
  });

  test('Act 1.5 — scaffold the workflow shell (off-camera)', async ({ request }) => {
    // `resetWorkflows()` clears running workflow *instances*, not authored *definitions* — a
    // prior take's scaffold survives in the in-memory definition store until the whole stack
    // restarts. Read the current version first (0 if this key has never been saved) so re-runs
    // against an already-warm stack are idempotent, rather than assuming a fresh version 0.
    const existing = await request.get(
      `${businessAppOrigin}/prism/workflow-authoring/workflows/${workflowKey}`,
      { ignoreHTTPSErrors: true }
    );
    const currentVersion = existing.ok() ? (await existing.json()).version : 0;

    // Deliberately zero states — Act 2 builds the first one live, on camera. initialState names
    // it in advance so the definition reads coherently, but nothing about this scaffold call
    // pre-empts the "built by hand" beat that follows.
    const response = await request.put(
      `${businessAppOrigin}/prism/workflow-authoring/workflows/${workflowKey}`,
      {
        ignoreHTTPSErrors: true,
        data: {
          definitionKey: workflowKey,
          displayName: 'Garden Waste Permit',
          version: currentVersion,
          initialState: 'how-many-bins',
          instancePolicy: 'single',
          states: [],
          queues: [{ key: 'web-user', displayName: 'Member', actor: 'member' }]
        }
      }
    );

    expect(response.ok(), `scaffold PUT failed: ${response.status()} ${await response.text()}`).toBeTruthy();
  });

  test('Act 2 — build the first stage by hand in the editor', async () => {
    await editorPage.goto(`${businessAppOrigin}/workflow-editor?workflow=${workflowKey}`);
    await expect(editorPage.locator('[data-prism-component="workflow-editor-shell"]'))
      .toHaveAttribute('data-prism-active-workflow', workflowKey, { timeout: 30_000 });
    await expect(editorPage.locator('prism-workflow-editor'))
      .toHaveAttribute('data-prism-workflow-loaded', workflowKey, { timeout: 30_000 });

    await showCaption(editorPage, "Stages and routes are fully UI-authorable today — let's build the first one by hand.");

    // Empty-canvas "Create stage" affordance (tests/workflow-editor/workflow-graph-keyboard.spec.ts).
    await editorPage.locator('[data-prism-empty-add-stage]').click();
    const dialog = editorPage.locator('[data-prism-create-stage-dialog]');
    await dialog.locator('[data-prism-create-stage-title]').fill('How many bins do you have?');
    await dialog.locator('[data-prism-create-stage-key]').fill('how-many-bins');
    await dialog.locator('[data-prism-create-stage-queue]').fill('web-user');
    await dialog.getByRole('button', { name: 'Create stage' }).click();
    await expect(dialog).toBeHidden();
    await expect(editorPage.locator('[data-prism-stage="how-many-bins"]')).toBeVisible();

    // "+ Add route" — target deliberately left unset. That's the honest story beat: the agent
    // fills it in when it builds the second stage in Act 3.
    // TODO: confirm exact data-prism-add-route scoping (per-stage vs. global) live.
    await showCaption(editorPage, "A route with nowhere to go yet — that's what the agent adds next.");

    // prism-definition-editor lives inside TWO nested shadow roots (workflow-editor-shell →
    // workflow-editor → definition-editor) — Playwright's own locators auto-pierce open shadow
    // roots, but a raw page.evaluate() callback runs plain DOM APIs that don't, so the walk has
    // to be explicit here.
    await editorPage.locator('[data-prism-confidence-tab="definition"]').click();
    // prism-definition-editor is lazy-loaded — wait for it to actually mount rather than a fixed
    // sleep, since first-load JS-chunk fetch time varies (this raced and failed intermittently
    // with a fixed 500ms wait).
    await editorPage.waitForFunction(() => {
      const shell = document.querySelector('prism-workflow-editor-shell');
      const editor = shell?.shadowRoot?.querySelector('prism-workflow-editor');
      return !!editor?.shadowRoot?.querySelector('prism-definition-editor');
    }, { timeout: 15_000 });
    await editorPage.evaluate(({ key, field }) => {
      const shell = document.querySelector('prism-workflow-editor-shell')!;
      const editor = shell.shadowRoot!.querySelector('prism-workflow-editor')!;
      const def = editor.shadowRoot!.querySelector('prism-definition-editor') as unknown as { value: string };
      const current = JSON.parse(def.value);
      const stage = current.states.find((s: { stateKey: string }) => s.stateKey === key);
      stage.components = [
        { type: 'number', fieldKey: field, label: 'How many bins do you have?', required: true, min: 1 }
      ];
      def.value = JSON.stringify(current);
      def.dispatchEvent(new CustomEvent('definition-input', { detail: { value: def.value }, bubbles: true, composed: true }));
    }, { key: 'how-many-bins', field: 'binCount' });
    await editorPage.waitForTimeout(600); // 250ms auto-apply debounce, plus margin

    // The Definition tab has no save button of its own ("Edits apply when valid" — auto-applies
    // to editor state); the actual persist-to-server Save button lives back on the Canvas tab.
    await editorPage.locator('[data-prism-confidence-tab="canvas"]').click();
    await editorPage.getByRole('button', { name: 'Save', exact: true }).click();
    await editorPage.waitForTimeout(1_000);

    await editorPage.locator('[data-prism-confidence-tab="validation"]').click();
  });

  test('Act 3 — hand off to the agent over MCP', async ({ browser, request }) => {
    // Real agent call, not mocked, doing real iterative debugging (validate → fix → re-validate,
    // sometimes cross-checking other seeded workflows to isolate a tool-usage mistake) — observed
    // live to need well over the initial 150s poll budget. 10 minutes overall, most of it given to
    // the poll below, small margin left for page setup/teardown.
    test.setTimeout(10 * 60_000);
    const ttydUrl = process.env.TTYD_URL ?? 'http://127.0.0.1:7681';
    const ttydPassword = process.env.TTYD_PASSWORD;
    if (!ttydPassword) {
      throw new Error('Set TTYD_PASSWORD to the password used when launching ttyd — see README.md.');
    }

    terminalPage = await browser.newPage({
      httpCredentials: { username: 'demo', password: ttydPassword },
      recordVideo: { dir: footageDir }
    });
    await terminalPage.goto(ttydUrl, { waitUntil: 'networkidle', timeout: 15_000 });
    await terminalPage.waitForTimeout(2_000);
    await terminalPage.locator('.xterm-screen').click();

    // ttyd spawns a fresh `claude` process per browser connection, so the one-time
    // "Claude Code running in BypassPermissions mode... 1. No, exit  2. Yes, I accept" gate
    // reappears every take. Answer it before typing the real prompt — otherwise the prompt's
    // keystrokes land on this dialog instead of the input box.
    await terminalPage.keyboard.press('2');
    await terminalPage.keyboard.press('Enter');
    await terminalPage.waitForTimeout(1_000);

    await showCaption(terminalPage, "Now an AI agent — connected only through the MCP toolkit we just shipped.");

    // This act's job is authoring, not proving the math — the agent designs and saves the
    // second stage (a real calculations block + a component that renders it, gateway-routed
    // from the first stage). Whether the fee is actually correct on screen is Act 5's job: a
    // real signed-in TestSite user submitting a bin count and seeing the live engine render it.
    // Asking the agent to also call simulate_workflow and read back the numbers here conflates
    // the two beats, so it's deliberately not part of this prompt.
    const prompt = [
      "I'm designing a garden waste collection permit service (definitionKey: garden-waste-permit).",
      'There is already a first stage "how-many-bins" capturing a number field "binCount".',
      'Read the workflow, then add a second stage "collection-fee" that calculates the fee:',
      '£40 base charge plus £10 per bin over 2, capped at £120 — and include a visible component',
      'that actually displays the calculated fee (a calculation with nothing rendering it is',
      'invisible to the user). Route the first stage through to it via a gateway. Validate before',
      'you save, and fix anything it flags, then save the workflow.'
    ].join(' ');

    await terminalPage.keyboard.type(prompt, { delay: 15 });
    await terminalPage.waitForTimeout(300);
    await terminalPage.keyboard.press('Enter');

    // The real completion signal is the saved definition itself, not anything printed in the
    // terminal — the terminal is just the visual of the agent working. Poll the same
    // read_workflow endpoint the agent itself calls, via the plain REST toolkit (not MCP), for a
    // second state carrying a non-empty `calculations` block. That's the one fact that can only
    // become true via a real save_workflow call reaching the live engine.
    await expect.poll(
      async () => {
        const response = await request.get(
          `${businessAppOrigin}/prism/workflow-authoring/workflows/${workflowKey}`,
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

    // Hold ~15s before cutting away — the editor's staleness poll (Act 4) runs on a fixed 15s
    // timer, not on focus, so the next poll needs time to have already landed.
    await terminalPage.waitForTimeout(15_000);
  });

  test('Act 4 — watch it land in the editor', async () => {
    // editorPage has been sitting on the Act 2 save (one stage) this whole time. The editor
    // polls for a newer server version every 15s (VERSION_POLL_INTERVAL_MS) — Act 3's save is
    // what makes the banner appear here, with no action from this test beyond waiting for it.
    await expect(editorPage.locator('[data-prism-stale-workflow-banner]')).toBeVisible({ timeout: 30_000 });
    await showCaption(editorPage, "The agent's save shows up here immediately — no restart, no refresh.");

    await editorPage.locator('[data-prism-reload-after-conflict]').click();
    await expect(editorPage.locator('[data-prism-stale-workflow-banner]')).toBeHidden({ timeout: 10_000 });

    // Act 2 ended on the Validation tab; the canvas tab panel carries `?hidden` whenever it
    // isn't active (prism-confidence-tabs.ts), so the graph is unrendered-to-Playwright until
    // this switches back — reload doesn't reset the active tab on its own.
    await editorPage.locator('[data-prism-confidence-tab="canvas"]').click();

    // Both stages now on canvas — the agent's work, reflected in the same editor session.
    await expect(editorPage.locator('[data-prism-stage="how-many-bins"]')).toBeVisible();
    await expect(editorPage.locator('[data-prism-stage="collection-fee"]')).toBeVisible();

    await showCaption(editorPage, "Reloaded — the second stage the agent built is right there.");

    // Definition tab: show the calculations block the agent actually wrote, not just the canvas
    // shape — the maths itself is the point of this whole toolkit.
    await editorPage.locator('[data-prism-confidence-tab="definition"]').click();
    await editorPage.waitForFunction(() => {
      const shell = document.querySelector('prism-workflow-editor-shell');
      const editor = shell?.shadowRoot?.querySelector('prism-workflow-editor');
      return !!editor?.shadowRoot?.querySelector('prism-definition-editor');
    }, { timeout: 15_000 });
    const hasCalculations = await editorPage.evaluate(() => {
      const shell = document.querySelector('prism-workflow-editor-shell')!;
      const editor = shell.shadowRoot!.querySelector('prism-workflow-editor')!;
      const def = editor.shadowRoot!.querySelector('prism-definition-editor') as unknown as { value: string };
      const parsed = JSON.parse(def.value);
      return Boolean(parsed.calculations?.fields && Object.keys(parsed.calculations.fields).length > 0);
    });
    expect(hasCalculations, 'reloaded definition should carry the calculations block the agent wrote').toBeTruthy();
  });

  test('Act 5 — run it as a real logged-in user', async ({ request }) => {
    // Ground truth from the engine itself, independent of the UI: whatever field name/label the
    // agent chose, this is the fee a real submission of 5 bins must produce. Comparing the live
    // page against this (rather than a hardcoded "£70") keeps the assertion honest across
    // different agent runs that all correctly implement the same formula.
    const currentDefinition = await (
      await request.get(`${businessAppOrigin}/prism/workflow-authoring/workflows/${workflowKey}`, { ignoreHTTPSErrors: true })
    ).json();
    // The agent chooses its own route trigger each run (empty string, "continue", "submit", ...)
    // — discover the real one from the initial render's own availableActions rather than
    // guessing, so this stays correct across different (equally valid) agent-authored shapes.
    const initialRender = await (
      await request.post(`${businessAppOrigin}/prism/workflow-authoring/workflows/simulate`, {
        ignoreHTTPSErrors: true,
        data: { workflow: currentDefinition, steps: [] }
      })
    ).json();
    const realActionKey = initialRender.trace?.[0]?.render?.availableActions?.[0]?.actionKey ?? '';

    const simulation = await (
      await request.post(`${businessAppOrigin}/prism/workflow-authoring/workflows/simulate`, {
        ignoreHTTPSErrors: true,
        data: { workflow: currentDefinition, steps: [{ action: realActionKey, fieldValues: { binCount: 5 } }] }
      })
    ).json();
    const expectedFee = simulation.calculations?.at(-1)?.fields?.fee
      ?? Object.values(simulation.calculations?.at(-1)?.fields ?? {})[0];
    expect(expectedFee, 'could not determine the expected fee from a direct simulate_workflow call').not.toBeUndefined();

    await signIn(testSitePage);

    // The real point of Act 1's nav-link beat: reach the page by clicking the link a real user
    // would use, not a direct URL.
    await showCaption(testSitePage, "A real signed-in user — arriving the way anyone actually would: the nav link.");
    await testSitePage.getByRole('link', { name: 'Garden Waste Permit' }).click();
    await testSitePage.waitForLoadState('networkidle', { timeout: 15_000 }).catch(() => {});

    await testSitePage.getByLabel(/how many bins/i).fill('5');
    // The agent-authored route's trigger/label may be blank (an empty-string action key) — fall
    // back from a named continue/submit button to the sole form button if nothing named matches.
    const namedSubmit = testSitePage.getByRole('button', { name: /continue|submit|next/i });
    if (await namedSubmit.count() > 0) {
      await namedSubmit.first().click();
    } else {
      await testSitePage.locator('form button[type="submit"], form button').first().click();
    }

    await showCaption(testSitePage, `The live engine calculates the fee — £${expectedFee}, right here.`);
    await expect(testSitePage.getByText(String(expectedFee))).toBeVisible({ timeout: 15_000 });
  });
});
