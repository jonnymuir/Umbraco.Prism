/** The identity half of a tenant: Microsoft Entra, or a generic OIDC provider with its client secret. */
export interface IdentityDraft {
  entraTenantId: string;
  entraClientId: string;
  secretKeyName: string;
  oidcAuthority: string;
  oidcClientId: string;
  /** Write-only: never receives the stored secret, only a replacement the author types. */
  oidcClientSecretReference: string;
  oidcClientSecretProvider: string;
  hasStoredOidcClientSecret: boolean;
  clearOidcClientSecret: boolean;
}

export const INLINE_SECRET_PROVIDER = 'inline';

export const emptyIdentity = (): IdentityDraft => ({
  entraTenantId: '',
  entraClientId: '',
  secretKeyName: '',
  oidcAuthority: '',
  oidcClientId: '',
  oidcClientSecretReference: '',
  oidcClientSecretProvider: '',
  hasStoredOidcClientSecret: false,
  clearOidcClientSecret: false
});

export function normalizeOptionalString(value: string) {
  const normalized = value.trim();
  return normalized.length > 0 ? normalized : undefined;
}

export function identityFromTenant(tenant: any): IdentityDraft {
  return {
    entraTenantId: tenant.entraTenantId ?? '',
    entraClientId: tenant.entraClientId ?? '',
    secretKeyName: tenant.oidcAuthority ? '' : (tenant.secretKeyName ?? ''),
    oidcAuthority: tenant.oidcAuthority ?? '',
    oidcClientId: tenant.oidcClientId ?? '',
    oidcClientSecretReference: '',
    oidcClientSecretProvider: tenant.oidcClientSecretProvider ?? '',
    hasStoredOidcClientSecret: tenant.hasOidcClientSecret ?? false,
    clearOidcClientSecret: false
  };
}

export const usesGenericOidc = (draft: IdentityDraft) =>
  Boolean(normalizeOptionalString(draft.oidcAuthority) || normalizeOptionalString(draft.oidcClientId));

/** The repo's own localhost Keycloak demo is the only tenant allowed an inline (non-Key-Vault) secret. */
export function isRepoOwnedLocalDemoTenant(draft: IdentityDraft, hostname: string) {
  if (hostname.trim().toLowerCase() !== 'localhost') {
    return false;
  }

  if (draft.oidcClientId.trim() !== 'prism-client') {
    return false;
  }

  try {
    return new URL(draft.oidcAuthority).hostname.toLowerCase() === 'localhost';
  } catch {
    return false;
  }
}

export function secretStatusMessage(draft: IdentityDraft, hostname: string) {
  if (!usesGenericOidc(draft)) {
    return '';
  }

  if (!draft.hasStoredOidcClientSecret) {
    return 'No generic OIDC client secret is configured yet.';
  }

  if (draft.oidcClientSecretProvider === INLINE_SECRET_PROVIDER || isRepoOwnedLocalDemoTenant(draft, hostname)) {
    return 'Current secret source: repo-owned localhost demo inline secret.';
  }

  return 'Current secret source: Azure Key Vault reference.';
}

/** What is still missing before a generic OIDC tenant can be saved, or '' when nothing is. */
export function secretValidationMessage(draft: IdentityDraft, hostname: string, tenantId: number | null) {
  const exempt = !usesGenericOidc(draft) || (draft.clearOidcClientSecret && tenantId !== null);
  const keepsStoredSecret = tenantId !== null && draft.hasStoredOidcClientSecret;
  if (exempt || keepsStoredSecret) {
    return '';
  }

  // The localhost demo is given its secret inline; everyone else names a Key Vault secret.
  if (isRepoOwnedLocalDemoTenant(draft, hostname)) {
    return normalizeOptionalString(draft.oidcClientSecretReference)
      ? ''
      : 'Enter the repo-owned localhost demo secret before saving this generic OIDC tenant.';
  }

  return normalizeOptionalString(draft.secretKeyName)
    ? ''
    : 'Enter an Azure Key Vault secret name for this generic OIDC client, or leave the OIDC section blank to use Entra.';
}

/** The identity fields of the save payload; a generic-OIDC reset or inline-secret replacement is explicit. */
export function identityPayload(draft: IdentityDraft, hostname: string) {
  const generic = usesGenericOidc(draft);
  const inlineSecretReplacement =
    generic && isRepoOwnedLocalDemoTenant(draft, hostname) && !draft.clearOidcClientSecret
      ? normalizeOptionalString(draft.oidcClientSecretReference)
      : undefined;

  return {
    entraTenantId: normalizeOptionalString(draft.entraTenantId),
    entraClientId: normalizeOptionalString(draft.entraClientId),
    secretKeyName: generic && draft.clearOidcClientSecret ? undefined : normalizeOptionalString(draft.secretKeyName),
    oidcAuthority: normalizeOptionalString(draft.oidcAuthority),
    oidcClientId: normalizeOptionalString(draft.oidcClientId),
    oidcClientSecretProvider: inlineSecretReplacement ? INLINE_SECRET_PROVIDER : undefined,
    oidcClientSecretReference: inlineSecretReplacement,
    resetOidcClientSecret: generic && draft.clearOidcClientSecret
  };
}
