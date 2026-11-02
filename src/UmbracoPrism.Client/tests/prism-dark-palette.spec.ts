import { test, expect, type Page } from '@playwright/test';

// The Edit story's tenant has a dark palette with one colour: --color-primary is #8fb4ff in dark
// mode. --color-surface has no dark value, so it is the same as light.
const editStoryUrl = '/?path=/story/prism-create-tenant-modal--edit';

const metadata = {
  sections: [
    {
      name: 'General Styles',
      variables: [
        { variable: '--color-primary', label: 'Primary Color', description: 'Brand primary colour', type: 'color', syntax: '<color>', currentValue: '#3544b1' },
        { variable: '--color-surface', label: 'Surface Color', description: 'Card/surface background', type: 'color', syntax: '<color>', currentValue: '#ffffff' },
        { variable: '--font-body', label: 'Body Font', description: 'Body font family', type: 'text', syntax: '*', currentValue: 'Arial' }
      ]
    }
  ]
};

async function openGeneralStyles(page: Page) {
  await page.route('**/prism/branding/metadata*', route =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(metadata) }));
  await page.goto(editStoryUrl);
  const modal = page.frameLocator('#storybook-preview-iframe').locator('prism-create-tenant-modal');
  await expect(modal).toBeVisible();
  await modal.evaluate((el: Element) => (el.shadowRoot?.querySelector('uui-tab[label="General Styles"]') as HTMLElement | null)?.click());
  // Dynamic rendering waits for the auth context (500ms) and the metadata request.
  await page.waitForTimeout(800);
  return modal;
}


test.describe('Dark palette', () => {
  test('A colour is the same as light in dark mode until it is customised', async ({ page }) => {
    const modal = await openGeneralStyles(page);

    const state = await modal.evaluate((el: Element) => ({
      sameAsLight: el.shadowRoot?.querySelector('[data-testid="dark-same-label---color-surface"]') !== null,
      field: el.shadowRoot?.querySelector('[data-testid="dark-field---color-surface"]') !== null,
      textHasDarkRow: el.shadowRoot?.querySelector('[data-testid="dark-toggle---font-body"]') !== null
    }));

    expect(state.sameAsLight).toBe(true);
    expect(state.field).toBe(false);
    expect(state.textHasDarkRow).toBe(false);
  });

  test('A dark value the tenant saved loads as customised and is saved again', async ({ page }) => {
    const modal = await openGeneralStyles(page);

    const state = await modal.evaluate((el: Element) => ({
      custom: el.shadowRoot?.querySelector('[data-testid="dark-custom-badge---color-primary"]') !== null,
      payload: (el as any)._buildTenantPayload().darkBrandingOverrides
    }));

    expect(state.custom).toBe(true);
    expect(state.payload).toEqual({ '--color-primary': '#8fb4ff' });
  });

  test('Customising a colour for dark mode adds it to the dark palette, and resetting removes it', async ({ page }) => {
    const modal = await openGeneralStyles(page);

    await modal.evaluate((el: Element) => (el.shadowRoot?.querySelector('[data-testid="dark-toggle---color-surface"]') as HTMLElement).click());
    await page.waitForTimeout(100);
    await modal.evaluate((el: Element) => {
      const input = el.shadowRoot?.querySelector('[data-testid="dark-field---color-surface"] uui-input') as HTMLInputElement;
      input.value = '#101820';
      input.dispatchEvent(new Event('input', { bubbles: true, composed: true }));
    });
    await page.waitForTimeout(100);

    const customised = await modal.evaluate((el: Element) => (el as any)._buildTenantPayload().darkBrandingOverrides);
    expect(customised).toEqual({ '--color-primary': '#8fb4ff', '--color-surface': '#101820' });

    await modal.evaluate((el: Element) => (el.shadowRoot?.querySelector('[data-testid="dark-toggle---color-surface"]') as HTMLElement).click());
    await page.waitForTimeout(100);

    const reset = await modal.evaluate((el: Element) => (el as any)._buildTenantPayload().darkBrandingOverrides);
    expect(reset).toEqual({ '--color-primary': '#8fb4ff' });
  });
});
