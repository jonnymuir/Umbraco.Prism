import { test, expect } from '@playwright/test';

// The Edit story has two variables in "General Styles":
//   --color-primary  → mobileOverrideValue: '#003399'  (chain should load BROKEN)
//   --color-surface  → no mobileOverrideValue           (chain should load INTACT)
const editStoryUrl = '/?path=/story/prism-create-tenant-modal--edit';

// Mock metadata matching the Edit story fixture data so the dynamic rendering path activates
const mockBrandingMetadata = {
  sections: [
    {
      name: 'General Styles',
      variables: [
        { variable: '--color-primary', label: 'Primary Color', description: 'Brand primary colour', type: 'color', syntax: '<color>', currentValue: '#3544b1' },
        { variable: '--color-surface', label: 'Surface Color', description: 'Card/surface background', type: 'color', syntax: '<color>', currentValue: '#ffffff' }
      ]
    }
  ]
};

const setupMetadataMock = async (page: import('@playwright/test').Page) => {
  await page.route('**/prism/branding/metadata*', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(mockBrandingMetadata) });
  });
};

const switchToGeneralStyles = async (page: import('@playwright/test').Page, modal: ReturnType<typeof import('@playwright/test').Page.prototype.frameLocator>['locator']) => {
  await modal.evaluate((el: Element) => {
    const tab = el.shadowRoot?.querySelector('uui-tab[label="General Styles"]') as HTMLElement | null;
    tab?.click();
  });
  // Wait for dynamic rendering: auth context timeout (500ms) + fetch + re-render
  await page.waitForTimeout(800);
};

test.describe('Mobile branding inheritance', () => {
  test('Mobile variable inherits desktop value by default', async ({ page }) => {
    await setupMetadataMock(page);
    await page.goto(editStoryUrl);

    const frame = page.frameLocator('#storybook-preview-iframe');
    const modal = frame.locator('prism-create-tenant-modal');
    await expect(modal).toBeVisible();

    await switchToGeneralStyles(page, modal);

    // --color-surface has no mobileOverrideValue so should be in INHERIT mode
    const result = await modal.evaluate((el: Element) => {
      const shadow = el.shadowRoot;
      const toggleBtn = shadow?.querySelector('[data-testid="mobile-inherit-toggle---color-surface"]') as HTMLElement | null;
      const inheritLabel = shadow?.querySelector('[data-testid="mobile-inherit-label---color-surface"]') as HTMLElement | null;
      const mobileField = shadow?.querySelector('[data-testid="mobile-field---color-surface"]') as HTMLElement | null;

      const toggleLabel = toggleBtn?.getAttribute('label') ?? toggleBtn?.getAttribute('aria-label') ?? null;
      const inheritLabelPresent = inheritLabel !== null;
      const inheritLabelVisible = inheritLabel
        ? window.getComputedStyle(inheritLabel).display !== 'none' && window.getComputedStyle(inheritLabel).visibility !== 'hidden'
        : false;

      const mobileInput = mobileField?.querySelector('uui-input') as HTMLElement | null;
      const pointerEvents = mobileInput ? window.getComputedStyle(mobileInput).pointerEvents : null;
      const isDisabled = mobileInput?.hasAttribute('disabled') ?? false;

      return { toggleLabel, inheritLabelPresent, inheritLabelVisible, pointerEvents, isDisabled };
    });

    expect(result.toggleLabel).toBe('Break mobile inheritance');
    expect(result.inheritLabelPresent).toBe(true);
    expect(result.inheritLabelVisible).toBe(true);
    // Mobile input must be non-interactive while inheriting
    expect(result.pointerEvents === 'none' || result.isDisabled).toBe(true);
  });

  test('Breaking inheritance enables independent mobile input', async ({ page }) => {
    await setupMetadataMock(page);
    await page.goto(editStoryUrl);

    const frame = page.frameLocator('#storybook-preview-iframe');
    const modal = frame.locator('prism-create-tenant-modal');
    await expect(modal).toBeVisible();

    await switchToGeneralStyles(page, modal);

    // Click the 🔗 toggle on --color-surface to break inheritance
    await modal.evaluate((el: Element) => {
      const toggleBtn = el.shadowRoot?.querySelector('[data-testid="mobile-inherit-toggle---color-surface"]') as HTMLElement | null;
      toggleBtn?.click();
    });

    // Allow the component to re-render
    await page.waitForTimeout(100);

    const result = await modal.evaluate((el: Element) => {
      const shadow = el.shadowRoot;
      const toggleBtn = shadow?.querySelector('[data-testid="mobile-inherit-toggle---color-surface"]') as HTMLElement | null;
      const inheritLabel = shadow?.querySelector('[data-testid="mobile-inherit-label---color-surface"]') as HTMLElement | null;
      const mobileField = shadow?.querySelector('[data-testid="mobile-field---color-surface"]') as HTMLElement | null;

      const toggleLabel = toggleBtn?.getAttribute('label') ?? toggleBtn?.getAttribute('aria-label') ?? null;
      const inheritLabelVisible = inheritLabel
        ? window.getComputedStyle(inheritLabel).display !== 'none' && window.getComputedStyle(inheritLabel).visibility !== 'hidden'
        : false;

      // Look for the "custom" badge — it sits in the header row above the mobile field
      const customBadge = shadow?.querySelector('[data-testid="mobile-custom-badge---color-surface"]') as HTMLElement | null;
      const customBadgeVisible = customBadge !== null;

      const mobileInput = mobileField?.querySelector('uui-input') as HTMLElement | null;
      const pointerEvents = mobileInput ? window.getComputedStyle(mobileInput).pointerEvents : null;
      const isDisabled = mobileInput?.hasAttribute('disabled') ?? false;

      // Pre-population: the input value should match the desktop override value for --color-surface
      // (no desktop override = '' or the defaultValue '#ffffff')
      const mobileInputValue = (mobileInput as HTMLInputElement | null)?.value ?? null;

      return { toggleLabel, inheritLabelVisible, customBadgeVisible, pointerEvents, isDisabled, mobileInputValue };
    });

    expect(result.toggleLabel).toBe('Restore mobile inheritance');
    expect(result.inheritLabelVisible).toBe(false);
    expect(result.customBadgeVisible).toBe(true);
    // Mobile input must now be interactive
    expect(result.pointerEvents !== 'none' && !result.isDisabled).toBe(true);
  });

  test('Restoring inheritance re-links mobile to desktop value', async ({ page }) => {
    await setupMetadataMock(page);
    await page.goto(editStoryUrl);

    const frame = page.frameLocator('#storybook-preview-iframe');
    const modal = frame.locator('prism-create-tenant-modal');
    await expect(modal).toBeVisible();

    await switchToGeneralStyles(page, modal);

    // Break inheritance first
    await modal.evaluate((el: Element) => {
      const toggleBtn = el.shadowRoot?.querySelector('[data-testid="mobile-inherit-toggle---color-surface"]') as HTMLElement | null;
      toggleBtn?.click();
    });
    await page.waitForTimeout(100);

    // Now restore it
    await modal.evaluate((el: Element) => {
      const toggleBtn = el.shadowRoot?.querySelector('[data-testid="mobile-inherit-toggle---color-surface"]') as HTMLElement | null;
      toggleBtn?.click();
    });
    await page.waitForTimeout(100);

    const result = await modal.evaluate((el: Element) => {
      const shadow = el.shadowRoot;
      const toggleBtn = shadow?.querySelector('[data-testid="mobile-inherit-toggle---color-surface"]') as HTMLElement | null;
      const inheritLabel = shadow?.querySelector('[data-testid="mobile-inherit-label---color-surface"]') as HTMLElement | null;
      const mobileField = shadow?.querySelector('[data-testid="mobile-field---color-surface"]') as HTMLElement | null;

      const toggleLabel = toggleBtn?.getAttribute('label') ?? toggleBtn?.getAttribute('aria-label') ?? null;
      const inheritLabelVisible = inheritLabel
        ? window.getComputedStyle(inheritLabel).display !== 'none' && window.getComputedStyle(inheritLabel).visibility !== 'hidden'
        : false;

      const mobileInput = mobileField?.querySelector('uui-input') as HTMLElement | null;
      const pointerEvents = mobileInput ? window.getComputedStyle(mobileInput).pointerEvents : null;
      const isDisabled = mobileInput?.hasAttribute('disabled') ?? false;

      return { toggleLabel, inheritLabelVisible, pointerEvents, isDisabled };
    });

    expect(result.toggleLabel).toBe('Break mobile inheritance');
    expect(result.inheritLabelVisible).toBe(true);
    expect(result.pointerEvents === 'none' || result.isDisabled).toBe(true);
  });

  test('Tenant with saved mobile overrides loads with those variables showing as custom', async ({ page }) => {
    await setupMetadataMock(page);
    await page.goto(editStoryUrl);

    const frame = page.frameLocator('#storybook-preview-iframe');
    const modal = frame.locator('prism-create-tenant-modal');
    await expect(modal).toBeVisible();

    await switchToGeneralStyles(page, modal);

    const result = await modal.evaluate((el: Element) => {
      const shadow = el.shadowRoot;

      // --color-primary has mobileOverrideValue '#003399' → chain should be BROKEN (⛓️)
      const primaryToggle = shadow?.querySelector('[data-testid="mobile-inherit-toggle---color-primary"]') as HTMLElement | null;
      const primaryInheritLabel = shadow?.querySelector('[data-testid="mobile-inherit-label---color-primary"]') as HTMLElement | null;

      const primaryToggleLabel = primaryToggle?.getAttribute('label') ?? primaryToggle?.getAttribute('aria-label') ?? null;
      const primaryInheritVisible = primaryInheritLabel
        ? window.getComputedStyle(primaryInheritLabel).display !== 'none' && window.getComputedStyle(primaryInheritLabel).visibility !== 'hidden'
        : false;

      // --color-surface has no mobileOverrideValue → chain should be INTACT (🔗)
      const surfaceToggle = shadow?.querySelector('[data-testid="mobile-inherit-toggle---color-surface"]') as HTMLElement | null;
      const surfaceInheritLabel = shadow?.querySelector('[data-testid="mobile-inherit-label---color-surface"]') as HTMLElement | null;

      const surfaceToggleLabel = surfaceToggle?.getAttribute('label') ?? surfaceToggle?.getAttribute('aria-label') ?? null;
      const surfaceInheritVisible = surfaceInheritLabel
        ? window.getComputedStyle(surfaceInheritLabel).display !== 'none' && window.getComputedStyle(surfaceInheritLabel).visibility !== 'hidden'
        : false;

      return {
        primaryToggleLabel,
        primaryInheritVisible,
        surfaceToggleLabel,
        surfaceInheritVisible
      };
    });

    // Variable WITH saved mobile override → chain broken, shows ⛓️ "Restore"
    expect(result.primaryToggleLabel).toBe('Restore mobile inheritance');
    expect(result.primaryInheritVisible).toBe(false);

    // Variable WITHOUT saved mobile override → chain intact, shows 🔗 "Break"
    expect(result.surfaceToggleLabel).toBe('Break mobile inheritance');
    expect(result.surfaceInheritVisible).toBe(true);
  });
});
