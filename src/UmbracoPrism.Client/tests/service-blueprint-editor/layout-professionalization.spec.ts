import { test, expect } from '@playwright/test';

// Slice D quarantine (2026-05-31):
// This file tests the previous host shell mounted at `/service-blueprint-editor.html`
// — the marketing-chrome / launch-card / hero surface that existed before
// Slice B + Slice C reframed the editor as an integrator-embedded component
// (see docs/guides/embedding-the-service-blueprint-editor.md). The reference shell
// is now demo-only; production integrators bring their own host page.
//
// TODO Slice E: reframe surviving behavioural intents (skip link, tab
// order, screen-reader landmark, outline/graph toggle) against the
// MockBusinessApp host page or the Storybook shell, then delete this file.

const EDITOR_URL = 'http://localhost:5167/service-blueprint-editor.html?service blueprint=planning';

test.describe.fixme('Layout Professionalization', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto(EDITOR_URL);
    await expect(page.locator('prism-service-blueprint-editor')).toHaveAttribute('data-prism-service-blueprint-loaded', /.+/, {
      timeout: 30_000,
    });
  });

  test.describe('1. Host chrome minimization', () => {
    test('bulky explanatory hero chrome no longer dominates the viewport', async ({ page }) => {
      // BEHAVIORAL HOOK REQUEST FOR ISABELLE:
      // The hero section should either:
      // (a) Be significantly reduced in size (max-height: 15vh or similar constraint)
      // (b) Be removed entirely, with launch controls moved to a compact utility area
      // (c) Collapse to a minimal header bar
      //
      // Current state: .hero takes significant vertical space with large copy blocks
      // Target state: host chrome occupies ≤15% of viewport height
      
      const hero = page.locator('.hero');
      const viewport = page.viewportSize();
      
      if (viewport) {
        const heroBox = await hero.boundingBox().catch(() => null);
        
        if (heroBox) {
          const heroHeightRatio = heroBox.height / viewport.height;
          
          // Hero chrome should be minimal (15% or less of viewport)
          // Current baseline is ~20-30%; target is ≤15%
          expect(heroHeightRatio).toBeLessThanOrEqual(0.15);
        } else {
          // Hero might be removed entirely — that's acceptable too
          await expect(hero).toHaveCount(0);
        }
      }
    });

    test('explanatory copy is not the primary message', async ({ page }) => {
      // BEHAVIORAL HOOK REQUEST FOR ISABELLE:
      // The large marketing/explanatory copy blocks should be removed or moved to docs:
      // - "This shell stays focused on authoring..."
      // - "Runtime cases, approvals, and business processing still belong to your application"
      // - The "Why this host stays thin" section
      //
      // Target: The host should show the editor, not pitch the editor
      
      const introText = page.getByText(/this shell stays focused on authoring/i);
      const whyThinText = page.getByText(/why this host stays thin/i);
      const runtimeCasesCopy = page.getByText(/runtime cases.*belong to your application/i);
      
      // All explanatory prose should be gone
      await expect(introText).toHaveCount(0);
      await expect(whyThinText).toHaveCount(0);
      await expect(runtimeCasesCopy).toHaveCount(0);
    });

    test('integration rail/sidebar is not part of the default experience', async ({ page }) => {
      // BEHAVIORAL HOOK REQUEST FOR ISABELLE:
      // The `.integration-rail` aside (currently in the content grid) should either:
      // (a) Be removed entirely
      // (b) Collapse to a minimal "?" help icon that opens a modal/drawer on demand
      // (c) Be hidden by default with an optional toggle
      //
      // Target: The mounted experience is editor-only, not editor + persistent sales copy
      
      const integrationRail = page.locator('.integration-rail');
      const snippetCard = page.locator('.snippet-card');
      const patternList = page.locator('.pattern-list');
      
      // Integration guidance should not be visible by default
      await expect(integrationRail).not.toBeVisible();
      await expect(snippetCard).not.toBeVisible();
      await expect(patternList).not.toBeVisible();
    });
  });

  test.describe('2. Simplified launch flow', () => {
    test('authoring API base is not a mainline distraction', async ({ page }) => {
      // BEHAVIORAL HOOK REQUEST FOR ISABELLE:
      // The authoring API base input should either:
      // (a) Be hidden entirely (URL param only, no UI field)
      // (b) Move to a settings/preferences area (gear icon, modal, etc.)
      // (c) Collapse to a minimal "Connected to: {base}" readonly badge
      //
      // Current state: Large form with label, input, and inline help text
      // Target: The API base is config, not a primary control
      
      const apiBaseInput = page.locator('input[id="authoring-api-base"]');
      const apiBaseLabel = page.locator('label[for="authoring-api-base"]');
      
      // API base input should not be part of the primary launch form
      await expect(apiBaseInput).not.toBeVisible();
      await expect(apiBaseLabel).not.toBeVisible();
    });

    test('launch card is streamlined or removed', async ({ page }) => {
      // BEHAVIORAL HOOK REQUEST FOR ISABELLE:
      // The .launch-card section should either:
      // (a) Collapse to a compact service blueprint switcher (dropdown or tabs)
      // (b) Move to a top nav bar or toolbar
      // (c) Be removed entirely if the URL param is the source of truth
      //
      // Current state: Large card with form, button, meta text
      // Target: ServiceBlueprint selection is utility, not hero action
      
      const launchCard = page.locator('.launch-card');
      const launchButton = page.locator('.launch-button');
      
      // Large launch card should not be prominent
      const cardBox = await launchCard.boundingBox().catch(() => null);
      
      if (cardBox) {
        // If the launch card still exists, it should be compact
        expect(cardBox.height).toBeLessThan(150);
      }
      
      // The "Open service blueprint" button language is too heavy for a switcher
      await expect(launchButton).toHaveCount(0);
    });

    test('service blueprint selection is quick-access utility', async ({ page }) => {
      // BEHAVIORAL HOOK REQUEST FOR ISABELLE:
      // If service blueprint selection remains visible, it should be:
      // - A compact dropdown or tab strip
      // - Inline with the editor chrome (top nav or toolbar)
      // - Not a separate "launch" action — just switch and go
      //
      // Target: Switching service blueprints feels like switching tabs, not launching apps
      
      const serviceBlueprintSelector = page.locator('[data-prism-service-blueprint-selector]');
      
      // OPTIONAL SEMANTIC HOOK: [data-prism-service-blueprint-selector]
      // If Isabelle keeps service blueprint selection visible, she should add this hook
      // so tests can verify it's present and accessible
      
      if (await serviceBlueprintSelector.count() > 0) {
        // If present, it should be compact and keyboard-accessible
        await expect(serviceBlueprintSelector).toBeVisible();
        
        const selectorBox = await serviceBlueprintSelector.boundingBox();
        expect(selectorBox?.height).toBeLessThan(60);
        
        // Should be keyboard-focusable
        await serviceBlueprintSelector.focus();
        await expect(serviceBlueprintSelector).toBeFocused();
      }
    });
  });

  test.describe('3. Editor surface prioritization', () => {
    test('editor occupies dominant viewport space', async ({ page }) => {
      // BEHAVIORAL HOOK REQUEST FOR ISABELLE:
      // The .editor-frame (or equivalent editor container) should occupy
      // the vast majority of viewport height — target ≥80% of viewport.
      //
      // Current state: Editor gets ~60% after hero/launch/integration chrome
      // Target state: Editor owns ≥80% of viewport height
      
      const editorFrame = page.locator('.editor-frame');
      const viewport = page.viewportSize();
      
      if (viewport) {
        const editorBox = await editorFrame.boundingBox();
        
        if (editorBox) {
          const editorHeightRatio = editorBox.height / viewport.height;
          
          // Editor should dominate the viewport (80%+)
          expect(editorHeightRatio).toBeGreaterThanOrEqual(0.80);
        }
      }
    });

    test('editor is the primary mounted experience, not a section within chrome', async ({ page }) => {
      // BEHAVIORAL HOOK REQUEST FOR ISABELLE:
      // The editor should feel like the *page*, not a widget within a page.
      // Semantic contract:
      // - <main> should wrap the editor directly (not editor + sidebar)
      // - No large sibling sections competing for attention
      // - Skip link target should jump to editor canvas, not a shell container
      
      const main = page.locator('main');
      const editorElement = page.locator('prism-service-blueprint-editor');
      
      // Main should contain the editor as its primary child
      await expect(main.locator('prism-service-blueprint-editor')).toBeVisible();
      
      // Main should not have large competing siblings (integration rail, etc.)
      const mainSiblings = page.locator('main ~ aside, main ~ section');
      const siblingCount = await mainSiblings.count();
      
      // At most 1 small sibling (e.g., a minimal settings drawer)
      expect(siblingCount).toBeLessThanOrEqual(1);
      
      if (siblingCount === 1) {
        const siblingBox = await mainSiblings.first().boundingBox();
        // Any sibling should be small or hidden by default
        if (siblingBox) {
          expect(siblingBox.height).toBeLessThan(100);
        }
      }
    });

    test('editor canvas is not buried under section headings and kickers', async ({ page }) => {
      // BEHAVIORAL HOOK REQUEST FOR ISABELLE:
      // The "Mounted editor" section heading, kicker, and note should be removed:
      // - <p class="section-kicker">Mounted editor</p>
      // - <h2>planning</h2>
      // - <p class="section-note">Connected to {api}</p>
      //
      // Target: The editor is the experience; headings about the editor are meta-chrome
      
      const sectionKicker = page.locator('.section-kicker');
      const sectionNote = page.locator('.section-note');
      const editorStageHeading = page.locator('.editor-stage h2');
      
      // No section labeling chrome
      await expect(sectionKicker).toHaveCount(0);
      await expect(sectionNote).toHaveCount(0);
      await expect(editorStageHeading).toHaveCount(0);
    });
  });

  test.describe('4. Keyboard and screen reader accessibility', () => {
    test('skip link jumps directly to editor canvas', async ({ page }) => {
      // BEHAVIORAL HOOK VERIFIED:
      // The skip link (#service-blueprint-editor-reference-main) should remain functional
      // and jump to the editor canvas, skipping any minimal utility chrome
      
      const skipLink = page.locator('.skip-link');
      await expect(skipLink).toBeVisible();
      await expect(skipLink).toHaveAttribute('href', '#service-blueprint-editor-reference-main');
      
      // Focus skip link and activate
      await skipLink.focus();
      await skipLink.press('Enter');
      
      // Verify focus moves to main editor area
      const main = page.locator('#service-blueprint-editor-reference-main');
      await expect(main).toBeFocused();
    });

    test('tab order flows from utility controls to editor canvas', async ({ page }) => {
      // BEHAVIORAL HOOK REQUEST FOR ISABELLE:
      // If any utility controls remain (service blueprint switcher, settings), they should:
      // - Be keyboard-accessible
      // - Have a logical tab order
      // - Not create a tab trap before reaching the editor
      //
      // Tab order should be: [skip link] → [utility controls] → [editor outline] → [editor canvas]
      
      // Tab from skip link
      await page.locator('.skip-link').focus();
      await page.keyboard.press('Tab');
      
      // Next focus should be either a utility control or the editor itself
      const focusedElement = await page.evaluate(() => {
        const el = document.activeElement;
        return {
          tagName: el?.tagName,
          role: el?.getAttribute('role'),
          ariaLabel: el?.getAttribute('aria-label'),
          dataPrism: el?.getAttribute('data-prism-component'),
        };
      });
      
      // Focus should reach the editor area within 5 tabs
      let tabCount = 0;
      while (tabCount < 5) {
        const currentFocus = await page.evaluate(() => {
          const el = document.activeElement;
          return el?.closest('[data-prism-service-blueprint-outline], [data-prism-component="service-blueprint-graph"]') !== null;
        });
        
        if (currentFocus) {
          break;
        }
        
        await page.keyboard.press('Tab');
        tabCount++;
      }
      
      // Should reach editor within 5 tabs
      expect(tabCount).toBeLessThan(5);
    });

    test('editor maintains existing keyboard shortcuts', async ({ page }) => {
      // BEHAVIORAL HOOK VERIFIED:
      // Existing keyboard shortcuts should still work:
      // - 'e' to open inspector
      // - 'v' to toggle validation
      // - Undo/redo
      // - Graph/list toggle
      
      const graphCanvas = page.getByRole('application');
      await expect(graphCanvas).toBeVisible();
      
      // Focus a stage
      const firstStage = page.locator('[data-prism-stage]').first();
      await firstStage.focus();
      
      // Press 'e' to open inspector
      await firstStage.press('e');
      
      const inspector = page.locator('[data-prism-component="step-inspector"]');
      await expect(inspector).toBeVisible({ timeout: 5_000 });
    });

    test('screen reader announces editor as primary landmark', async ({ page }) => {
      // BEHAVIORAL HOOK REQUEST FOR ISABELLE:
      // The <main> landmark should contain the editor directly.
      // Screen reader users should hear: "main, service blueprint editor" (not nested sections)
      
      const main = page.locator('main[id="service-blueprint-editor-reference-main"]');
      await expect(main).toBeVisible();
      
      // Main should have accessible name
      const mainLabel = await main.evaluate(el => {
        return el.getAttribute('aria-label') || 
               el.getAttribute('aria-labelledby') ||
               'main';
      });
      
      // Main should not be nested in other landmarks
      const nestedLandmark = await main.evaluate(el => {
        return el.closest('header, nav, aside, footer') !== null;
      });
      
      expect(nestedLandmark).toBe(false);
    });
  });

  test.describe('5. Editor functionality preservation', () => {
    test('outline navigation remains accessible', async ({ page }) => {
      // BEHAVIORAL HOOK VERIFIED:
      // The persistent left-side outline should remain visible and functional
      
      const outline = page.locator('[data-prism-service-blueprint-outline]');
      await expect(outline).toBeVisible();
      
      // Outline should have stage items
      const outlineStages = outline.locator('[data-prism-outline-stage]');
      await expect(outlineStages).not.toHaveCount(0);
      
      // Click an outline stage
      const firstOutlineStage = outlineStages.first();
      await firstOutlineStage.click();
      
      // Should select in graph
      await expect(firstOutlineStage).toHaveAttribute('aria-current', 'true');
    });

    test('graph/list toggle remains functional', async ({ page }) => {
      // BEHAVIORAL HOOK VERIFIED:
      // The graph/list mode toggle should still work
      
      const graphCanvas = page.getByRole('application');
      await expect(graphCanvas).toBeVisible();
      
      // Toggle to list view
      const listToggle = page.locator('prism-service-blueprint-graph').getByRole('button', { name: 'List view' });
      await listToggle.click();
      
      const listTable = page.locator('[data-prism-linear-table]');
      await expect(listTable).toBeVisible({ timeout: 5_000 });
      
      // Toggle back to graph
      const graphToggle = page.locator('prism-service-blueprint-graph').getByRole('button', { name: 'Graph view' });
      await graphToggle.click();
      
      await expect(graphCanvas).toBeVisible({ timeout: 5_000 });
    });

    test('stage selection and inspector remain functional', async ({ page }) => {
      // BEHAVIORAL HOOK VERIFIED:
      // Selecting a stage should open the inspector
      
      const firstStage = page.locator('[data-prism-stage]').first();
      await firstStage.focus();
      await firstStage.press('e');
      
      const inspector = page.locator('[data-prism-component="step-inspector"]');
      await expect(inspector).toBeVisible({ timeout: 5_000 });
    });

    test('confidence tabs (validation/preview/simulation) remain accessible', async ({ page }) => {
      // BEHAVIORAL HOOK VERIFIED:
      // The tabbed confidence surfaces should remain visible and functional
      
      const confidenceTabs = page.locator('[data-prism-confidence-tabs]');
      await expect(confidenceTabs).toBeVisible();
      
      // Validation tab
      await confidenceTabs.locator('[data-prism-confidence-tab="validation"]').click();
      const validationPanel = page.locator('[data-prism-confidence-panel="validation"]');
      await expect(validationPanel).toBeVisible({ timeout: 5_000 });
      
      // Preview tab
      await confidenceTabs.locator('[data-prism-confidence-tab="preview"]').click();
      const previewPanel = page.locator('[data-prism-confidence-panel="preview"]');
      await expect(previewPanel).toBeVisible({ timeout: 5_000 });
      
      // Simulation tab
      await confidenceTabs.locator('[data-prism-confidence-tab="simulation"]').click();
      const simulationPanel = page.locator('[data-prism-confidence-panel="simulation"]');
      await expect(simulationPanel).toBeVisible({ timeout: 5_000 });
    });

    test('role-first swim lanes remain visible and semantic', async ({ page }) => {
      // BEHAVIORAL HOOK VERIFIED:
      // The role-first swim lanes should remain visible and accessible
      
      const graphCanvas = page.getByRole('application');
      await expect(graphCanvas).toHaveAttribute('aria-roledescription', /role-first/i);
      
      const roleLanes = page.locator('[data-prism-role-queue]');
      await expect(roleLanes).not.toHaveCount(0);
      
      // At least 2 lanes should be visible in viewport
      const firstLane = roleLanes.first();
      const secondLane = roleLanes.nth(1);
      await expect(firstLane).toBeInViewport();
      await expect(secondLane).toBeInViewport({ ratio: 0.5 });
    });
  });

  test.describe('Regression: PR #75 pointer-blocked stages', () => {
    test('stage cards are clickable without chrome overlap', async ({ page }) => {
      // HISTORICAL CONTEXT:
      // PR #75 CI failures showed "Send" button was pointer-blocked by overlapping
      // editor chrome in the browser-hosted surface. This was mitigated by using
      // keyboard shortcuts, but the root cause was insufficient editor space.
      //
      // This test proves that stage cards are not blocked by overlapping chrome.
      
      const firstStage = page.locator('[data-prism-stage]').first();
      await expect(firstStage).toBeVisible();
      
      // Stage should be clickable (not pointer-blocked)
      const stageBox = await firstStage.boundingBox();
      expect(stageBox).not.toBeNull();
      
      if (stageBox) {
        // Click the center of the stage
        await page.mouse.click(stageBox.x + stageBox.width / 2, stageBox.y + stageBox.height / 2);
        
        // Should select the stage
        await expect(firstStage).toHaveAttribute('aria-pressed', 'true', { timeout: 2_000 });
      }
    });
  });
});
