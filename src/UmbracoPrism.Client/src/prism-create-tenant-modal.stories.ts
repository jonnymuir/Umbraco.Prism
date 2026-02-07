import type { Meta, StoryObj } from '@storybook/web-components';
import { html } from 'lit';
import { expect, userEvent } from '@storybook/test';
import './prism-create-tenant-modal';
import { PrismCreateTenantModalElement } from './prism-create-tenant-modal';

type StoryArgs = {
  data?: { tenant?: any };
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

    const shadow = modal.shadowRoot as ShadowRoot;
    await expect(shadow).not.toBeNull();
    await expect(shadow.textContent ?? '').toContain('Tenant Name');

    const container = shadow.querySelector('.container') as HTMLElement;
    const generalPanel = shadow.querySelector('#general-panel') as HTMLElement;
    await expect(container).not.toBeNull();
    await expect(generalPanel).not.toBeNull();
    await expect(container.getBoundingClientRect().height).toBeGreaterThanOrEqual(350);
    await expect(generalPanel.getBoundingClientRect().height).toBeGreaterThan(0);

    const identityTab = shadow.querySelector('uui-tab[label="Identity"]') as HTMLElement;
    await userEvent.click(identityTab);
    await modal.updateComplete;

    await expect(shadow.textContent ?? '').toContain('Directory (Tenant) ID');

    const identityPanel = shadow.querySelector('#identity-panel') as HTMLElement;
    await expect(identityPanel).not.toBeNull();
    await expect(identityPanel.getBoundingClientRect().height).toBeGreaterThan(0);

    const generalTab = shadow.querySelector('uui-tab[label="General"]') as HTMLElement;
    await userEvent.click(generalTab);
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
      }
    }
  },
  play: async ({ canvasElement, args }) => {
    const modal = canvasElement.querySelector('prism-create-tenant-modal') as PrismCreateTenantModalElement;
    modal.data = args.data;
    await modal.updateComplete;

    const shadow = modal.shadowRoot as ShadowRoot;
    await expect(shadow).not.toBeNull();
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
    await userEvent.click(identityTab);
    await modal.updateComplete;

    await expect(shadow.textContent ?? '').toContain('Directory (Tenant) ID');
    const identityPanel = shadow.querySelector('#identity-panel') as HTMLElement;
    await expect(identityPanel).not.toBeNull();
    await expect(identityPanel.getBoundingClientRect().height).toBeGreaterThan(0);
  }
};
