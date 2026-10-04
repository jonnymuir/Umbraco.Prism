/** What the author configures for the tenant's mobile app, and the rules for defaults and validity. */
export interface MobileDraft {
  appName: string;
  appId: string;
  version: string;
  startUrl: string;
  userAgentMarker: string;
  iconUrl: string;
  splashUrl: string;
  iconPickerError: string;
  splashPickerError: string;
  errorBackgroundColor: string;
  errorTextColor: string;
  errorTitle: string;
  errorMessage: string;
  showErrorDiagnostics: boolean;
  pushNotificationsEnabled: boolean;
}

const DEFAULT_ERROR_BACKGROUND = '#0f172a';
const DEFAULT_ERROR_TEXT = '#f8fafc';
const DEFAULT_ERROR_TITLE = 'We’re having trouble connecting';
const DEFAULT_ERROR_MESSAGE = 'Please check your connection and try again.';

export const emptyMobile = (): MobileDraft => ({
  appName: '',
  appId: '',
  version: '1.0.0',
  startUrl: '',
  userAgentMarker: 'PrismMobile',
  iconUrl: '',
  splashUrl: '',
  iconPickerError: '',
  splashPickerError: '',
  errorBackgroundColor: DEFAULT_ERROR_BACKGROUND,
  errorTextColor: DEFAULT_ERROR_TEXT,
  errorTitle: DEFAULT_ERROR_TITLE,
  errorMessage: DEFAULT_ERROR_MESSAGE,
  showErrorDiagnostics: true,
  pushNotificationsEnabled: false
});

export function defaultMobileAppId(name: string) {
  const normalized = (name || 'tenant')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/(^-|-$)/g, '');

  return `com.prism.${normalized || 'tenant'}`;
}

export function defaultMobileStartUrl(hostname: string) {
  const host = (hostname || '').trim();
  if (!host) return '';

  if (host.startsWith('http://') || host.startsWith('https://')) return host;
  return `https://${host}`;
}

export function defaultMobileIconUrl(hostname: string) {
  const startUrl = defaultMobileStartUrl(hostname);
  if (!startUrl) return '';
  return `${startUrl.replace(/\/$/, '')}/favicon.ico`;
}

export function isValidMobileAppId(value: string) {
  if (!value) return false;
  return /^[a-zA-Z0-9]+(\.[a-zA-Z0-9_-]+)+$/.test(value.trim());
}

export function isValidAbsoluteUrl(value: string) {
  if (!value) return false;
  try {
    const parsed = new URL(value.trim());
    return parsed.protocol === 'http:' || parsed.protocol === 'https:';
  } catch {
    return false;
  }
}

export function isLikelyLocalhostUrl(value: string) {
  try {
    const parsed = new URL(value.trim());
    return ['localhost', '127.0.0.1', '::1'].includes(parsed.hostname);
  } catch {
    return false;
  }
}

/** A key's value from the saved config, whichever casing the server serialised it in. */
function configValue(config: Record<string, unknown>, camelKey: string) {
  if (camelKey in config) return config[camelKey];

  const pascalKey = camelKey.charAt(0).toUpperCase() + camelKey.slice(1);
  return pascalKey in config ? config[pascalKey] : undefined;
}

/** The tenant's saved mobile config (an object, or JSON text of one), or null if it has none or it is unreadable. */
function readSavedConfig(tenant: any): Record<string, unknown> | null {
  const raw = tenant?.mobileAppConfig;
  if (!raw) return null;

  if (typeof raw === 'string') {
    try {
      const parsed = JSON.parse(raw);
      return parsed && typeof parsed === 'object' ? parsed : null;
    } catch {
      return null;
    }
  }
  return typeof raw === 'object' ? raw : null;
}

/** The draft for a saved tenant: its saved mobile config, with the tenant's own name and host filling any gaps. */
export function mobileFromTenant(tenant: any): MobileDraft {
  const config = readSavedConfig(tenant) ?? {};
  const saved = <T>(key: string) => configValue(config, key) as T | undefined;
  const name: string = tenant.name ?? '';
  const hostname: string = tenant.hostname ?? '';

  return {
    appName: saved<string>('appName') ?? name,
    appId: saved<string>('appId') ?? defaultMobileAppId(tenant.name ?? 'tenant'),
    version: saved<string>('version') ?? '1.0.0',
    startUrl: saved<string>('startUrl') ?? defaultMobileStartUrl(hostname),
    userAgentMarker: saved<string>('userAgentMarker') ?? 'PrismMobile',
    iconUrl: saved<string>('iconUrl') ?? defaultMobileIconUrl(hostname),
    splashUrl: saved<string>('splashUrl') ?? '',
    iconPickerError: '',
    splashPickerError: '',
    errorBackgroundColor: saved<string>('errorBackgroundColor') ?? DEFAULT_ERROR_BACKGROUND,
    errorTextColor: saved<string>('errorTextColor') ?? DEFAULT_ERROR_TEXT,
    errorTitle: saved<string>('errorTitle') ?? DEFAULT_ERROR_TITLE,
    errorMessage: saved<string>('errorMessage') ?? DEFAULT_ERROR_MESSAGE,
    showErrorDiagnostics: saved<boolean>('showErrorDiagnostics') ?? true,
    pushNotificationsEnabled: saved<boolean>('pushNotificationsEnabled') ?? false
  };
}

/** "Use tenant defaults": the name, app id, start URL and icon follow the tenant; blank error-screen fields get their defaults back. */
export function withTenantDefaults(draft: MobileDraft, tenantName: string, hostname: string): MobileDraft {
  const appName = tenantName || draft.appName;
  return {
    ...draft,
    appName,
    appId: defaultMobileAppId(appName || tenantName || 'tenant'),
    startUrl: defaultMobileStartUrl(hostname),
    userAgentMarker: draft.userAgentMarker || 'PrismMobile',
    iconUrl: defaultMobileIconUrl(hostname),
    errorBackgroundColor: draft.errorBackgroundColor || DEFAULT_ERROR_BACKGROUND,
    errorTextColor: draft.errorTextColor || DEFAULT_ERROR_TEXT,
    errorTitle: draft.errorTitle || DEFAULT_ERROR_TITLE,
    errorMessage: draft.errorMessage || DEFAULT_ERROR_MESSAGE
  };
}

/** The mobile config saved with the tenant. */
export function mobileAppConfig(draft: MobileDraft) {
  return {
    appName: draft.appName,
    appId: draft.appId,
    version: draft.version,
    startUrl: draft.startUrl,
    userAgentMarker: draft.userAgentMarker,
    iconUrl: draft.iconUrl,
    splashUrl: draft.splashUrl,
    errorBackgroundColor: draft.errorBackgroundColor,
    errorTextColor: draft.errorTextColor,
    errorTitle: draft.errorTitle,
    errorMessage: draft.errorMessage,
    showErrorDiagnostics: draft.showErrorDiagnostics
  };
}

/** The request body for generating a bundle: the saved config plus the two feature switches. */
export function bundleRequest(draft: MobileDraft, biometricAuthEnabled: boolean) {
  return { ...mobileAppConfig(draft), biometricAuthEnabled, pushNotificationsEnabled: draft.pushNotificationsEnabled };
}
