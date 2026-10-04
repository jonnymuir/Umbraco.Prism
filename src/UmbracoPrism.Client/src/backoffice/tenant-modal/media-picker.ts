import { UMB_MODAL_MANAGER_CONTEXT } from '@umbraco-cms/backoffice/modal';
import { UMB_MEDIA_PICKER_MODAL } from '@umbraco-cms/backoffice/media';
import type { UmbElement } from '@umbraco-cms/backoffice/element-api';
import { authHeaders, bearerToken } from './auth.js';

/**
 * Lets the author pick one item from the Media Library and returns its URL (as the management API
 * reports it, usually site-relative), or null if they cancelled or it has none. `label` only
 * prefixes the diagnostics, so a failure says which picker it came from.
 */
export async function pickMediaUrl(host: UmbElement, label = ''): Promise<string | null> {
  const modalManager = await host.getContext(UMB_MODAL_MANAGER_CONTEXT);
  if (!modalManager) return null;

  const modal = modalManager.open(host, UMB_MEDIA_PICKER_MODAL, { data: { multiple: false } });
  const result = await modal.onSubmit().catch(() => null);
  const unique = result?.selection?.[0];
  if (!unique) return null;

  try {
    const token = await bearerToken(host);
    const res = await fetch(`/umbraco/management/api/v1/media/urls?id=${unique}`, { headers: authHeaders(token) });
    if (!res.ok) return null;

    const data = await res.json();
    const items: Array<{ id: string; urlInfos: Array<{ culture: string | null; url: string | null }> }> = Array.isArray(data) ? data : [data];
    const rawUrl: string = items[0]?.urlInfos?.[0]?.url ?? '';
    if (!rawUrl) {
      console.warn(`[Prism] ${label}media URL response had no URL`, data);
      return null;
    }
    return rawUrl;
  } catch (err) {
    console.error(`[Prism] Failed to fetch ${label.toLowerCase()}media URL`, err);
    return null;
  }
}
