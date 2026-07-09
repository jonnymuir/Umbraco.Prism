import { useMemo, useRef, useSyncExternalStore } from 'react';
import {
  ReactFlow,
  ReactFlowProvider,
  type EdgeTypes,
  type NodeTypes,
} from '@xyflow/react';
import { GraphCallbacksContext, type GraphProps } from './graph-callbacks.js';
import { buildGraphModel } from './graph-model.js';
import type { GraphBridge } from './graph-bridge.js';
import { StageNode } from './nodes/stage-node.js';
import { GatewayNode } from './nodes/gateway-node.js';
import { RouteEdge } from './edges/route-edge.js';
import { LaneLayer } from './lanes/lane-layer.js';

const nodeTypes = { stage: StageNode, gateway: GatewayNode } as NodeTypes;
const edgeTypes = { route: RouteEdge } as EdgeTypes;

export function GraphApp({ bridge }: { bridge: GraphBridge }) {
  const props = useSyncExternalStore(bridge.subscribe, bridge.getSnapshot);
  return (
    <ReactFlowProvider>
      <GraphCallbacksContext.Provider value={bridge.callbacks}>
        <WorkflowGraphCanvas bridge={bridge} props={props} />
      </GraphCallbacksContext.Provider>
    </ReactFlowProvider>
  );
}

function WorkflowGraphCanvas({ bridge, props }: { bridge: GraphBridge; props: GraphProps }) {
  const callbacks = bridge.callbacks;
  const model = useMemo(() => buildGraphModel(props), [props]);
  const readyFired = useRef(false);

  return (
    <ReactFlow
      nodes={model.nodes}
      edges={model.edges}
      nodeTypes={nodeTypes}
      edgeTypes={edgeTypes}
      minZoom={0.4}
      maxZoom={2}
      defaultViewport={{ x: 0, y: 0, zoom: 1 }}
      nodesDraggable={false}
      nodesConnectable={false}
      nodesFocusable={false}
      edgesFocusable={false}
      elementsSelectable={false}
      panOnDrag
      zoomOnScroll
      zoomOnPinch
      zoomOnDoubleClick={false}
      onInit={instance => {
        bridge.setFlowInstance(instance);
        callbacks.zoomChanged(instance.getZoom());
        // Readiness for test probes: nodes/edges are committed to the DOM two
        // frames after the viewport initialises.
        requestAnimationFrame(() => requestAnimationFrame(() => {
          if (!readyFired.current) {
            readyFired.current = true;
            callbacks.ready();
          }
        }));
      }}
      onMove={(_event, viewport) => callbacks.zoomChanged(viewport.zoom)}
      onPaneClick={() => callbacks.paneClicked()}
      onPaneContextMenu={event => {
        if (props.readOnly) {
          return;
        }
        event.preventDefault();
        callbacks.openContextMenu(
          { clientX: event.clientX, clientY: event.clientY },
          { kind: 'canvas' }
        );
      }}
    >
      <LaneLayer lanes={model.lanes} height={model.bounds.height} />
    </ReactFlow>
  );
}
