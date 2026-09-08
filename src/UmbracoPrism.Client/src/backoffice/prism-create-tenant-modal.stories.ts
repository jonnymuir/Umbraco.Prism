import type { Meta, StoryObj } from '@storybook/web-components-vite';
import { html } from 'lit';
import { expect, waitFor } from 'storybook/test';
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
        mobileOverrideValue?: string;
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
        secretKeyName: 'northwind-prism-secret',
        oidcAuthority: 'https://auth.example.com/realms/northwind',
        oidcClientId: 'northwind-portal',
        oidcClientSecretProvider: 'azure-key-vault',
        hasOidcClientSecret: true,
        mobileAppConfig: {
          AppName: 'Northwind Portal',
          AppId: 'com.northwind.portal',
          Version: '2.3.4',
          StartUrl: 'https://northwind.example/app',
          UserAgentMarker: 'PrismMobileNW',
          IconUrl: 'https://northwind.example/media/icon.png',
          SplashUrl: 'https://northwind.example/media/splash.png',
          ErrorBackgroundColor: '#111827',
          ErrorTextColor: '#f3f4f6',
          ErrorTitle: 'Cannot connect right now',
          ErrorMessage: 'Please try again in a moment.',
          ShowErrorDiagnostics: false
        },
        mobileBrandingOverrides: {
          '--color-primary': '#003399'
        }
      },
      brandingTabs: [
        {
          label: 'General Styles',
          variables: [
            { name: '--color-primary', defaultValue: '#3544b1', overrideValue: '#0055ff', mobileOverrideValue: '#003399' },
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
    await expect(modal.getAttribute('aria-label') ?? '').toContain('Edit Tenant');

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
    await expect(shadow.textContent ?? '').toContain('Current secret source: Azure Key Vault reference.');
    await expect(shadow.textContent ?? '').toContain('OIDC Key Vault Secret Name (replace only)');
    const identityPanel = shadow.querySelector('#identity-panel') as HTMLElement;
    await expect(identityPanel).not.toBeNull();
    await expect(identityPanel.getBoundingClientRect().height).toBeGreaterThan(0);
  }
};

export const LocalDemoSecretException: Story = {
  args: {
    data: {
      tenant: {
        id: 321,
        name: 'Local Dev (Keycloak)',
        hostname: 'localhost',
        oidcAuthority: 'https://localhost:8443/realms/prism-dev',
        oidcClientId: 'prism-client',
        oidcClientSecretProvider: 'inline',
        hasOidcClientSecret: true
      },
      brandingTabs: []
    }
  },
  play: async ({ canvasElement, args }) => {
    const modal = canvasElement.querySelector('prism-create-tenant-modal') as PrismCreateTenantModalElement;
    modal.data = args.data;
    await modal.updateComplete;

    if (!modal.shadowRoot) throw new Error('Shadow root not found');
    const shadow = modal.shadowRoot;

    const identityTab = shadow.querySelector('uui-tab[label="Identity"]') as HTMLElement;
    identityTab.dispatchEvent(new MouseEvent('click', { bubbles: true, composed: true }));
    await modal.updateComplete;

    await expect(shadow.textContent ?? '').toContain('repo-owned localhost Keycloak demo');
    await expect(shadow.textContent ?? '').toContain('Local Demo OIDC Client Secret');
  }
};

export const OverflowTabs: Story = {
  args: {
    data: {
      tenant: {
        id: 456,
        name: 'Prism Demo',
        hostname: 'demo.example'
      },
      brandingTabs: [
        {
          label: 'prism-colors.css',
          variables: [{ name: '--prism-primary', defaultValue: '#4f46e5' }]
        },
        {
          label: 'prism-typography.css',
          variables: [{ name: '--prism-font-display', defaultValue: 'Space Grotesk' }]
        },
        {
          label: 'prism-layout.css',
          variables: [{ name: '--prism-page-max', defaultValue: '1100px' }]
        },
        {
          label: 'prism-imagery.css',
          variables: [{ name: '--prism-hero-image', defaultValue: 'linear-gradient(...)' }]
        },
        {
          label: 'prism-components.css',
          variables: [{ name: '--prism-button-bg', defaultValue: '#4f46e5' }]
        },
        {
          label: 'prism-extra.css',
          variables: [{ name: '--prism-extra-token', defaultValue: '1rem' }]
        }
      ]
    }
  },
  play: async ({ canvasElement, args }) => {
    const modal = canvasElement.querySelector('prism-create-tenant-modal') as PrismCreateTenantModalElement;
    modal.data = args.data;
    modal.style.width = '420px';
    await modal.updateComplete;

    if (!modal.shadowRoot) throw new Error('Shadow root not found');
    const shadow = modal.shadowRoot;

    // The six branding tabs each render as a <uui-tab> in the modal's own markup (see
    // _renderBrandingTab / the uui-tab-group block). uui-tab-group may visually collapse the
    // overflow into its own dropdown, but that's its private concern — the <uui-tab> element
    // is always present in the light DOM, and clicking it still fires the group's
    // _handleTabGroupClick (which keys off data-tab-key). So drive selection through the tab
    // element directly rather than reaching into uui-tab-group's shadow internals, which
    // changed shape between UUI versions.
    const typographyTab = await waitFor(() => {
      const tab = shadow.querySelector('uui-tab[label="prism-typography.css"]') as HTMLElement | null;
      if (!tab) throw new Error('prism-typography.css tab not rendered');
      return tab;
    });

    typographyTab.dispatchEvent(new MouseEvent('click', { bubbles: true, composed: true }));
    await modal.updateComplete;

    await waitFor(() => expect(shadow.textContent ?? '').toContain('--prism-font-display'));
  }
};
