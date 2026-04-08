// ⚠️ MOBILE BOUNDARY: No @umbraco-cms imports allowed in this directory.

import type {
  WorkflowApiClient,
  WorkflowResponseEnvelope,
  WorkflowProblem,
} from './workflow-api-client';

export type OrchestratorState =
  | 'idle'
  | 'creating'
  | 'asking'
  | 'submitting'
  | 'waiting'
  | 'polling'
  | 'complete'
  | 'error';

export interface StateChangeEvent {
  state: OrchestratorState;
  envelope: WorkflowResponseEnvelope | null;
}

export class WorkflowOrchestrator extends EventTarget {
  private _state: OrchestratorState = 'idle';
  private _currentEnvelope: WorkflowResponseEnvelope | null = null;
  private _validationProblems: WorkflowProblem[] = [];
  private _pollTimeoutId: number | null = null;

  constructor(private readonly apiClient: WorkflowApiClient) {
    super();
  }

  get state(): OrchestratorState {
    return this._state;
  }

  get currentEnvelope(): WorkflowResponseEnvelope | null {
    return this._currentEnvelope;
  }

  get validationProblems(): WorkflowProblem[] {
    return this._validationProblems;
  }

  async start(definitionKey: string, correlationId?: string): Promise<void> {
    this._setState('creating');
    this._validationProblems = [];

    try {
      const envelope = await this.apiClient.createInstance({
        definitionKey,
        correlationId,
      });

      this._currentEnvelope = envelope;
      this._processEnvelope(envelope);
    } catch (error) {
      this._handleError(error);
    }
  }

  async advance(actionKey: string, fieldValues?: Record<string, unknown>): Promise<void> {
    if (!this._currentEnvelope) {
      throw new Error('No active workflow instance');
    }

    this._setState('submitting');
    this._validationProblems = [];

    try {
      const envelope = await this.apiClient.advance(this._currentEnvelope.instanceId, {
        actionKey,
        fieldValues,
        stateVersion: this._currentEnvelope.stateVersion,
      });

      this._currentEnvelope = envelope;
      this._processEnvelope(envelope);
    } catch (error) {
      // Handle 409 Conflict (stateVersion mismatch) by refreshing
      if (error && typeof error === 'object' && 'statusCode' in error) {
        const apiError = error as { statusCode: number; envelope?: WorkflowResponseEnvelope };
        if (apiError.statusCode === 409 && apiError.envelope) {
          this._currentEnvelope = apiError.envelope;
          this._processEnvelope(apiError.envelope);
          return;
        }
      }

      this._handleError(error);
    }
  }

  async cancel(): Promise<void> {
    this._stopPolling();

    if (this._currentEnvelope) {
      try {
        await this.apiClient.deleteInstance(this._currentEnvelope.instanceId);
      } catch {
        // Ignore errors on cancel
      }
    }

    this._setState('idle');
    this._currentEnvelope = null;
    this._validationProblems = [];
  }

  private _processEnvelope(envelope: WorkflowResponseEnvelope): void {
    this._stopPolling();

    switch (envelope.responseState) {
      case 'ask_now':
        this._setState('asking');
        break;

      case 'wait':
        this._setState('waiting');
        this._startPolling(envelope.pollAfterMs ?? 5000);
        break;

      case 'complete':
        this._setState('complete');
        break;

      case 'error':
        this._validationProblems = envelope.problems || [];
        this._setState('error');
        break;
    }
  }

  private _startPolling(delayMs: number): void {
    this._stopPolling();

    this._pollTimeoutId = window.setTimeout(() => {
      this._poll();
    }, delayMs);
  }

  private _stopPolling(): void {
    if (this._pollTimeoutId !== null) {
      window.clearTimeout(this._pollTimeoutId);
      this._pollTimeoutId = null;
    }
  }

  private async _poll(): Promise<void> {
    if (!this._currentEnvelope) return;

    this._setState('polling');

    try {
      const envelope = await this.apiClient.getInstance(this._currentEnvelope.instanceId);
      this._currentEnvelope = envelope;

      // If still waiting, keep polling
      if (envelope.responseState === 'wait') {
        this._setState('waiting');
        this._startPolling(envelope.pollAfterMs ?? 5000);
      } else {
        this._processEnvelope(envelope);
      }
    } catch (error) {
      this._handleError(error);
    }
  }

  private _handleError(error: unknown): void {
    console.error('Workflow orchestrator error:', error);

    if (error && typeof error === 'object' && 'envelope' in error) {
      const apiError = error as { envelope?: WorkflowResponseEnvelope };
      if (apiError.envelope) {
        this._validationProblems = apiError.envelope.problems || [];
      }
    }

    this._setState('error');
  }

  private _setState(newState: OrchestratorState): void {
    this._state = newState;
    this.dispatchEvent(
      new CustomEvent<StateChangeEvent>('state-change', {
        detail: {
          state: newState,
          envelope: this._currentEnvelope,
        },
      })
    );
  }
}
