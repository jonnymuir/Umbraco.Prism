import type { AuthoredWorkflow } from './types.js';

export type TransitionConditionMode = 'always' | 'event' | 'guard';

export const TRANSITION_ACTION_OPTIONS = [
  { value: 'continue', label: 'Continue' },
  { value: 'submit', label: 'Submit' },
  { value: 'approve', label: 'Approve' },
  { value: 'reject', label: 'Reject' },
  { value: 'assign', label: 'Assign' },
  { value: 'return', label: 'Return' },
] as const;

export function transitionQuickAction(action?: string): string {
  return TRANSITION_ACTION_OPTIONS.some(option => option.value === (action ?? '').trim().toLowerCase())
    ? (action ?? '').trim().toLowerCase()
    : 'custom';
}

export function parseTransitionCondition(condition?: string): {
  mode: TransitionConditionMode;
  value: string;
} {
  const trimmed = condition?.trim() ?? '';
  if (!trimmed) {
    return { mode: 'always', value: '' };
  }

  if (trimmed.startsWith('event:')) {
    return { mode: 'event', value: trimmed.slice('event:'.length).trim() };
  }

  if (trimmed.startsWith('guard:')) {
    return { mode: 'guard', value: trimmed.slice('guard:'.length).trim() };
  }

  return { mode: 'guard', value: trimmed };
}

export function serialiseTransitionCondition(mode: TransitionConditionMode, value: string): string | undefined {
  if (mode === 'always') {
    return undefined;
  }

  return `${mode}:${value.trim()}`;
}

export function describeTransitionCondition(condition?: string): string {
  const parsed = parseTransitionCondition(condition);
  if (parsed.mode === 'always') {
    return 'Always available';
  }

  if (!parsed.value) {
    return parsed.mode === 'event' ? 'Event required' : 'Guard expression required';
  }

  return parsed.mode === 'event'
    ? `Event: ${parsed.value}`
    : `Guard: ${parsed.value}`;
}

export function defaultTransitionTarget(workflow: AuthoredWorkflow, sourceStageKey: string): string | null {
  const currentIndex = workflow.stages.findIndex(stage => stage.stageKey === sourceStageKey);
  if (currentIndex >= 0) {
    const nextStage = workflow.stages[currentIndex + 1];
    if (nextStage) {
      return nextStage.stageKey;
    }
  }

  return workflow.stages.find(stage => stage.stageKey !== sourceStageKey)?.stageKey ?? null;
}

export function defaultTransitionAction(workflow: AuthoredWorkflow, targetStageKey: string): string {
  return targetStageKey === workflow.initialStageKey ? 'return' : 'continue';
}
