import { UMB_AUTH_CONTEXT } from '@umbraco-cms/backoffice/auth';
import type { UmbElement } from '@umbraco-cms/backoffice/element-api';

export type ContextHost = Pick<UmbElement, 'consumeContext' | 'getContext'>;

export const authHeaders = (token: string | undefined): Record<string, string> => (token ? { Authorization: `Bearer ${token}` } : {});

/**
 * The latest bearer token, waiting at most half a second for the auth context to appear — a
 * modal can open before the context is provided, and the request should still go out (and fail
 * with a 401 the caller reports) rather than hang.
 */
export async function bearerTokenWithin500ms(host: ContextHost): Promise<string | undefined> {
  let token: string | undefined;
  await Promise.race([
    new Promise<void>(resolve => {
      host.consumeContext(UMB_AUTH_CONTEXT, async authContext => {
        if (authContext) token = await authContext.getLatestToken();
        resolve();
      });
    }),
    new Promise<void>(resolve => setTimeout(resolve, 500))
  ]);
  return token;
}

/** The latest bearer token from the auth context, or undefined when there isn't one. */
export async function bearerToken(host: ContextHost): Promise<string | undefined> {
  const authContext = await host.getContext(UMB_AUTH_CONTEXT);
  return authContext ? await authContext.getLatestToken() : undefined;
}
