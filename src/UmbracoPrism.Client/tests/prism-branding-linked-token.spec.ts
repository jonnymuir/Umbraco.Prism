import { test, expect } from '@playwright/test';

// The Edit story's "General Styles" tab (see prism-mobile-branding-inheritance.spec.ts for the
// two base variables it already defines). This suite adds a third, linked, variable to the
// mocked metadata response to exercise the "Linked to X" picker (see _renderDynamicField /
// renderValueControl in prism-create-tenant-modal.ts) without needing a real prism-govuk-bridge.css
// file loaded — a var(--x, fallback) value is all the rendering path needs to detect a link.
const editStoryUrl = '/?path=/story/prism-create-tenant-modal--edit';

const mockBrandingMetadata = {
  sections: [
    {
      name: 'General Styles',
      variables: [
        { variable: '--color-primary', label: 'Primary Color', description: 'Brand primary colour', type: 'color', syntax: '<color>', currentValue: '#3544b1' },
        { variable: '--color-surface', label: 'Surface Color', description: 'Card/surface background', type: 'color', syntax: '<color>', currentValue: '#ffffff' },
        { variable: '--color-link', label: 'Link Color', description: 'Linked to the primary brand colour', type: 'color', syntax: '<color>', currentValue: 'var(--color-primary, #3544b1)' }
      ]
    }
  ]
};

const setupMetadataMock = async (page: import('@playwright/test').Page) => {
  await page.route('**/prism/branding/metadata*', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(mockBrandingMetadata) });
  });
};

// The Edit story's own `brandingTabs` prop (not the mocked metadata) is what
// _renderDynamicBrandingTab filters displayed variables against, so --color-link needs adding
// there too, or it fetches metadata for it but never renders it.
const addLinkedVariableToStoryTabs = async (modal: ReturnType<typeof import('@playwright/test').Page.prototype.frameLocator>['locator']) => {
  await modal.evaluate((el: any) => {
    const data = el.data;
    const tabs = data.brandingTabs.map((t: any) =>
      t.label === 'General Styles'
        ? { ...t, variables: [...t.variables, { name: '--color-link' }] }
        : t
    );
    el.data = { ...data, brandingTabs: tabs };
  });
};

const switchToGeneralStyles = async (page: import('@playwright/test').Page, modal: ReturnType<typeof import('@playwright/test').Page.prototype.frameLocator>['locator']) => {
  await modal.evaluate((el: Element) => {
    const tab = el.shadowRoot?.querySelector('uui-tab[label="General Styles"]') as HTMLElement | null;
    tab?.click();
  });
  await page.waitForTimeout(800);
};

test.describe('Branding editor: linked token picker', () => {
  test('A variable whose value is var(--x) shows a "Linked to" badge, not a broken colour picker', async ({ page }) => {
    await setupMetadataMock(page);
    await page.goto(editStoryUrl);

    const frame = page.frameLocator('#storybook-preview-iframe');
    const modal = frame.locator('prism-create-tenant-modal');
    await expect(modal).toBeVisible();

    await addLinkedVariableToStoryTabs(modal);
    await switchToGeneralStyles(page, modal);

    const result = await modal.evaluate((el: Element) => {
      const shadow = el.shadowRoot;
      const badge = shadow?.querySelector('[data-testid="link-badge---color-link"]') as HTMLElement | null;
      const customiseBtn = shadow?.querySelector('[data-testid="link-customise---color-link"]') as HTMLElement | null;
      const swatch = badge?.querySelector('span') as HTMLElement | null;

      return {
        badgeText: badge?.textContent?.trim() ?? null,
        customisePresent: customiseBtn !== null,
        swatchBackground: swatch ? window.getComputedStyle(swatch).backgroundColor : null
      };
    });

    expect(result.badgeText).toContain('Linked to Primary Color');
    expect(result.customisePresent).toBe(true);
    // #3544b1 -> rgb(53, 68, 177), proves the raw var(--color-primary, ...) text was resolved to
    // the live Primary Color value for the preview swatch, not left as unparsed text.
    expect(result.swatchBackground).toBe('rgb(53, 68, 177)');
  });

  test('Customise reveals a token picker preselected to the current link target', async ({ page }) => {
    await setupMetadataMock(page);
    await page.goto(editStoryUrl);

    const frame = page.frameLocator('#storybook-preview-iframe');
    const modal = frame.locator('prism-create-tenant-modal');
    await expect(modal).toBeVisible();

    await addLinkedVariableToStoryTabs(modal);
    await switchToGeneralStyles(page, modal);

    await modal.evaluate((el: Element) => {
      const btn = el.shadowRoot?.querySelector('[data-testid="link-customise---color-link"]') as HTMLElement | null;
      btn?.click();
    });
    await page.waitForTimeout(100);

    const result = await modal.evaluate((el: Element) => {
      const shadow = el.shadowRoot;
      const picker = shadow?.querySelector('[data-testid="link-picker---color-link"]') as (HTMLElement & { value?: string }) | null;
      const native = picker?.shadowRoot?.querySelector('#native') as HTMLSelectElement | null;
      const optionValues = native ? Array.from(native.options).map(o => o.value) : [];

      return {
        pickerPresent: picker !== null,
        selectedValue: native?.value ?? null,
        optionValues
      };
    });

    expect(result.pickerPresent).toBe(true);
    expect(result.selectedValue).toBe('--color-primary');
    expect(result.optionValues).toContain('__custom__');
    expect(result.optionValues).toContain('--color-surface');
    // Cannot link to itself
    expect(result.optionValues).not.toContain('--color-link');
  });

  test('Picking a different token repoints the link', async ({ page }) => {
    await setupMetadataMock(page);
    await page.goto(editStoryUrl);

    const frame = page.frameLocator('#storybook-preview-iframe');
    const modal = frame.locator('prism-create-tenant-modal');
    await expect(modal).toBeVisible();

    await addLinkedVariableToStoryTabs(modal);
    await switchToGeneralStyles(page, modal);

    await modal.evaluate((el: Element) => {
      const btn = el.shadowRoot?.querySelector('[data-testid="link-customise---color-link"]') as HTMLElement | null;
      btn?.click();
    });
    await page.waitForTimeout(100);

    await modal.evaluate((el: Element) => {
      const picker = el.shadowRoot?.querySelector('[data-testid="link-picker---color-link"]') as HTMLElement | null;
      const native = picker?.shadowRoot?.querySelector('#native') as HTMLSelectElement | null;
      if (native) {
        native.value = '--color-surface';
        native.dispatchEvent(new Event('change', { bubbles: true }));
      }
    });
    await page.waitForTimeout(100);

    const result = await modal.evaluate((el: Element) => {
      const picker = el.shadowRoot?.querySelector('[data-testid="link-picker---color-link"]') as (HTMLElement & { value?: string }) | null;
      const native = picker?.shadowRoot?.querySelector('#native') as HTMLSelectElement | null;
      return { selectedValue: native?.value ?? null };
    });

    expect(result.selectedValue).toBe('--color-surface');
  });

  test('Choosing "Custom value" drops to a literal colour input seeded with the resolved colour', async ({ page }) => {
    await setupMetadataMock(page);
    await page.goto(editStoryUrl);

    const frame = page.frameLocator('#storybook-preview-iframe');
    const modal = frame.locator('prism-create-tenant-modal');
    await expect(modal).toBeVisible();

    await addLinkedVariableToStoryTabs(modal);
    await switchToGeneralStyles(page, modal);

    await modal.evaluate((el: Element) => {
      const btn = el.shadowRoot?.querySelector('[data-testid="link-customise---color-link"]') as HTMLElement | null;
      btn?.click();
    });
    await page.waitForTimeout(100);

    await modal.evaluate((el: Element) => {
      const picker = el.shadowRoot?.querySelector('[data-testid="link-picker---color-link"]') as HTMLElement | null;
      const native = picker?.shadowRoot?.querySelector('#native') as HTMLSelectElement | null;
      if (native) {
        native.value = '__custom__';
        native.dispatchEvent(new Event('change', { bubbles: true }));
      }
    });
    await page.waitForTimeout(100);

    const result = await modal.evaluate((el: Element) => {
      const shadow = el.shadowRoot;
      const picker = shadow?.querySelector('[data-testid="link-picker---color-link"]');
      const badge = shadow?.querySelector('[data-testid="link-badge---color-link"]');
      const colorInput = shadow?.querySelector('input[type="color"][aria-label^="Link Color"]') as HTMLInputElement | null;

      return {
        pickerGone: picker === null,
        badgeGone: badge === null,
        colorInputValue: colorInput?.value ?? null
      };
    });

    expect(result.pickerGone).toBe(true);
    expect(result.badgeGone).toBe(true);
    expect(result.colorInputValue).toBe('#3544b1');
  });
});
