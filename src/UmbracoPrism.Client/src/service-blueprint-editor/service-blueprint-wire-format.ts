/**
 * The editor now works directly against the persisted ServiceBlueprintDefinition
 * contract, so load/save is a straight JSON pass-through.
 */

import type { AuthoredServiceBlueprint } from './types.js';

export function serialiseServiceBlueprint(serviceBlueprint: AuthoredServiceBlueprint): Record<string, unknown> {
  return serviceBlueprint as unknown as Record<string, unknown>;
}

export function normaliseServiceBlueprint(raw: Record<string, unknown>): AuthoredServiceBlueprint {
  return raw as unknown as AuthoredServiceBlueprint;
}
