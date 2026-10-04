import { expect, test } from '@playwright/test';
import {
  emptyIdentity,
  identityPayload,
  secretStatusMessage,
  secretValidationMessage,
  type IdentityDraft
} from '../src/backoffice/tenant-modal/identity-rules.js';
import {
  defaultMobileAppId,
  defaultMobileIconUrl,
  isLikelyLocalhostUrl,
  isValidMobileAppId,
  mobileFromTenant,
  withTenantDefaults
} from '../src/backoffice/tenant-modal/mobile-rules.js';
import {
  linkableTargets,
  nonEmptyOverrides,
  parseLink,
  resolveLiteralValue,
  staticTableOverrides,
  toOverrideMap,
  type BrandingMetadata
} from '../src/backoffice/tenant-modal/branding-rules.js';

// The rules behind the tenant modal's tabs, tested without a browser: what makes a generic-OIDC
// tenant savable, how mobile defaults follow the tenant, and how branding overrides are collected.

const genericOidc = (change: Partial<IdentityDraft> = {}): IdentityDraft => ({
  ...emptyIdentity(),
  oidcAuthority: 'https://auth.example.com/realms/prod',
  oidcClientId: 'portal',
  ...change
});

test.describe('identity rules', () => {
  test('a tenant with no OIDC settings needs no secret', () => {
    expect(secretValidationMessage(emptyIdentity(), 'northwind.example', null)).toBe('');
  });

  test('a new generic-OIDC tenant must name a Key Vault secret', () => {
    expect(secretValidationMessage(genericOidc(), 'northwind.example', null)).toContain('Azure Key Vault secret name');
    expect(secretValidationMessage(genericOidc({ secretKeyName: 'northwind-oidc-secret' }), 'northwind.example', null)).toBe('');
  });

  test('an existing tenant keeps its stored secret unless it is being replaced or cleared', () => {
    expect(secretValidationMessage(genericOidc({ hasStoredOidcClientSecret: true }), 'northwind.example', 5)).toBe('');
    expect(secretValidationMessage(genericOidc({ hasStoredOidcClientSecret: false }), 'northwind.example', 5)).not.toBe('');
  });

  test('the localhost Keycloak demo is the only tenant that takes an inline secret', () => {
    const demo = genericOidc({ oidcAuthority: 'https://localhost:8443/realms/prism', oidcClientId: 'prism-client' });
    expect(secretValidationMessage(demo, 'localhost', null)).toContain('localhost demo secret');
    expect(identityPayload({ ...demo, oidcClientSecretReference: 'prism-dev-secret' }, 'localhost')).toMatchObject({
      oidcClientSecretProvider: 'inline',
      oidcClientSecretReference: 'prism-dev-secret'
    });
    // the same client id on a real host gets no inline provider
    expect(identityPayload({ ...demo, oidcClientSecretReference: 'x' }, 'northwind.example').oidcClientSecretProvider).toBeUndefined();
  });

  test('clearing a stored OIDC secret is an explicit reset and drops the secret name', () => {
    const payload = identityPayload(genericOidc({ secretKeyName: 'old', clearOidcClientSecret: true, hasStoredOidcClientSecret: true }), 'northwind.example');
    expect(payload.resetOidcClientSecret).toBe(true);
    expect(payload.secretKeyName).toBeUndefined();
  });

  test('the status line says where the current secret comes from', () => {
    expect(secretStatusMessage(genericOidc(), 'northwind.example')).toContain('No generic OIDC client secret');
    expect(secretStatusMessage(genericOidc({ hasStoredOidcClientSecret: true }), 'northwind.example')).toContain('Azure Key Vault');
    expect(secretStatusMessage(genericOidc({ hasStoredOidcClientSecret: true, oidcClientSecretProvider: 'inline' }), 'northwind.example')).toContain('inline');
  });
});

test.describe('mobile rules', () => {
  test('app ids are suggested from the tenant name', () => {
    expect(defaultMobileAppId("O'Brien & Sons!")).toBe('com.prism.o-brien-sons');
    expect(defaultMobileAppId('')).toBe('com.prism.tenant');
  });

  test('app ids must be reverse-domain style', () => {
    expect(isValidMobileAppId('com.acme.portal')).toBe(true);
    expect(isValidMobileAppId('portal')).toBe(false);
  });

  test('the icon defaults to the site favicon, and localhost is recognised', () => {
    expect(defaultMobileIconUrl('portal.example')).toBe('https://portal.example/favicon.ico');
    expect(isLikelyLocalhostUrl('https://localhost:44399')).toBe(true);
    expect(isLikelyLocalhostUrl('https://portal.example')).toBe(false);
  });

  test('a saved tenant fills the mobile settings from its saved config, in either casing, and its own name and host elsewhere', () => {
    const draft = mobileFromTenant({
      name: 'Northwind',
      hostname: 'northwind.example',
      mobileAppConfig: JSON.stringify({ AppId: 'com.northwind.app', errorTitle: 'Offline' })
    });
    expect(draft.appId).toBe('com.northwind.app');
    expect(draft.errorTitle).toBe('Offline');
    expect(draft.appName).toBe('Northwind');
    expect(draft.startUrl).toBe('https://northwind.example');
    expect(draft.showErrorDiagnostics).toBe(true);
  });

  test('an unreadable saved config falls back to the tenant defaults', () => {
    expect(mobileFromTenant({ name: 'Northwind', hostname: 'h.example', mobileAppConfig: '{not json' }).appId).toBe('com.prism.northwind');
  });

  test('"use tenant defaults" follows the tenant and restores blank error-screen fields', () => {
    const draft = withTenantDefaults({ ...mobileFromTenant({ name: 'Old', hostname: 'old.example' }), errorTitle: '' }, 'Northwind', 'northwind.example');
    expect(draft.appName).toBe('Northwind');
    expect(draft.appId).toBe('com.prism.northwind');
    expect(draft.startUrl).toBe('https://northwind.example');
    expect(draft.errorTitle).toBe('We’re having trouble connecting');
  });
});

test.describe('branding rules', () => {
  const metadata: BrandingMetadata = {
    sections: [
      {
        name: 'Colours',
        variables: [
          { variable: '--prism-primary', label: 'Primary', description: '', type: 'color', syntax: '<color>', currentValue: '#0d6efd' },
          { variable: '--prism-danger', label: 'Danger', description: '', type: 'color', syntax: '<color>', currentValue: 'var(--prism-primary, #0d6efd)' },
          { variable: '--prism-accent', label: 'Accent', description: '', type: 'color', syntax: '<color>', currentValue: '#ff0000' },
          { variable: '--prism-radius', label: 'Radius', description: '', type: 'length', syntax: '<length>', currentValue: '4px' }
        ]
      }
    ]
  };

  test('a var() reference is recognised, with its fallback', () => {
    expect(parseLink('var(--prism-danger, #d4351c)')).toEqual({ target: '--prism-danger', fallback: '#d4351c' });
    expect(parseLink('#d4351c')).toBeNull();
  });

  test('a chain of references resolves to a literal, and a cycle does not hang', () => {
    expect(resolveLiteralValue(metadata, {}, 'var(--prism-danger)')).toBe('#0d6efd');
    const cyclic = { '--prism-primary': 'var(--prism-danger)', '--prism-danger': 'var(--prism-primary)' };
    expect(() => resolveLiteralValue(metadata, cyclic, 'var(--prism-primary)')).not.toThrow();
  });

  test('a variable can link to other literal variables of its own type, not to itself or to links', () => {
    const accent = metadata.sections[0].variables[2];
    const targets = linkableTargets(metadata, {}, accent).map(target => target.value);
    expect(targets).toEqual(['--prism-primary']);
  });

  test('overrides keep only non-empty trimmed values, and mobile ones only where mobile is not inheriting', () => {
    expect(nonEmptyOverrides({ a: ' #fff ', b: '  ', c: '' })).toEqual({ a: '#fff' });
    expect(nonEmptyOverrides({ a: '1', b: '2' }, name => name === 'b')).toEqual({ b: '2' });
  });

  test('the static table collects what was typed in it', () => {
    const tabs = [{ label: 'Site', variables: [{ name: '--x', overrideValue: ' red ', mobileOverrideValue: '' }, { name: '--y' }] }];
    expect(staticTableOverrides(tabs, 'overrideValue')).toEqual({ '--x': 'red' });
    expect(staticTableOverrides(tabs, 'mobileOverrideValue')).toEqual({});
  });

  test('saved overrides may arrive as an object or as JSON text', () => {
    expect(toOverrideMap('{"--x":"red","--n":1}')).toEqual({ '--x': 'red' });
    expect(toOverrideMap({ '--x': 'red' })).toEqual({ '--x': 'red' });
    expect(toOverrideMap('{not json')).toEqual({});
  });
});
