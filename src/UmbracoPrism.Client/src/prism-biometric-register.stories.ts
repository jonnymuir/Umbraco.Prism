import type { Meta, StoryObj } from '@storybook/web-components';
import { expect } from '@storybook/test';
import './prism-biometric-register';
import { PrismBiometricRegisterElement } from './prism-biometric-register';
import type { BiometricBridge } from './biometric-bridge.js';

// Mock BiometricBridge for Storybook
const createMockBridge = (config: {
  isAvailable: boolean;
  registerBehavior?: 'success' | 'cancelled' | 'not_enrolled' | 'locked_out' | 'unavailable' | 'error';
}): BiometricBridge => ({
  async isAvailable() {
    return config.isAvailable;
  },
  async getOrCreateDeviceId() {
    return 'mock-device-id';
  },
  async checkEnrollmentChange(_tenantHost: string) {
    return false;
  },
  async register(_tenantHost: string, _loginHint?: string) {
    await new Promise(resolve => setTimeout(resolve, 1000));
    
    switch (config.registerBehavior) {
      case 'cancelled':
        throw { name: 'BiometricError', code: 'cancelled', message: 'Biometric registration was cancelled' };
      case 'not_enrolled':
        throw { name: 'BiometricError', code: 'not_enrolled', message: 'No biometric credentials are enrolled on this device' };
      case 'locked_out':
        throw { name: 'BiometricError', code: 'locked_out', message: 'Biometric authentication is temporarily locked' };
      case 'unavailable':
        throw { name: 'BiometricError', code: 'unavailable', message: 'Biometric authentication is not available' };
      case 'error':
        throw new Error('Network error');
      case 'success':
      default:
        return;
    }
  },
  async authenticate(_tenantHost: string) {
    return 'mock-session-token';
  },
  async revokeDevice(_tenantHost: string) {
    return;
  },
  async unenrolBiometric(_tenantHostname: string) {
    return;
  },
  async clearLocalCredentials(_tenantHost?: string) {
    return;
  }
});

type StoryArgs = {
  tenantHost: string;
  loginHint?: string;
  mockConfig: {
    isAvailable: boolean;
    registerBehavior?: 'success' | 'cancelled' | 'not_enrolled' | 'locked_out' | 'unavailable' | 'error';
  };
};

function makeElement(args: StoryArgs): PrismBiometricRegisterElement {
  const el = document.createElement('prism-biometric-register') as PrismBiometricRegisterElement;
  el.tenantHost = args.tenantHost;
  if (args.loginHint) el.loginHint = args.loginHint;
  el._mockBridge = createMockBridge(args.mockConfig);
  return el;
}

const meta: Meta<StoryArgs> = {
  title: 'Prism/Biometric Register',
  component: 'prism-biometric-register',
  tags: ['autodocs'],
  args: {
    tenantHost: 'example.prism.umbraco.io',
    loginHint: 'user@example.com',
    mockConfig: {
      isAvailable: true,
      registerBehavior: 'success'
    }
  },
  render: (args) => makeElement(args)
};

export default meta;

type Story = StoryObj<StoryArgs>;

export const Available: Story = {
  args: {
    mockConfig: {
      isAvailable: true,
      registerBehavior: 'success'
    }
  },
  play: async ({ canvasElement }) => {
    await new Promise(resolve => setTimeout(resolve, 200));
    const component = canvasElement.querySelector('prism-biometric-register') as PrismBiometricRegisterElement;
    await component.updateComplete;

    if (!component.shadowRoot) throw new Error('Shadow root not found');
    const shadow = component.shadowRoot;
    
    await expect(shadow.textContent ?? '').toContain('Enable Biometric Login');
    
    const button = shadow.querySelector('.register-button') as HTMLButtonElement;
    await expect(button).not.toBeNull();
    await expect(button.getAttribute('aria-label')).toContain('biometric');
  }
};

export const Loading: Story = {
  args: {
    mockConfig: {
      isAvailable: true,
      registerBehavior: 'success'
    }
  },
  play: async ({ canvasElement }) => {
    await new Promise(resolve => setTimeout(resolve, 200));
    const component = canvasElement.querySelector('prism-biometric-register') as PrismBiometricRegisterElement;
    await component.updateComplete;

    if (!component.shadowRoot) throw new Error('Shadow root not found');
    const shadow = component.shadowRoot;
    
    const button = shadow.querySelector('.register-button') as HTMLButtonElement;
    button.click();
    await component.updateComplete;
    
    await expect(shadow.textContent ?? '').toContain('Setting up biometric login');
    
    const statusMessage = shadow.querySelector('.status-message') as HTMLElement;
    await expect(statusMessage.getAttribute('aria-busy')).toBe('true');
    
    const spinner = shadow.querySelector('.spinner');
    await expect(spinner).not.toBeNull();
  }
};

export const Success: Story = {
  args: {
    mockConfig: {
      isAvailable: true,
      registerBehavior: 'success'
    }
  },
  play: async ({ canvasElement }) => {
    await new Promise(resolve => setTimeout(resolve, 200));
    const component = canvasElement.querySelector('prism-biometric-register') as PrismBiometricRegisterElement;
    await component.updateComplete;

    if (!component.shadowRoot) throw new Error('Shadow root not found');
    const shadow = component.shadowRoot;
    
    const button = shadow.querySelector('.register-button') as HTMLButtonElement;
    button.click();
    await new Promise(resolve => setTimeout(resolve, 1200));
    await component.updateComplete;
    
    await expect(shadow.textContent ?? '').toContain('Biometric login enabled');
    
    const statusMessage = shadow.querySelector('.status-message.success') as HTMLElement;
    await expect(statusMessage).not.toBeNull();
    await expect(statusMessage.getAttribute('role')).toBe('status');
  }
};

export const NotEnrolled: Story = {
  args: {
    mockConfig: {
      isAvailable: true,
      registerBehavior: 'not_enrolled'
    }
  },
  play: async ({ canvasElement }) => {
    await new Promise(resolve => setTimeout(resolve, 200));
    const component = canvasElement.querySelector('prism-biometric-register') as PrismBiometricRegisterElement;
    await component.updateComplete;

    if (!component.shadowRoot) throw new Error('Shadow root not found');
    const shadow = component.shadowRoot;
    
    const button = shadow.querySelector('.register-button') as HTMLButtonElement;
    button.click();
    await new Promise(resolve => setTimeout(resolve, 1200));
    await component.updateComplete;
    
    await expect(shadow.textContent ?? '').toContain('No biometrics enrolled on this device');
    await expect(shadow.textContent ?? '').toContain('Face ID or fingerprint in Settings');
    
    const errorMessage = shadow.querySelector('.status-message.error') as HTMLElement;
    await expect(errorMessage).not.toBeNull();
    await expect(errorMessage.getAttribute('role')).toBe('alert');
    
    const retryButton = shadow.querySelector('.retry-button') as HTMLButtonElement;
    await expect(retryButton).not.toBeNull();
  }
};

export const Cancelled: Story = {
  args: {
    mockConfig: {
      isAvailable: true,
      registerBehavior: 'cancelled'
    }
  },
  play: async ({ canvasElement }) => {
    await new Promise(resolve => setTimeout(resolve, 200));
    const component = canvasElement.querySelector('prism-biometric-register') as PrismBiometricRegisterElement;
    await component.updateComplete;

    if (!component.shadowRoot) throw new Error('Shadow root not found');
    const shadow = component.shadowRoot;
    
    const button = shadow.querySelector('.register-button') as HTMLButtonElement;
    button.click();
    await new Promise(resolve => setTimeout(resolve, 1200));
    await component.updateComplete;
    
    await expect(shadow.textContent ?? '').toContain('Registration cancelled');
    
    const retryButton = shadow.querySelector('.retry-button') as HTMLButtonElement;
    await expect(retryButton).not.toBeNull();
  }
};

export const LockedOut: Story = {
  args: {
    mockConfig: {
      isAvailable: true,
      registerBehavior: 'locked_out'
    }
  },
  play: async ({ canvasElement }) => {
    await new Promise(resolve => setTimeout(resolve, 200));
    const component = canvasElement.querySelector('prism-biometric-register') as PrismBiometricRegisterElement;
    await component.updateComplete;

    if (!component.shadowRoot) throw new Error('Shadow root not found');
    const shadow = component.shadowRoot;
    
    const button = shadow.querySelector('.register-button') as HTMLButtonElement;
    button.click();
    await new Promise(resolve => setTimeout(resolve, 1200));
    await component.updateComplete;
    
    await expect(shadow.textContent ?? '').toContain('Too many attempts');
  }
};

export const Unavailable: Story = {
  args: {
    mockConfig: {
      isAvailable: false
    }
  },
  play: async ({ canvasElement }) => {
    await new Promise(resolve => setTimeout(resolve, 200));
    const component = canvasElement.querySelector('prism-biometric-register') as PrismBiometricRegisterElement;
    await component.updateComplete;

    await expect(component.hidden).toBe(true);
    
    if (!component.shadowRoot) throw new Error('Shadow root not found');
    const shadow = component.shadowRoot;
    
    const container = shadow.querySelector('.container');
    await expect(container).toBeNull();
  }
};
