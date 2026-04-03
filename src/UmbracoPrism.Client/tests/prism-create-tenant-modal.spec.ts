import { test, expect } from '@playwright/test';

const createStoryUrl = '/?path=/story/prism-create-tenant-modal--create';
const editStoryUrl = '/?path=/story/prism-create-tenant-modal--edit';

test('Create modal tabs switch and content has height', async ({ page }) => {
  await page.goto(createStoryUrl);

  const frame = page.frameLocator('#storybook-preview-iframe');
  const modal = frame.locator('prism-create-tenant-modal');
  await expect(modal).toBeVisible();

  const switchToTab = async (label: 'General' | 'Identity') => {
    await modal.evaluate((el, tabLabel) => {
      const tab = el.shadowRoot?.querySelector(`uui-tab[label="${tabLabel}"]`) as HTMLElement | null;
      tab?.click();
    }, label);
  };

  const container = modal.locator('.container');
  await expect(container).toBeVisible();
  const containerHeight = await container.evaluate((el) => el.getBoundingClientRect().height);
  expect(containerHeight).toBeGreaterThanOrEqual(350);

  await switchToTab('Identity');
  await expect(frame.getByText('Directory (Tenant) ID')).toBeVisible();

  const identityPanel = modal.locator('div[role="tabpanel"]', { hasText: 'Directory (Tenant) ID' });
  await expect(identityPanel).toBeVisible();
  const identityHeight = await identityPanel.evaluate((el) => el.getBoundingClientRect().height);
  expect(identityHeight).toBeGreaterThan(0);

  await switchToTab('General');
  await expect(frame.getByText('Tenant Name')).toBeVisible();

  const generalPanel = modal.locator('div[role="tabpanel"]', { hasText: 'Tenant Name' });
  await expect(generalPanel).toBeVisible();
  const generalHeight = await generalPanel.evaluate((el) => el.getBoundingClientRect().height);
  expect(generalHeight).toBeGreaterThan(0);
});

test('Edit modal tabs switch and content has height', async ({ page }) => {
  await page.goto(editStoryUrl);

  const frame = page.frameLocator('#storybook-preview-iframe');
  const modal = frame.locator('prism-create-tenant-modal');
  await expect(modal).toBeVisible();

  const switchToTab = async (label: 'General' | 'Identity') => {
    await modal.evaluate((el, tabLabel) => {
      const tab = el.shadowRoot?.querySelector(`uui-tab[label="${tabLabel}"]`) as HTMLElement | null;
      tab?.click();
    }, label);
  };

  await expect(frame.getByText('Edit Tenant')).toBeVisible();

  const container = modal.locator('.container');
  await expect(container).toBeVisible();
  const containerHeight = await container.evaluate((el) => el.getBoundingClientRect().height);
  expect(containerHeight).toBeGreaterThanOrEqual(350);

  await switchToTab('General');
  await expect(frame.getByText('Tenant Name')).toBeVisible();

  const generalPanel = modal.locator('div[role="tabpanel"]', { hasText: 'Tenant Name' });
  await expect(generalPanel).toBeVisible();
  const generalHeight = await generalPanel.evaluate((el) => el.getBoundingClientRect().height);
  expect(generalHeight).toBeGreaterThan(0);

  await switchToTab('Identity');
  await expect(frame.getByText('Directory (Tenant) ID')).toBeVisible();

  const identityPanel = modal.locator('div[role="tabpanel"]', { hasText: 'Directory (Tenant) ID' });
  await expect(identityPanel).toBeVisible();
  const identityHeight = await identityPanel.evaluate((el) => el.getBoundingClientRect().height);
  expect(identityHeight).toBeGreaterThan(0);
});

test('Edit modal shows branding tabs', async ({ page }) => {
  await page.goto(editStoryUrl);

  const frame = page.frameLocator('#storybook-preview-iframe');
  const modal = frame.locator('prism-create-tenant-modal');
  await expect(modal).toBeVisible();

  await modal.evaluate((el) => {
    const tab = el.shadowRoot?.querySelector('uui-tab[label="General Styles"]') as HTMLElement | null;
    tab?.click();
  });

  await expect(frame.getByText('--color-primary')).toBeVisible();

  await modal.evaluate((el) => {
    const tab = el.shadowRoot?.querySelector('uui-tab[label="Other Styles"]') as HTMLElement | null;
    tab?.click();
  });

  await expect(frame.getByText('--custom-border')).toBeVisible();
});

test('Edit modal branding table shows mobile override column and value', async ({ page }) => {
  await page.goto(editStoryUrl);

  const frame = page.frameLocator('#storybook-preview-iframe');
  const modal = frame.locator('prism-create-tenant-modal');
  await expect(modal).toBeVisible();

  await modal.evaluate((el) => {
    const tab = el.shadowRoot?.querySelector('uui-tab[label="General Styles"]') as HTMLElement | null;
    tab?.click();
  });

  await expect(frame.getByRole('columnheader', { name: 'Mobile' })).toBeVisible();

  const mobileValue = await modal.evaluate((el) => {
    const mobileInput = el.shadowRoot?.querySelector(
      '#branding-panel-0 uui-table-row:first-of-type uui-table-cell:nth-of-type(4) uui-input'
    ) as HTMLInputElement | null;
    return mobileInput?.value ?? '';
  });

  expect(mobileValue).toBe('#003399');
});

test('Edit modal allows editing mobile override value', async ({ page }) => {
  await page.goto(editStoryUrl);

  const frame = page.frameLocator('#storybook-preview-iframe');
  const modal = frame.locator('prism-create-tenant-modal');
  await expect(modal).toBeVisible();

  await modal.evaluate((el) => {
    const tab = el.shadowRoot?.querySelector('uui-tab[label="General Styles"]') as HTMLElement | null;
    tab?.click();
  });

  const updatedValue = '#111111';
  const mobileValue = await modal.evaluate((el, nextValue) => {
    const mobileInput = el.shadowRoot?.querySelector(
      '#branding-panel-0 uui-table-row:first-of-type uui-table-cell:nth-of-type(4) uui-input'
    ) as HTMLInputElement | null;

    if (!mobileInput) return '';

    mobileInput.value = nextValue;
    mobileInput.dispatchEvent(new InputEvent('input', { bubbles: true, composed: true }));
    return mobileInput.value;
  }, updatedValue);

  expect(mobileValue).toBe(updatedValue);
});

test('Edit modal hydrates all Produce Mobile values from persisted config', async ({ page }) => {
  await page.goto(editStoryUrl);

  const frame = page.frameLocator('#storybook-preview-iframe');
  const modal = frame.locator('prism-create-tenant-modal');
  await expect(modal).toBeVisible();

  await modal.evaluate((el) => {
    const tab = el.shadowRoot?.querySelector('uui-tab[label="Produce Mobile"]') as HTMLElement | null;
    tab?.click();
  });

  const values = await modal.evaluate((el) => {
    const read = (id: string) => (el.shadowRoot?.querySelector(`#${id}`) as HTMLInputElement | null)?.value ?? '';
    const diagnostics = (el.shadowRoot?.querySelector('uui-checkbox[label="Show technical diagnostics"]') as HTMLInputElement | null)?.checked ?? false;

    return {
      appName: read('mobile-app-name'),
      appId: read('mobile-app-id'),
      version: read('mobile-version'),
      startUrl: read('mobile-start-url'),
      userAgentMarker: read('mobile-ua-marker'),
      iconUrl: read('mobile-icon-url'),
      splashUrl: read('mobile-splash-url'),
      errorBackgroundColor: read('mobile-error-bg'),
      errorTextColor: read('mobile-error-text'),
      errorTitle: read('mobile-error-title'),
      errorMessage: read('mobile-error-message'),
      showDiagnostics: diagnostics
    };
  });

  expect(values).toEqual({
    appName: 'Northwind Portal',
    appId: 'com.northwind.portal',
    version: '2.3.4',
    startUrl: 'https://northwind.example/app',
    userAgentMarker: 'PrismMobileNW',
    iconUrl: 'https://northwind.example/media/icon.png',
    splashUrl: 'https://northwind.example/media/splash.png',
    errorBackgroundColor: '#111827',
    errorTextColor: '#f3f4f6',
    errorTitle: 'Cannot connect right now',
    errorMessage: 'Please try again in a moment.',
    showDiagnostics: false
  });
});

test('Produce Mobile tab shows push notifications toggle', async ({ page }) => {
  await page.goto(editStoryUrl);

  const frame = page.frameLocator('#storybook-preview-iframe');
  const modal = frame.locator('prism-create-tenant-modal');
  await expect(modal).toBeVisible();

  await modal.evaluate((el) => {
    const tab = el.shadowRoot?.querySelector('uui-tab[label="Produce Mobile"]') as HTMLElement | null;
    tab?.click();
  });

  const pushToggle = await modal.evaluate((el) => {
    const toggle = el.shadowRoot?.querySelector('input[aria-label="Push Notifications"]') as HTMLInputElement | null;
    return toggle ? { visible: true, checked: toggle.checked } : { visible: false, checked: false };
  });

  expect(pushToggle.visible).toBe(true);
  expect(pushToggle.checked).toBe(false);
});

test('Push notifications toggle can be enabled', async ({ page }) => {
  await page.goto(editStoryUrl);

  const frame = page.frameLocator('#storybook-preview-iframe');
  const modal = frame.locator('prism-create-tenant-modal');
  await expect(modal).toBeVisible();

  await modal.evaluate((el) => {
    const tab = el.shadowRoot?.querySelector('uui-tab[label="Produce Mobile"]') as HTMLElement | null;
    tab?.click();
  });

  const isChecked = await modal.evaluate((el) => {
    const toggle = el.shadowRoot?.querySelector('input[aria-label="Push Notifications"]') as HTMLInputElement | null;
    if (!toggle) return false;

    toggle.checked = true;
    toggle.dispatchEvent(new Event('change', { bubbles: true, composed: true }));
    return toggle.checked;
  });

  expect(isChecked).toBe(true);

  const finalChecked = await modal.evaluate((el) => {
    const toggle = el.shadowRoot?.querySelector('input[aria-label="Push Notifications"]') as HTMLInputElement | null;
    return toggle?.checked ?? false;
  });

  expect(finalChecked).toBe(true);
});
