import type { Meta, StoryObj } from '@storybook/web-components';
import { expect } from '@storybook/test';
import './prism-biometric-settings';
import { PrismBiometricSettingsElement } from './prism-biometric-settings';

type StoryArgs = {
  tenantHost: string;
  registered: boolean;
};

/**
 * Builds an element with mocked bridge and forced internal state for Storybook.
 */
function makeElement(
  args: StoryArgs,
  overrides: {
    isAvailable?: boolean;
    uiState?: 'idle' | 'confirm-revoke' | 'revoking' | 'revoke-error';
    errorMessage?: string;
  } = {}
): PrismBiometricSettingsElement {
  const el = document.createElement('prism-biometric-settings') as PrismBiometricSettingsElement;
  el.tenantHost = args.tenantHost;
  el.registered = args.registered;

  const isAvail = overrides.isAvailable ?? true;

  el._mockBridge = {
    isAvailable: async () => isAvail,
    getOrCreateDeviceId: async () => 'mock-device-id',
    register: async () => {},
    authenticate: async () => 'mock-session-token',
    revokeDevice: async () => {},
    clearLocalCredentials: async () => {}
  };

  if (overrides.uiState || overrides.errorMessage !== undefined) {
    setTimeout(() => {
      if (overrides.uiState) (el as any)._uiState = overrides.uiState;
      if (overrides.isAvailable !== undefined) (el as any)._isAvailable = isAvail;
      if (overrides.errorMessage !== undefined) (el as any)._errorMessage = overrides.errorMessage;
      el.requestUpdate();
    }, 50);
  }

  return el;
}

const meta: Meta<StoryArgs> = {
  title: 'Prism/Biometric Settings',
  component: 'prism-biometric-settings',
  tags: ['autodocs'],
  args: {
    tenantHost: 'tenant1.prism.local',
    registered: true
  }
};

export default meta;

type Story = StoryObj<StoryArgs>;

export const Registered: Story = {
  args: { tenantHost: 'tenant1.prism.local', registered: true },
  render: (args) => makeElement(args),
  play: async ({ canvasElement }) => {
    await new Promise(r => setTimeout(r, 200));
    const el = canvasElement.querySelector('prism-biometric-settings') as PrismBiometricSettingsElement;
    await el.updateComplete;

    const root = el.shadowRoot!;
    const status = root.querySelector('.status-text') as HTMLElement;
    await expect(status).not.toBeNull();
    await expect(status.textContent).toContain('Biometric login is enabled');

    const disableBtn = root.querySelector('.btn-danger') as HTMLButtonElement;
    await expect(disableBtn).not.toBeNull();
    await expect(disableBtn.textContent).toContain('Disable');
  }
};

export const NotRegistered: Story = {
  args: { tenantHost: 'tenant1.prism.local', registered: false },
  render: (args) => makeElement(args),
  play: async ({ canvasElement }) => {
    await new Promise(r => setTimeout(r, 200));
    const el = canvasElement.querySelector('prism-biometric-settings') as PrismBiometricSettingsElement;
    await el.updateComplete;

    const root = el.shadowRoot!;
    const status = root.querySelector('.status-text') as HTMLElement;
    await expect(status.textContent).toContain('not set up');

    const setupBtn = root.querySelector('.btn-primary') as HTMLButtonElement;
    await expect(setupBtn).not.toBeNull();
    await expect(setupBtn.textContent).toContain('Set up');
  }
};

export const Unavailable: Story = {
  args: { tenantHost: 'tenant1.prism.local', registered: false },
  render: (args) => makeElement(args, { isAvailable: false }),
  play: async ({ canvasElement }) => {
    await new Promise(r => setTimeout(r, 200));
    const el = canvasElement.querySelector('prism-biometric-settings') as PrismBiometricSettingsElement;
    await el.updateComplete;

    const root = el.shadowRoot!;
    const status = root.querySelector('.status-text') as HTMLElement;
    await expect(status.textContent).toContain('not supported');

    // No interactive buttons when unavailable
    const buttons = root.querySelectorAll('button');
    await expect(buttons.length).toBe(0);
  }
};

export const ConfirmingRevoke: Story = {
  args: { tenantHost: 'tenant1.prism.local', registered: true },
  render: (args) => makeElement(args, { uiState: 'confirm-revoke' }),
  play: async ({ canvasElement }) => {
    await new Promise(r => setTimeout(r, 200));
    const el = canvasElement.querySelector('prism-biometric-settings') as PrismBiometricSettingsElement;
    await el.updateComplete;

    const root = el.shadowRoot!;
    const dialog = root.querySelector('[role="dialog"]') as HTMLElement;
    await expect(dialog).not.toBeNull();
    await expect(dialog.getAttribute('aria-modal')).toBe('true');

    const cancelBtn = Array.from(root.querySelectorAll('button')).find(b =>
      b.textContent?.includes('Cancel')
    ) as HTMLButtonElement;
    await expect(cancelBtn).not.toBeNull();

    const confirmBtn = Array.from(root.querySelectorAll('button')).find(b =>
      b.textContent?.includes('Disable')
    ) as HTMLButtonElement;
    await expect(confirmBtn).not.toBeNull();
  }
};

export const Revoking: Story = {
  args: { tenantHost: 'tenant1.prism.local', registered: true },
  render: (args) => makeElement(args, { uiState: 'revoking' }),
  play: async ({ canvasElement }) => {
    await new Promise(r => setTimeout(r, 200));
    const el = canvasElement.querySelector('prism-biometric-settings') as PrismBiometricSettingsElement;
    await el.updateComplete;

    const root = el.shadowRoot!;
    const spinner = root.querySelector('.loading-spinner');
    await expect(spinner).not.toBeNull();

    const status = root.querySelector('.status-text') as HTMLElement;
    await expect(status.getAttribute('aria-busy')).toBe('true');
    await expect(status.textContent).toContain('Disabling');
  }
};
