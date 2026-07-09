import { createContext, useContext } from 'react';
import type { AuthoredWorkflow } from '../types.js';
import type { WorkflowQueueDefinition } from '../workflow-stage-assignment.js';

/**
 * Props snapshot pushed from the Lit wrapper into the React canvas on every
 * Lit update. Mirrors the public properties of <prism-workflow-graph>.
 */
export type GraphProps = {
  workflow: AuthoredWorkflow | null;
  availableQueues: WorkflowQueueDefinition[];
  readOnly: boolean;
  selectedStageKey: string | null;
  selectedGatewayKey: string | null;
  selectedTransitionIndex: number | null;
  simulationCurrentStageKey: string | null;
  simulationPathStageKeys: string[];
  simulationPathTransitionIndices: number[];
};

export type GraphContextMenuTarget =
  | { kind: 'canvas' }
  | { kind: 'stage'; stageKey: string }
  | { kind: 'transition'; transitionIndex: number };

/**
 * Semantic callbacks from the React canvas back into the Lit wrapper. The
 * canvas interprets pointer/keyboard gestures; the wrapper owns selection
 * events, dialogs, the context menu, and announcements.
 */
export type GraphCallbacks = {
  selectStage(stageKey: string, options?: { openInspector?: boolean }): void;
  selectGateway(gatewayKey: string, options?: { openInspector?: boolean }): void;
  selectTransition(transitionIndex: number, options?: { openInspector?: boolean }): void;
  requestDeleteStage(stageKey: string, returnTarget?: HTMLElement): void;
  requestDeleteTransition(transitionIndex: number): void;
  openContextMenu(
    position: { clientX: number; clientY: number },
    target: GraphContextMenuTarget,
    returnTarget?: HTMLElement
  ): void;
  paneClicked(): void;
  laneFocused(lane: { label: string; description: string; stageCount: number }): void;
  zoomChanged(zoom: number): void;
  ready(): void;
};

export const GraphCallbacksContext = createContext<GraphCallbacks | null>(null);

export function useGraphCallbacks(): GraphCallbacks {
  const callbacks = useContext(GraphCallbacksContext);
  if (!callbacks) {
    throw new Error('GraphCallbacksContext is not provided.');
  }
  return callbacks;
}
