/**
 * In-memory reference implementation of `WorkflowSource`.
 *
 * Useful for stories, tests, and any host that wants page-lifetime persistence
 * without hooking up a backend. Hold a clone on read and clone again on save
 * so callers cannot mutate stored state through their own references.
 */

import type { AuthoredWorkflow } from './types.js';
import type { WorkflowSource, WorkflowSummary } from './workflow-source.js';
import { withDerivedTransitions } from './workflow-routes.js';

type SeedEntry = AuthoredWorkflow | { workflowKey: string; workflow: AuthoredWorkflow };

function deepClone<T>(value: T): T {
  return JSON.parse(JSON.stringify(value)) as T;
}

export class InMemoryWorkflowSource implements WorkflowSource {
  private readonly workflows = new Map<string, AuthoredWorkflow>();

  constructor(seed: ReadonlyArray<SeedEntry> = []) {
    for (const entry of seed) {
      if ('workflow' in entry) {
        this.workflows.set(entry.workflowKey, deepClone(entry.workflow));
      } else {
        this.workflows.set(entry.definitionKey, deepClone(entry));
      }
    }
  }

  async list(): Promise<WorkflowSummary[]> {
    return Array.from(this.workflows.entries())
      .map(([workflowKey, workflow]) => ({
        workflowKey,
        definitionKey: workflow.definitionKey,
        displayName: workflow.displayName,
      }))
      .sort((a, b) => a.workflowKey.localeCompare(b.workflowKey));
  }

  async load(key: string): Promise<AuthoredWorkflow> {
    const workflow = this.workflows.get(key);
    if (!workflow) {
      throw new Error(`Workflow "${key}" not found.`);
    }
    return withDerivedTransitions(deepClone(workflow));
  }

  async save(key: string, workflow: AuthoredWorkflow): Promise<void> {
    // Strip the derived transitions view before storing — it's a read-only
    // projection of gateways[].routes maintained by withDerivedTransitions.
    const { transitions: _legacy, ...rest } = workflow as AuthoredWorkflow & { transitions?: unknown };
    this.workflows.set(key, deepClone(rest as AuthoredWorkflow));
  }

  /** Returns the underlying entries — handy for tests that want to assert state. */
  snapshot(): ReadonlyMap<string, AuthoredWorkflow> {
    return new Map(this.workflows);
  }
}
