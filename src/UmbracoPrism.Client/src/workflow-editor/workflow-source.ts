/**
 * WorkflowSource — the boundary contract between Prism's workflow editor
 * (a service-design tool) and the host business application.
 *
 * Hosts implement this interface to expose their authored workflows to the
 * editor. The editor never speaks HTTP, never reads identity, never knows
 * how the host stores its workflows. Save authorisation is the host's call:
 * resolve `save` to enforce permissions; surface UX hints via
 * `WorkflowAuthorContext` if you want the editor to grey out the Save button.
 *
 * Reference implementation: `InMemoryWorkflowSource` (this package).
 * Integrator examples: `MockBusinessApp/wwwroot/dist/workflow-editor-bootstrap.js`.
 */

import type { AuthoredWorkflow } from './types.js';

export interface WorkflowSummary {
  /** Host-facing lookup key. May differ from `definitionKey`. */
  workflowKey: string;
  /** Stable identity of the authored document, when the host tracks one. */
  id?: string;
  /** Definition key embedded in the workflow body. */
  definitionKey: string;
  /** Display name shown in workflow pickers. */
  displayName: string;
}

export interface WorkflowSource {
  /** Returns every workflow the editor should let the author pick. */
  list(): Promise<WorkflowSummary[]>;

  /** Loads one authored workflow by its host-facing key. */
  load(key: string): Promise<AuthoredWorkflow>;

  /** Persists the authored workflow back to the host. The host enforces save permissions. */
  save(key: string, workflow: AuthoredWorkflow): Promise<void>;
}
