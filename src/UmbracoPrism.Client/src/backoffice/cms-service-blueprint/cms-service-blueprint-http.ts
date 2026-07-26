// Shared bearer-token fetch helper for the CMS Service Blueprint entity's data sources — same
// authentication shape as UmbracoBackofficeServiceBlueprintSource (the editor's own ServiceBlueprintSource):
// the Management API is Bearer-token authenticated and the token can rotate between calls, so
// it's fetched fresh via UMB_AUTH_CONTEXT on every request rather than captured once.

import type { UmbControllerHost } from '@umbraco-cms/backoffice/controller-api';
import type { UmbClassInterface } from '@umbraco-cms/backoffice/class-api';
import { UMB_AUTH_CONTEXT } from '@umbraco-cms/backoffice/auth';

export const CMS_SERVICE_BLUEPRINT_API_BASE = '/umbraco/management/api/v1/prism/cms-service-blueprints';

async function authHeaders(host: UmbControllerHost, extra: Record<string, string> = {}): Promise<Record<string, string>> {
  // Data source constructors are typed to the minimal UmbControllerHost per Umbraco's own
  // extension interfaces, but the object actually passed by the extension loader always
  // implements the fuller UmbClassInterface (getContext/consumeContext/observe) — same
  // assumption core's own server data sources rely on for context/API access.
  const authContext = await (host as UmbClassInterface).getContext(UMB_AUTH_CONTEXT);
  const token = await authContext?.getLatestToken();
  return {
    ...extra,
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
  };
}

export async function cmsServiceBlueprintFetch(host: UmbControllerHost, path: string, init: RequestInit = {}): Promise<Response> {
  return fetch(`${CMS_SERVICE_BLUEPRINT_API_BASE}${path}`, {
    ...init,
    headers: {
      Accept: 'application/json',
      ...(await authHeaders(host, (init.headers as Record<string, string>) ?? {})),
    },
    credentials: 'same-origin',
  });
}
