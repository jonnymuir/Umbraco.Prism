import { useCallback, useEffect, useMemo, useRef, useState, useSyncExternalStore } from 'react';
import {
  Background,
  BackgroundVariant,
  MiniMap,
  ReactFlow,
  ReactFlowProvider,
  SelectionMode,
  applyNodeChanges,
  useReactFlow,
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
import { laneForPosition } from './service-blueprint-graph-layout.js';

const nodeTypes = { stage: StageNode, gateway: GatewayNode } as NodeTypes;
const edgeTypes = { route: RouteEdge } as EdgeTypes;

export function GraphApp({ bridge }: { bridge: GraphBridge }) {
  const props = useSyncExternalStore(bridge.subscribe, bridge.getSnapshot);
  return (
    <ReactFlowProvider>
      <GraphCallbacksContext.Provider value={bridge.callbacks}>
        <ServiceBlueprintGraphCanvas bridge={bridge} props={props} />
      </GraphCallbacksContext.Provider>
    </ReactFlowProvider>
  );
}

/**
 * The serviceBlueprint document is the source of truth: local React Flow node state
 * exists only so in-flight drags render smoothly, and re-seeds whenever the
 * host pushes a new snapshot (every commit replaces the serviceBlueprint object).
 */
function useControlledNodes(model: GraphModel) {
  const [nodes, setNodes] = useState(model.nodes);
  useEffect(() => {
    // Carry the RF-level multi-selection across reseeds so a host re-render
    // doesn't dissolve an in-progress marquee selection.
    setNodes(current => {
      const selectedIds = new Set(current.filter(node => node.selected).map(node => node.id));
      return selectedIds.size === 0
        ? model.nodes
        : model.nodes.map(node => selectedIds.has(node.id) ? { ...node, selected: true } : node);
    });
  }, [model]);
  const onNodesChange = useCallback((changes: NodeChange<GraphFlowNode>[]) => {
    // Position changes keep in-flight drags smooth; select changes power the
    // shift-marquee multi-drag; dimensions changes record each node's
    // ResizeObserver-measured size (`node.measured`), which React Flow
    // requires to keep handle bounds valid — dropping these meant every
    // drag reseeded a same-id-but-new-identity node with `measured`
    // undefined, silently discarding its handle bounds and breaking
    // connected edges part-way through a drag. Everything else is derived
    // from the document.
    const localChanges = changes.filter(change =>
      change.type === 'position' || change.type === 'select' || change.type === 'dimensions'
    );
    if (localChanges.length === 0) {
      return;
    }
    setNodes(current => applyNodeChanges(localChanges, current) as GraphFlowNode[]);
  }, []);
  return { nodes, onNodesChange };
}

function ServiceBlueprintGraphCanvas({ bridge, props }: { bridge: GraphBridge; props: GraphProps }) {
  const callbacks = bridge.callbacks;
  const model = useMemo(() => buildGraphModel(props), [props]);
  const { nodes, onNodesChange } = useControlledNodes(model);
  const { screenToFlowPosition } = useReactFlow();
  const readyFired = useRef(false);

  // React Flow resolves drop targets with document.elementFromPoint, which
  // cannot see into the shadow root — so connection drops are hit-tested
  // against the node rects in flow coordinates instead.
  const nodeIdAtScreenPoint = useCallback((clientX: number, clientY: number): string | null => {
    const point = screenToFlowPosition({ x: clientX, y: clientY });
    const hit = nodes.find(candidate =>
      point.x >= candidate.position.x
      && point.x <= candidate.position.x + (candidate.width ?? 0)
      && point.y >= candidate.position.y
      && point.y <= candidate.position.y + (candidate.height ?? 0));
    return hit?.id ?? null;
  }, [nodes, screenToFlowPosition]);

  const handleNodeDragStop = useCallback(
    (_event: unknown, node: GraphFlowNode, draggedNodes: GraphFlowNode[]) => {
      // React Flow only fills the third argument for selection drags.
      const dragged = draggedNodes.length > 0 ? draggedNodes : [node];
      const moves: GraphNodeMove[] = dragged.map(dragged => {
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
      nodesConnectable={!props.readOnly}
      autoPanOnConnect={false}
      isValidConnection={connection => connection.source !== connection.target}
      onConnect={connection => {
        if (connection.source && connection.target) {
          callbacks.connectRequested({ sourceId: connection.source, targetId: connection.target });
        }
      }}
      onConnectEnd={(event, connectionState) => {
        // onConnect only fires for handle-precise drops; accept a drop
        // anywhere on a node body as connecting to that node.
        if (connectionState.isValid) {
          return;
        }
        const sourceId = connectionState.fromNode?.id;
        if (!sourceId) {
          return;
        }
        let targetId = connectionState.toNode?.id ?? null;
        if (!targetId) {
          const pointer = 'changedTouches' in event ? event.changedTouches[0] : event;
          targetId = nodeIdAtScreenPoint(pointer.clientX, pointer.clientY);
        }
        if (targetId && targetId !== sourceId) {
          callbacks.connectRequested({ sourceId, targetId });
        }
      }}
      nodesFocusable={false}
      edgesFocusable={false}
      // RF-level selection exists purely for shift-marquee multi-drag; the
      // semantic single selection lives on the inner buttons.
      elementsSelectable={!props.readOnly}
      selectionKeyCode="Shift"
      multiSelectionKeyCode="Shift"
      selectionMode={SelectionMode.Partial}
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
      onSelectionChange={({ nodes: selectedNodes }) =>
        callbacks.multiSelectionChanged(selectedNodes.map(selected => selected.id))}
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
      <Background variant={BackgroundVariant.Dots} gap={20} size={1.4} color="#cbd5e1" />
      <LaneLayer lanes={model.lanes} height={model.bounds.height} />
      <MiniMap
        pannable
        zoomable
        ariaLabel="ServiceBlueprint overview map"
        className="graph-minimap"
        nodeColor={node => (node.type === 'gateway' ? '#c4b5fd' : '#93c5fd')}
        nodeStrokeColor="#475569"
      />
    </ReactFlow>
  );
}
