import type { Meta, StoryObj } from '@storybook/web-components';
import { html } from 'lit';
import { expect } from '@storybook/test';
import './prism-create-tenant-modal';
import { PrismCreateTenantModalElement } from './prism-create-tenant-modal';

type StoryArgs = {
  data?: {
    tenant?: any;
    brandingTabs?: Array<{
      label: string;
      variables: Array<{
        name: string;
        defaultValue?: string;
        overrideValue?: string;
      }>;
    }>;
  };
};

const meta: Meta<StoryArgs> = {
  title: 'Prism/Create Tenant Modal',
  component: 'prism-create-tenant-modal',
  tags: ['autodocs'],
  args: {
    data: undefined
  },
  render: (args) => html`
    <prism-create-tenant-modal .data=${args.data}></prism-create-tenant-modal>
  `
};

export default meta;

type Story = StoryObj<StoryArgs>;

export const Create: Story = {
  play: async ({ canvasElement }) => {
    const modal = canvasElement.querySelector('prism-create-tenant-modal') as PrismCreateTenantModalElement;
    await modal.updateComplete;

    if (!modal.shadowRoot) throw new Error('Shadow root not found');
    const shadow = modal.shadowRoot;
    await expect(shadow.textContent ?? '').toContain('Tenant Name');

    const container = shadow.querySelector('.container') as HTMLElement;
    const generalPanel = shadow.querySelector('#general-panel') as HTMLElement;
    await expect(container).not.toBeNull();
    await expect(generalPanel).not.toBeNull();
    await expect(container.getBoundingClientRect().height).toBeGreaterThanOrEqual(350);
    await expect(generalPanel.getBoundingClientRect().height).toBeGreaterThan(0);

    const identityTab = shadow.querySelector('uui-tab[label="Identity"]') as HTMLElement;
    identityTab.dispatchEvent(new MouseEvent('click', { bubbles: true, composed: true }));
    await modal.updateComplete;

    await expect(shadow.textContent ?? '').toContain('Directory (Tenant) ID');

    const identityPanel = shadow.querySelector('#identity-panel') as HTMLElement;
    await expect(identityPanel).not.toBeNull();
    await expect(identityPanel.getBoundingClientRect().height).toBeGreaterThan(0);

    const generalTab = shadow.querySelector('uui-tab[label="General"]') as HTMLElement;
    generalTab.dispatchEvent(new MouseEvent('click', { bubbles: true, composed: true }));
    await modal.updateComplete;
    await expect(shadow.textContent ?? '').toContain('Tenant Name');
  }
};

export const Edit: Story = {
  args: {
    data: {
      tenant: {
        id: 123,
        name: 'Northwind',
        hostname: 'northwind.example',
        entraTenantId: '00000000-0000-0000-0000-000000000000',
        entraClientId: '11111111-1111-1111-1111-111111111111',
        secretKeyName: 'northwind-prism-secret'
      },
      brandingTabs: [
        {
          label: 'General Styles',
          variables: [
            { name: '--color-primary', defaultValue: '#3544b1', overrideValue: '#0055ff' },
            { name: '--color-surface', defaultValue: '#ffffff' }
          ]
        },
        {
          label: 'Other Styles',
          variables: [
            { name: '--custom-border', overrideValue: '2px solid #0d6efd' }
          ]
        }
      ]
    }
  },
  play: async ({ canvasElement, args }) => {
    const modal = canvasElement.querySelector('prism-create-tenant-modal') as PrismCreateTenantModalElement;
    modal.data = args.data;
    await modal.updateComplete;

    if (!modal.shadowRoot) throw new Error('Shadow root not found');
    const shadow = modal.shadowRoot;
    await expect(shadow.textContent ?? '').toContain('Update Tenant');
    const dialogLayout = shadow.querySelector('uui-dialog-layout') as HTMLElement;
    await expect(dialogLayout?.getAttribute('headline') ?? '').toContain('Edit Tenant');

    const container = shadow.querySelector('.container') as HTMLElement;
    const generalPanel = shadow.querySelector('#general-panel') as HTMLElement;
    await expect(container).not.toBeNull();
    await expect(generalPanel).not.toBeNull();
    await expect(container.getBoundingClientRect().height).toBeGreaterThanOrEqual(350);
    await expect(generalPanel.getBoundingClientRect().height).toBeGreaterThan(0);

    const identityTab = shadow.querySelector('uui-tab[label="Identity"]') as HTMLElement;
    identityTab.dispatchEvent(new MouseEvent('click', { bubbles: true, composed: true }));
    await modal.updateComplete;

    await expect(shadow.textContent ?? '').toContain('Directory (Tenant) ID');
    const identityPanel = shadow.querySelector('#identity-panel') as HTMLElement;
    await expect(identityPanel).not.toBeNull();
    await expect(identityPanel.getBoundingClientRect().height).toBeGreaterThan(0);
  }
};
