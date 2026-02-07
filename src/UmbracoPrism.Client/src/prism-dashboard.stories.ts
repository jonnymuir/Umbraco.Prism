import type { Meta, StoryObj } from '@storybook/web-components';
import { html } from 'lit';
import { expect, within } from '@storybook/test';
import './index.ts';

type StoryArgs = Record<string, never>;

type PrismDashboardElement = HTMLElement & {
  _tenants?: Array<{
    id: number;
    name: string;
    hostname: string;
    themeColor: string;
    entraClientId?: string;
  }>;
  requestUpdate?: () => void;
  updateComplete?: Promise<unknown>;
};

const meta: Meta<StoryArgs> = {
  title: 'Prism/Dashboard',
  component: 'prism-dashboard',
  tags: ['autodocs'],
  render: () => html`<prism-dashboard></prism-dashboard>`
};

export default meta;

type Story = StoryObj<StoryArgs>;

export const EmptyState: Story = {
  play: async ({ canvasElement }) => {
    const dashboard = canvasElement.querySelector('prism-dashboard') as PrismDashboardElement;
    await dashboard.updateComplete;

    if (!dashboard.shadowRoot) throw new Error('Shadow root not found');
    const canvas = within(dashboard.shadowRoot as unknown as HTMLElement);
    await expect(
      canvas.getByText('No tenants found. Click "Add New Tenant" to get started.')
    ).toBeInTheDocument();
  }
};

export const WithTenants: Story = {
  play: async ({ canvasElement }) => {
    const dashboard = canvasElement.querySelector('prism-dashboard') as PrismDashboardElement;

    dashboard._tenants = [
      {
        id: 1,
        name: 'Northwind',
        hostname: 'northwind.example',
        themeColor: '#3544b1',
        entraClientId: '11111111-1111-1111-1111-111111111111'
      },
      {
        id: 2,
        name: 'Contoso',
        hostname: 'contoso.example',
        themeColor: '#ff7a59'
      }
    ];

    dashboard.requestUpdate?.();
    await dashboard.updateComplete;

    if (!dashboard.shadowRoot) throw new Error('Shadow root not found');
    const canvas = within(dashboard.shadowRoot as unknown as HTMLElement);
    await expect(canvas.getByText('Northwind')).toBeInTheDocument();
    await expect(canvas.getByText('Contoso')).toBeInTheDocument();
    await expect(canvas.getByText('Not Configured')).toBeInTheDocument();
  }
};
