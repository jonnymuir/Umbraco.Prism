/**
 * In-memory reference implementation of `ServiceBlueprintSource`.
 *
 * Useful for stories, tests, and any host that wants page-lifetime persistence
 * without hooking up a backend. Hold a clone on read and clone again on save
 * so callers cannot mutate stored state through their own references.
 */

import type { AuthoredServiceBlueprint } from './types.js';
import type { ServiceBlueprintSource, ServiceBlueprintSummary } from './service-blueprint-source.js';

type SeedEntry = AuthoredServiceBlueprint | { blueprintKey: string; serviceBlueprint: AuthoredServiceBlueprint };

function deepClone<T>(value: T): T {
  return JSON.parse(JSON.stringify(value)) as T;
}

export class InMemoryServiceBlueprintSource implements ServiceBlueprintSource {
  private readonly serviceBlueprints = new Map<string, AuthoredServiceBlueprint>();

  constructor(seed: ReadonlyArray<SeedEntry> = []) {
    for (const entry of seed) {
      if ('serviceBlueprint' in entry) {
        this.serviceBlueprints.set(entry.blueprintKey, deepClone(entry.serviceBlueprint));
      } else {
        this.serviceBlueprints.set(entry.definitionKey, deepClone(entry));
      }
    }
  }

  async list(): Promise<ServiceBlueprintSummary[]> {
    return Array.from(this.serviceBlueprints.entries())
      .map(([blueprintKey, serviceBlueprint]) => ({
        blueprintKey,
        definitionKey: serviceBlueprint.definitionKey,
        displayName: serviceBlueprint.displayName,
      }))
      .sort((a, b) => a.blueprintKey.localeCompare(b.blueprintKey));
  }

  async load(key: string): Promise<AuthoredServiceBlueprint> {
    const serviceBlueprint = this.serviceBlueprints.get(key);
    if (!serviceBlueprint) {
      throw new Error(`ServiceBlueprint "${key}" not found.`);
    }
    return deepClone(serviceBlueprint);
  }

  async save(key: string, serviceBlueprint: AuthoredServiceBlueprint): Promise<void> {
    this.serviceBlueprints.set(key, deepClone(serviceBlueprint));
  }

  /** Returns the underlying entries — handy for tests that want to assert state. */
  snapshot(): ReadonlyMap<string, AuthoredServiceBlueprint> {
    return new Map(this.serviceBlueprints);
  }
}
