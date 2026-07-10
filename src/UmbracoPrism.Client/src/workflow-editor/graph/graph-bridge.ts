import { createElement } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import type { ReactFlowInstance } from '@xyflow/react';
import { GraphApp } from './graph-app.js';
import type { GraphCallbacks, GraphProps } from './graph-callbacks.js';
import type { GraphFlowNode, RouteFlowEdge } from './graph-model.js';

type FlowInstance = ReactFlowInstance<GraphFlowNode, RouteFlowEdge>;

function snapshotsEqual(left: GraphProps, right: GraphProps): boolean {
  return (Object.keys(left) as Array<keyof GraphProps>)
    .every(key => Object.is(left[key], right[key]));
}

/**
 * Owns the React root for the graph canvas inside the Lit component's shadow
 * root. Renders once; subsequent Lit updates flow through a
 * useSyncExternalStore snapshot instead of repeated root.render calls.
 */
export class GraphBridge {
  readonly callbacks: GraphCallbacks;

  private root: Root | null;
  private snapshot: GraphProps;
  private readonly listeners = new Set<() => void>();
  private flow: FlowInstance | null = null;

  constructor(container: HTMLElement, initialProps: GraphProps, callbacks: GraphCallbacks) {
    this.callbacks = callbacks;
    this.snapshot = initialProps;
    this.root = createRoot(container);
    this.root.render(createElement(GraphApp, { bridge: this }));
  }

  readonly subscribe = (listener: () => void): (() => void) => {
    this.listeners.add(listener);
    return () => this.listeners.delete(listener);
  };

  readonly getSnapshot = (): GraphProps => this.snapshot;

  update(props: GraphProps) {
    if (snapshotsEqual(this.snapshot, props)) {
      return;
    }
    this.snapshot = props;
    this.listeners.forEach(listener => listener());
  }

  setFlowInstance(flow: FlowInstance) {
    this.flow = flow;
  }

  fitView() {
    void this.flow?.fitView({ padding: 0.1, duration: 200 });
  }

  /** Pan/zoom so a single node (e.g. one just created) is visibly in frame. */
  centerOnNode(nodeId: string) {
    void this.flow?.fitView({ nodes: [{ id: nodeId }], padding: 0.6, duration: 260, maxZoom: 1 });
  }

  zoomIn() {
    void this.flow?.zoomIn({ duration: 120 });
  }

  zoomOut() {
    void this.flow?.zoomOut({ duration: 120 });
  }

  unmount() {
    const root = this.root;
    this.root = null;
    this.flow = null;
    // Deferred: React must never unmount synchronously from inside a Lit
    // lifecycle callback that may itself be running during a React render.
    queueMicrotask(() => root?.unmount());
  }
}
