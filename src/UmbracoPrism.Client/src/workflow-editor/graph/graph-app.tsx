import { useCallback, useEffect, useMemo, useRef, useState, useSyncExternalStore } from 'react';
import {
  ReactFlow,
  ReactFlowProvider,
  applyNodeChanges,
  type EdgeTypes,
  type NodeChange,
  type NodeTypes,
} from '@xyflow/react';
import { GraphCallbacksContext, type GraphNodeMove, type GraphProps } from './graph-callbacks.js';
import { buildGraphModel, type GraphFlowNode, type GraphModel } from './graph-model.js';
import type { GraphBridge } from './graph-bridge.js';
import { StageNode } from './nodes/stage-node.js';
import { GatewayNode } from './nodes/gateway-node.js';
import { RouteEdge } from './edges/route-edge.js';
import { LaneLayer } from './lanes/lane-layer.js';
import { laneForPosition } from './workflow-graph-layout.js';

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

/**
 * The workflow document is the source of truth: local React Flow node state
 * exists only so in-flight drags render smoothly, and re-seeds whenever the
 * host pushes a new snapshot (every commit replaces the workflow object).
 */
function useControlledNodes(model: GraphModel) {
  const [nodes, setNodes] = useState(model.nodes);
  useEffect(() => {
    setNodes(model.nodes);
  }, [model]);
  const onNodesChange = useCallback((changes: NodeChange<GraphFlowNode>[]) => {
    const positionChanges = changes.filter(change => change.type === 'position');
    if (positionChanges.length === 0) {
      return;
    }
    setNodes(current => applyNodeChanges(positionChanges, current) as GraphFlowNode[]);
  }, []);
  return { nodes, onNodesChange };
}

function WorkflowGraphCanvas({ bridge, props }: { bridge: GraphBridge; props: GraphProps }) {
  const callbacks = bridge.callbacks;
  const model = useMemo(() => buildGraphModel(props), [props]);
  const { nodes, onNodesChange } = useControlledNodes(model);
  const readyFired = useRef(false);

  const handleNodeDragStop = useCallback(
    (_event: unknown, _node: GraphFlowNode, draggedNodes: GraphFlowNode[]) => {
      const moves: GraphNodeMove[] = draggedNodes.map(dragged => {
        const width = dragged.width ?? 0;
        const currentQueue = dragged.data.node.queueKey;
        const lane = laneForPosition(model.lanes, dragged.position.x + width / 2);
        return {
          nodeId: dragged.id,
          x: dragged.position.x,
          y: dragged.position.y,
          queueKey: lane && lane.key !== currentQueue ? lane.key : null,
        };
      });
      if (moves.length > 0) {
        callbacks.nodesMoved(moves);
      }
    },
    [callbacks, model.lanes]
  );

  return (
    <ReactFlow
      nodes={nodes}
      edges={model.edges}
      nodeTypes={nodeTypes}
      edgeTypes={edgeTypes}
      minZoom={0.4}
      maxZoom={2}
      defaultViewport={{ x: 0, y: 0, zoom: 1 }}
      nodesDraggable={!props.readOnly}
      nodeDragThreshold={4}
      onNodesChange={onNodesChange}
      onNodeDragStop={handleNodeDragStop}
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
