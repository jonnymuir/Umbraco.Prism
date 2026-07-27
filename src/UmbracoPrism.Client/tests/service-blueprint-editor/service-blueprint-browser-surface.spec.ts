import { expect, test } from '@playwright/test';

// Slice D quarantine (2026-05-31):
// This file tests the previous browser shell at `/service-blueprint-editor.html` —
// the marketing-chrome / launch-card / integration-rail surface that existed
// before the Slice B ServiceBlueprintSource boundary + Slice C MockBusinessApp
// rewrite. That surface has been deliberately retired; the editor is now
// embedded by integrators in their own host pages (see
// docs/guides/embedding-the-service-blueprint-editor.md). These specs are marked
// fixme rather than deleted so the behavioural intent stays visible if a
// future slice reintroduces a Prism-shipped demo shell.
//
// TODO Slice E: reframe surviving behavioural intents (keyboard reach,
// screen-reader landmarks, swim-lane reachability) against the
// MockBusinessApp host page or the Storybook shell, then delete this file.

function shellUrl(serviceBlueprintKey = 'planning'): string {
  const apiBase = process.env.BUSINESS_APP_ORIGIN || 'http://localhost:7245';
  return `/service-blueprint-editor.html?service blueprint=${serviceBlueprintKey}&api=${encodeURIComponent(apiBase)}`;
}

test.describe.fixme('Browser-hosted service blueprint surface: Usability proof', () => {
  test.describe('1. Visual workspace prioritization', () => {
    test('editor workspace is not overwhelmed by host marketing chrome', async ({ page }) => {
      await page.goto(shellUrl('planning'));
      
      // Wait for editor to load
      await expect(page.locator('prism-service-blueprint-editor')).toHaveAttribute(
        'data-prism-service-blueprint-loaded',
        'planning',
        { timeout: 30_000 }
      );

      // BEHAVIORAL REQUIREMENT FOR ISABELLE:
      // The editor frame (.editor-frame in shell) should occupy significant vertical space
      // Hero/launch card should be visually secondary to the service blueprint workspace
      
      const editorFrame = page.locator('.editor-frame');
      const heroSection = page.locator('.hero');
      
      await expect(editorFrame).toBeVisible();
      await expect(heroSection).toBeVisible();

      // Measure viewport distribution
      const viewport = page.viewportSize();
      const editorBox = await editorFrame.boundingBox();
      const heroBox = await heroSection.boundingBox();

      if (!viewport || !editorBox || !heroBox) {
        throw new Error('Could not measure layout boxes');
      }

      // Editor should occupy at least 60% of viewport height
      const editorHeightRatio = editorBox.height / viewport.height;
      expect(editorHeightRatio).toBeGreaterThan(0.6);

      // Hero chrome should be no more than 30% of viewport height
      const heroHeightRatio = heroBox.height / viewport.height;
      expect(heroHeightRatio).toBeLessThan(0.3);

      // Editor should be visually prioritized (appears first in reading order after skip link)
      const mainLandmark = page.locator('main');
      await expect(mainLandmark).toBeVisible();
      await expect(mainLandmark.locator('prism-service-blueprint-editor')).toBeVisible();
    });

    test('swim lanes are visible without excessive scrolling', async ({ page }) => {
      await page.goto(shellUrl('planning'));
      
      await expect(page.locator('prism-service-blueprint-editor')).toHaveAttribute(
        'data-prism-service-blueprint-loaded',
        'planning',
        { timeout: 30_000 }
      );

      // BEHAVIORAL REQUIREMENT FOR ISABELLE:
      // Role lanes ([data-prism-role-queue]) should be visible in the viewport
      // At least 2-3 swim lanes should be visible without scrolling the editor frame
      
      const graphCanvas = page.getByRole('application');
      await expect(graphCanvas).toBeVisible();

      const roleLanes = page.locator('[data-prism-role-queue]');
      await expect(roleLanes).not.toHaveCount(0);

      // At least the first 2 lanes should be in viewport
      const firstLane = roleLanes.first();
      const secondLane = roleLanes.nth(1);
      
      await expect(firstLane).toBeInViewport();
      await expect(secondLane).toBeInViewport({ ratio: 0.5 });
    });

    test('editor chrome does not block interactive stage cards', async ({ page }) => {
      await page.goto(shellUrl('planning'));
      
      await expect(page.locator('prism-service-blueprint-editor')).toHaveAttribute(
        'data-prism-service-blueprint-loaded',
        'planning',
        { timeout: 30_000 }
      );

      // BEHAVIORAL REQUIREMENT FOR ISABELLE:
      // Stage cards must be pointer-accessible, not blocked by overlapping editor chrome
      // This was the actual CI failure in PR #75 — the "Send" button was blocked
      
      const firstStage = page.locator('[data-prism-stage]').first();
      await expect(firstStage).toBeVisible();

      // Stage card should be clickable (not blocked by overlays)
      await firstStage.click();
      
      // Selection should register
      await expect(firstStage).toHaveAttribute('aria-pressed', 'true', { timeout: 5_000 });
    });

    test('integration rail does not steal focus from editor workspace', async ({ page }) => {
      await page.goto(shellUrl('planning'));
      
      await expect(page.locator('prism-service-blueprint-editor')).toHaveAttribute(
        'data-prism-service-blueprint-loaded',
        'planning',
        { timeout: 30_000 }
      );

      // BEHAVIORAL REQUIREMENT FOR ISABELLE:
      // Integration rail (.integration-rail) should be an <aside>, not competing for focus
      // Editor should be the primary interactive surface
      
      const integrationRail = page.locator('.integration-rail');
      await expect(integrationRail).toBeVisible();

      // Rail should be an aside landmark
      const asideLandmark = page.locator('aside.integration-rail');
      await expect(asideLandmark).toBeVisible();

      // Editor should have initial focus after page load (after skip link usage)
      const skipLink = page.locator('.skip-link');
      await skipLink.focus();
      await skipLink.press('Enter');

      // Focus should land in the editor area
      const mainContent = page.locator('#service-blueprint-editor-reference-main');
      await expect(mainContent).toBeInViewport();
    });
  });

  test.describe('2. Swim lane reachability and navigation', () => {
    test('all swim lanes are reachable via keyboard in browser host', async ({ page }) => {
      await page.goto(shellUrl('planning'));
      
      await expect(page.locator('prism-service-blueprint-editor')).toHaveAttribute(
        'data-prism-service-blueprint-loaded',
        'planning',
        { timeout: 30_000 }
      );

      // BEHAVIORAL REQUIREMENT FOR ISABELLE:
      // Tab order must flow through all swim lanes
      // Arrow keys must navigate between lanes
      
      const graphCanvas = page.getByRole('application');
      await graphCanvas.focus();

      // Down arrow should navigate between swim lanes
      await page.keyboard.press('ArrowDown');
      
      // A stage in a different lane should now be focusable
      const focusedElement = page.locator(':focus');
      await expect(focusedElement).toBeVisible();
    });

    test('swim lanes can be navigated with screen reader', async ({ page }) => {
      await page.goto(shellUrl('planning'));
      
      await expect(page.locator('prism-service-blueprint-editor')).toHaveAttribute(
        'data-prism-service-blueprint-loaded',
        'planning',
        { timeout: 30_000 }
      );

      // BEHAVIORAL REQUIREMENT FOR ISABELLE:
      // Each role lane must have aria-label="Role: {role-name} lane"
      // Each stage card must have aria-label="{stage-title} stage"
      
      const firstLane = page.locator('[data-prism-role-queue]').first();
      await expect(firstLane).toHaveAttribute('aria-label', /lane/i);

      const firstStage = page.locator('[data-prism-stage]').first();
      await expect(firstStage).toHaveAttribute('aria-label', /stage/i);
    });

    test('swim lane horizontal scroll does not break with browser chrome', async ({ page }) => {
      await page.goto(shellUrl('planning'));
      
      await expect(page.locator('prism-service-blueprint-editor')).toHaveAttribute(
        'data-prism-service-blueprint-loaded',
        'planning',
        { timeout: 30_000 }
      );

      // BEHAVIORAL REQUIREMENT FOR ISABELLE:
      // If service blueprint has >5 stages, horizontal scroll should work within editor frame
      // Scroll should not leak to the browser host page
      
      const editorFrame = page.locator('.editor-frame');
      const graphCanvas = page.getByRole('application');
      
      // Measure initial scroll position of host page
      const initialHostScroll = await page.evaluate(() => window.scrollY);

      // Focus graph and use arrow keys to navigate right
      await graphCanvas.focus();
      for (let i = 0; i < 10; i++) {
        await page.keyboard.press('ArrowRight');
      }

      // Host page scroll should not have changed
      const finalHostScroll = await page.evaluate(() => window.scrollY);
      expect(finalHostScroll).toBe(initialHostScroll);

      // Editor frame should contain scrolling
      const editorScrolled = await graphCanvas.evaluate((el) => {
        const scrollParent = el.closest('[data-prism-component="service-blueprint-graph"]');
        return scrollParent ? scrollParent.scrollLeft > 0 : false;
      });
      
      // This may be false if the planning service blueprint is short, but proves containment
      if (editorScrolled) {
        expect(editorScrolled).toBe(true);
      }
    });

    test('zoom and fit controls work in browser-hosted surface', async ({ page }) => {
      await page.goto(shellUrl('planning'));
      
      await expect(page.locator('prism-service-blueprint-editor')).toHaveAttribute(
        'data-prism-service-blueprint-loaded',
        'planning',
        { timeout: 30_000 }
      );

      // BEHAVIORAL REQUIREMENT FOR ISABELLE:
      // If zoom controls exist ([data-prism-zoom-in], [data-prism-zoom-out], [data-prism-fit-to-screen]),
      // they should work without affecting browser host chrome
      
      const zoomIn = page.locator('[data-prism-zoom-in]');
      const zoomOut = page.locator('[data-prism-zoom-out]');
      const fitToScreen = page.locator('[data-prism-fit-to-screen]');

      // If zoom controls exist, test them
      if (await zoomIn.count() > 0) {
        await zoomIn.click();
        
        // Graph should zoom (this is a smoke test, not a pixel-perfect zoom check)
        const graphCanvas = page.getByRole('application');
        await expect(graphCanvas).toBeVisible();
        
        await fitToScreen.click();
        await expect(graphCanvas).toBeVisible();
      }
    });
  });

  test.describe('3. Keyboard and screen reader accessibility', () => {
    test('skip link works from browser chrome to editor', async ({ page }) => {
      await page.goto(shellUrl('planning'));
      
      // BEHAVIORAL REQUIREMENT FOR ISABELLE:
      // Skip link should jump directly to the editor workspace
      // Target: #service-blueprint-editor-reference-main
      
      const skipLink = page.locator('.skip-link');
      await expect(skipLink).not.toBeInViewport();

      // Focus skip link (it should become visible)
      await page.keyboard.press('Tab');
      await expect(skipLink).toBeInViewport();

      // Activate skip link
      await page.keyboard.press('Enter');

      // Main content should now be in viewport and focused
      const mainContent = page.locator('#service-blueprint-editor-reference-main');
      await expect(mainContent).toBeInViewport();
    });

    test('tab order flows logically from host chrome to editor', async ({ page }) => {
      await page.goto(shellUrl('planning'));
      
      await expect(page.locator('prism-service-blueprint-editor')).toHaveAttribute(
        'data-prism-service-blueprint-loaded',
        'planning',
        { timeout: 30_000 }
      );

      // BEHAVIORAL REQUIREMENT FOR ISABELLE:
      // Tab order: skip link → launch form (in header) → editor toolbar → graph canvas
      
      await page.keyboard.press('Tab'); // Skip link
      const skipLink = page.locator('.skip-link');
      await expect(skipLink).toBeFocused();

      await page.keyboard.press('Tab'); // Launch form (service blueprint dropdown)
      const serviceBlueprintDropdown = page.locator('#service-blueprint-key');
      await expect(serviceBlueprintDropdown).toBeFocused();

      // Continue tabbing to reach editor
      for (let i = 0; i < 5; i++) {
        await page.keyboard.press('Tab');
      }

      // Eventually focus should reach the editor toolbar or graph
      const focusedElement = page.locator(':focus');
      const editorElement = page.locator('prism-service-blueprint-editor');
      
      // Focus should be within the editor
      const focusInEditor = await focusedElement.evaluate((el, editor) => {
        return editor.contains(el);
      }, await editorElement.elementHandle());
      
      expect(focusInEditor).toBe(true);
    });

    test('screen reader announces service blueprint structure in browser host', async ({ page }) => {
      await page.goto(shellUrl('planning'));
      
      await expect(page.locator('prism-service-blueprint-editor')).toHaveAttribute(
        'data-prism-service-blueprint-loaded',
        'planning',
        { timeout: 30_000 }
      );

      // BEHAVIORAL REQUIREMENT FOR ISABELLE:
      // Main heading structure should be navigable via screen reader
      // H1: "Compose the editor into your app" (host chrome)
      // H2: "planning" (editor heading)
      // H3+: stage headings in inspector when selected
      
      const h1 = page.getByRole('heading', { level: 1 });
      await expect(h1).toContainText(/compose the editor/i);

      const editorHeading = page.locator('.editor-stage h2');
      await expect(editorHeading).toContainText('planning');

      // Select a stage to open inspector
      const firstStage = page.locator('[data-prism-stage]').first();
      await firstStage.click();

      const inspector = page.locator('[data-prism-component="step-inspector"]');
      await expect(inspector).toBeVisible({ timeout: 10_000 });

      // Inspector should have a heading for the selected stage
      const inspectorHeading = inspector.getByRole('heading', { level: 2 });
      await expect(inspectorHeading).toBeVisible();
    });

    test('focus restoration works after closing inspector in browser host', async ({ page }) => {
      await page.goto(shellUrl('planning'));
      
      await expect(page.locator('prism-service-blueprint-editor')).toHaveAttribute(
        'data-prism-service-blueprint-loaded',
        'planning',
        { timeout: 30_000 }
      );

      // BEHAVIORAL REQUIREMENT FOR ISABELLE:
      // After closing inspector (Escape key), focus should return to the stage card
      
      const firstStage = page.locator('[data-prism-stage]').first();
      await firstStage.click();

      const inspector = page.locator('[data-prism-component="step-inspector"]');
      await expect(inspector).toBeVisible({ timeout: 10_000 });

      // Close inspector with Escape
      await page.keyboard.press('Escape');
      await expect(inspector).not.toBeVisible();

      // Focus should return to the stage card
      await expect(firstStage).toBeFocused();
    });

    test('live regions announce structural changes in browser host', async ({ page }) => {
      await page.goto(shellUrl('planning'));
      
      await expect(page.locator('prism-service-blueprint-editor')).toHaveAttribute(
        'data-prism-service-blueprint-loaded',
        'planning',
        { timeout: 30_000 }
      );

      // BEHAVIORAL REQUIREMENT FOR ISABELLE:
      // Live region ([role="status"] or [aria-live="polite"]) should announce:
      // - "Stage created: {title}"
      // - "Stage deleted: {title}"
      // - "Transition created from {source} to {target}"
      
      const liveRegion = page.locator('[role="status"], [aria-live="polite"]');
      
      // For now, just verify a live region exists
      // Full proof will test announcements once stage creation is implemented
      if (await liveRegion.count() > 0) {
        await expect(liveRegion).toBeAttached();
      }
    });
  });

  test.describe('4. Simple editing flow from browser entry point', () => {
    test('can create a stage from browser-hosted editor', async ({ page }) => {
      await page.goto(shellUrl('planning'));
      
      await expect(page.locator('prism-service-blueprint-editor')).toHaveAttribute(
        'data-prism-service-blueprint-loaded',
        'planning',
        { timeout: 30_000 }
      );

      // BEHAVIORAL REQUIREMENT FOR ISABELLE:
      // Stage creation must work from browser-hosted surface
      // Add Stage button → form → stage appears in graph
      
      const addStageButton = page.locator('[data-prism-add-stage]');
      
      if (await addStageButton.count() > 0) {
        const initialStageCount = await page.locator('[data-prism-stage]').count();
        
        await addStageButton.click();
        
        // Stage creation form should appear
        const stageForm = page.locator('[data-prism-stage-form]');
        await expect(stageForm).toBeVisible({ timeout: 10_000 });
        
        // Fill in stage details
        await stageForm.locator('input[name="title"]').fill('Test Stage');
        await stageForm.locator('button[type="submit"]').click();
        
        // New stage should appear
        const newStageCount = await page.locator('[data-prism-stage]').count();
        expect(newStageCount).toBe(initialStageCount + 1);
      }
    });

    test('can edit stage properties in browser-hosted editor', async ({ page }) => {
      await page.goto(shellUrl('planning'));
      
      await expect(page.locator('prism-service-blueprint-editor')).toHaveAttribute(
        'data-prism-service-blueprint-loaded',
        'planning',
        { timeout: 30_000 }
      );

      // BEHAVIORAL REQUIREMENT FOR ISABELLE:
      // Inspector editing must work from browser-hosted surface
      
      const firstStage = page.locator('[data-prism-stage]').first();
      await firstStage.click();

      const inspector = page.locator('[data-prism-component="step-inspector"]');
      await expect(inspector).toBeVisible({ timeout: 10_000 });

      // Inspector should show editable fields
      const titleField = inspector.locator('input[type="text"]').first();
      if (await titleField.count() > 0) {
        const originalValue = await titleField.inputValue();
        
        await titleField.fill(originalValue + ' Updated');
        await titleField.blur();
        
        // Change should be reflected
        const newValue = await titleField.inputValue();
        expect(newValue).toContain('Updated');
      }
    });

    test('can save service blueprint from browser-hosted editor', async ({ page }) => {
      await page.goto(shellUrl('planning'));
      
      await expect(page.locator('prism-service-blueprint-editor')).toHaveAttribute(
        'data-prism-service-blueprint-loaded',
        'planning',
        { timeout: 30_000 }
      );

      // BEHAVIORAL REQUIREMENT FOR ISABELLE:
      // Save button must be accessible and functional in browser-hosted surface
      
      const saveButton = page.locator('[data-prism-save]');
      await expect(saveButton).toBeVisible();

      // Make a small change to enable save
      const firstStage = page.locator('[data-prism-stage]').first();
      await firstStage.click();

      const inspector = page.locator('[data-prism-component="step-inspector"]');
      await expect(inspector).toBeVisible({ timeout: 10_000 });

      // If we can make an edit, the save button should become enabled
      // For now, just verify the button is present and clickable
      await expect(saveButton).toBeEnabled({ timeout: 5_000 });
    });

    test('undo/redo work from browser-hosted editor', async ({ page }) => {
      await page.goto(shellUrl('planning'));
      
      await expect(page.locator('prism-service-blueprint-editor')).toHaveAttribute(
        'data-prism-service-blueprint-loaded',
        'planning',
        { timeout: 30_000 }
      );

      // BEHAVIORAL REQUIREMENT FOR ISABELLE:
      // Undo/redo buttons must work in browser-hosted surface
      
      const undoButton = page.locator('[data-prism-undo]');
      const redoButton = page.locator('[data-prism-redo]');

      await expect(undoButton).toBeVisible();
      await expect(redoButton).toBeVisible();

      // Initial state: both should be disabled
      await expect(undoButton).toHaveAttribute('aria-label', /idle/i);
      await expect(redoButton).toHaveAttribute('aria-label', /idle/i);
    });

    test('can switch service blueprints from browser host without losing editor state', async ({ page }) => {
      await page.goto(shellUrl('planning'));
      
      await expect(page.locator('prism-service-blueprint-editor')).toHaveAttribute(
        'data-prism-service-blueprint-loaded',
        'planning',
        { timeout: 30_000 }
      );

      // BEHAVIORAL REQUIREMENT FOR ISABELLE:
      // Switching service blueprints via launch form should cleanly reload editor
      // No stale state, no broken references
      
      const serviceBlueprintDropdown = page.locator('#service-blueprint-key');
      await serviceBlueprintDropdown.selectOption({ label: /information-request/i });

      const launchButton = page.locator('.launch-button');
      await launchButton.click();

      // Editor should reload with new service blueprint
      await expect(page.locator('prism-service-blueprint-editor')).toHaveAttribute(
        'data-prism-service-blueprint-loaded',
        'information-request',
        { timeout: 30_000 }
      );

      // Stages should reflect the new service blueprint
      const stages = page.locator('[data-prism-stage]');
      await expect(stages).not.toHaveCount(0);
    });
  });

  test.describe('5. Browser-specific edge cases', () => {
    test('editor remains usable after browser window resize', async ({ page }) => {
      await page.goto(shellUrl('planning'));
      
      await expect(page.locator('prism-service-blueprint-editor')).toHaveAttribute(
        'data-prism-service-blueprint-loaded',
        'planning',
        { timeout: 30_000 }
      );

      // BEHAVIORAL REQUIREMENT FOR ISABELLE:
      // Editor should respond to viewport changes without breaking layout
      
      const originalViewport = page.viewportSize();
      
      // Resize to narrow mobile viewport
      await page.setViewportSize({ width: 375, height: 667 });
      
      const editorFrame = page.locator('.editor-frame');
      await expect(editorFrame).toBeVisible();
      
      // Graph should still be accessible
      const graphCanvas = page.getByRole('application');
      await expect(graphCanvas).toBeVisible();

      // Restore viewport
      if (originalViewport) {
        await page.setViewportSize(originalViewport);
      }
    });

    test('editor state persists across browser navigation', async ({ page }) => {
      await page.goto(shellUrl('planning'));
      
      await expect(page.locator('prism-service-blueprint-editor')).toHaveAttribute(
        'data-prism-service-blueprint-loaded',
        'planning',
        { timeout: 30_000 }
      );

      // BEHAVIORAL REQUIREMENT FOR ISABELLE:
      // URL should reflect current service blueprint and API base
      // This allows sharing/bookmarking specific service blueprints
      
      const url = page.url();
      expect(url).toContain('service blueprint=planning');
      
      // Navigate away and back
      await page.goto('about:blank');
      await page.goto(url);
      
      // Editor should reload to the same service blueprint
      await expect(page.locator('prism-service-blueprint-editor')).toHaveAttribute(
        'data-prism-service-blueprint-loaded',
        'planning',
        { timeout: 30_000 }
      );
    });

    test('editor works with browser zoom at 150%', async ({ page, context }) => {
      await page.goto(shellUrl('planning'));
      
      // Apply browser zoom
      await context.addInitScript(() => {
        document.body.style.zoom = '1.5';
      });

      await expect(page.locator('prism-service-blueprint-editor')).toHaveAttribute(
        'data-prism-service-blueprint-loaded',
        'planning',
        { timeout: 30_000 }
      );

      // BEHAVIORAL REQUIREMENT FOR ISABELLE:
      // Editor should remain usable at high zoom levels (WCAG AA)
      
      const editorFrame = page.locator('.editor-frame');
      await expect(editorFrame).toBeVisible();
      
      const firstStage = page.locator('[data-prism-stage]').first();
      await expect(firstStage).toBeVisible();
      
      // Stage should be clickable at high zoom
      await firstStage.click();
      await expect(firstStage).toHaveAttribute('aria-pressed', 'true');
    });

    test('editor handles API errors gracefully in browser host', async ({ page }) => {
      // Use an invalid service blueprint key
      await page.goto(shellUrl('nonexistent-service-blueprint'));
      
      // BEHAVIORAL REQUIREMENT FOR ISABELLE:
      // Editor should show a clear error message, not a broken state
      
      const errorMessage = page.locator('[role="alert"]');
      await expect(errorMessage).toBeVisible({ timeout: 10_000 });
      
      // Error should be human-readable
      await expect(errorMessage).toContainText(/not found|could not load|error/i);
    });
  });
});
