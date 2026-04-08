// ⚠️ MOBILE BOUNDARY: No @umbraco-cms imports allowed in this directory.

export type WorkflowResponseState = 'ask_now' | 'wait' | 'complete' | 'error';

export type WorkflowArchetype =
  | 'Collect'
  | 'Review'
  | 'TaskQueue'
  | 'Decision'
  | 'RequestChanges'
  | 'StatusTimeline'
  | 'Completion';

export type FieldType =
  | 'text'
  | 'number'
  | 'date'
  | 'select'
  | 'radio'
  | 'checkbox'
  | 'textarea'
  | 'currency';

export interface WorkflowResponseEnvelope {
  instanceId: string;
  responseState: WorkflowResponseState;
  stateVersion: number;
  correlationId: string | null;
  serverTimeUtc: string;
  pollAfterMs: number | null;
  render: WorkflowRenderPayload | null;
  problems: WorkflowProblem[];
}

export interface WorkflowRenderPayload {
  archetype: WorkflowArchetype;
  stateDisplayName: string;
  fieldGroups: FieldGroupRenderPayload[];
  availableActions: WorkflowAction[];
}

export interface FieldGroupRenderPayload {
  groupKey: string;
  displayName: string;
  fields: FieldRenderPayload[];
}

export interface FieldRenderPayload {
  fieldKey: string;
  label: string;
  hint: string;
  fieldType: FieldType;
  required: boolean;
  value: unknown;
  options: string[] | null;
}

export interface WorkflowAction {
  actionKey: string;
  label: string;
  style: 'primary' | 'secondary' | 'destructive';
}

export interface WorkflowProblem {
  fieldKey: string;
  message: string;
  code: string;
}

export interface CreateInstanceRequest {
  definitionKey: string;
  correlationId?: string;
}

export interface AdvanceRequest {
  actionKey: string;
  fieldValues?: Record<string, unknown>;
  stateVersion: number;
}

export class WorkflowApiError extends Error {
  constructor(
    message: string,
    public readonly statusCode: number,
    public readonly envelope?: WorkflowResponseEnvelope
  ) {
    super(message);
    this.name = 'WorkflowApiError';
  }
}

export class WorkflowApiClient {
  private readonly baseUrl: string;

  constructor(baseUrl: string = '/umbraco/prism/workflow') {
    this.baseUrl = baseUrl;
  }

  async createInstance(request: CreateInstanceRequest): Promise<WorkflowResponseEnvelope> {
    return this._post<WorkflowResponseEnvelope>('/instances', request);
  }

  async getInstance(instanceId: string): Promise<WorkflowResponseEnvelope> {
    return this._get<WorkflowResponseEnvelope>(`/instances/${instanceId}`);
  }

  async advance(instanceId: string, request: AdvanceRequest): Promise<WorkflowResponseEnvelope> {
    return this._post<WorkflowResponseEnvelope>(`/instances/${instanceId}/advance`, request);
  }

  async deleteInstance(instanceId: string): Promise<void> {
    await this._delete(`/instances/${instanceId}`);
  }

  async getDefinitions(): Promise<unknown> {
    return this._get<unknown>('/definitions');
  }

  private async _get<T>(path: string): Promise<T> {
    const response = await fetch(`${this.baseUrl}${path}`, {
      method: 'GET',
      credentials: 'include',
      headers: {
        Accept: 'application/json',
      },
    });

    return this._handleResponse<T>(response);
  }

  private async _post<T>(path: string, body: unknown): Promise<T> {
    const response = await fetch(`${this.baseUrl}${path}`, {
      method: 'POST',
      credentials: 'include',
      headers: {
        'Content-Type': 'application/json',
        Accept: 'application/json',
      },
      body: JSON.stringify(body),
    });

    return this._handleResponse<T>(response);
  }

  private async _delete(path: string): Promise<void> {
    const response = await fetch(`${this.baseUrl}${path}`, {
      method: 'DELETE',
      credentials: 'include',
    });

    if (!response.ok) {
      throw new WorkflowApiError(
        `HTTP ${response.status}: ${response.statusText}`,
        response.status
      );
    }
  }

  private async _handleResponse<T>(response: Response): Promise<T> {
    if (!response.ok) {
      let envelope: WorkflowResponseEnvelope | undefined;
      try {
        envelope = await response.json();
      } catch {
        // Response might not be JSON
      }

      throw new WorkflowApiError(
        envelope?.problems?.[0]?.message || `HTTP ${response.status}: ${response.statusText}`,
        response.status,
        envelope
      );
    }

    return response.json() as Promise<T>;
  }
}
