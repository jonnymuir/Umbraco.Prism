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
