import { Handle, Position, type NodeProps } from '@xyflow/react';
import { useGraphCallbacks } from '../graph-callbacks.js';
import type { GatewayFlowNode } from '../graph-model.js';

export function GatewayNode({ data }: NodeProps<GatewayFlowNode>) {
  const callbacks = useGraphCallbacks();
  const { node, rowRank, selected, routeCount, triggerLabel, conditionLabel, readOnly } = data;
  const gateway = node.gateway;
  const isPill = node.pill;
  const shapeClass = isPill ? 'shape-pill' : 'shape-diamond';

  const handleKeyDown = (event: React.KeyboardEvent<HTMLButtonElement>) => {
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      callbacks.selectGateway(gateway.key);
      return;
    }
    if (event.key.toLowerCase() === 'e') {
      event.preventDefault();
      callbacks.selectGateway(gateway.key, { openInspector: true });
    }
  };

  const className = [
    'gateway-node',
    node.surface,
    `kind-${gateway.gatewayType.toLowerCase()}`,
    shapeClass,
    selected ? 'selected' : '',
  ].filter(Boolean).join(' ');

  return (
    <div
      className={`gateway-node-shell ${shapeClass}`}
      data-prism-gateway-node={gateway.key}
      data-prism-gateway-shape={isPill ? 'pill' : 'diamond'}
      data-prism-row-rank={String(rowRank)}
    >
      <Handle type="target" position={Position.Top} id="in" isConnectable={!readOnly} className="graph-handle" />
      <button
        type="button"
        className={className}
        aria-pressed={selected}
        aria-label={isPill
          ? `${gateway.displayName}, single-route gateway via “${triggerLabel}”, ${node.queueLabel} queue`
          : `${gateway.displayName}, ${gateway.gatewayType} gateway, ${node.queueLabel} queue`}
        data-prism-gateway={gateway.key}
        data-prism-gateway-kind={gateway.gatewayType}
        data-prism-gateway-route-count={String(routeCount)}
        data-prism-queue={node.queueKey}
        onClick={() => callbacks.selectGateway(gateway.key)}
        onDoubleClick={() => callbacks.selectGateway(gateway.key, { openInspector: true })}
        onKeyDown={handleKeyDown}
      >
        {isPill
          ? (
            <>
              <span className="pill-trigger">{triggerLabel || gateway.displayName}</span>
              {conditionLabel
                ? <span className="pill-condition" aria-label="conditional route" title={conditionLabel}>•</span>
                : null}
            </>
          )
          : <span className="node-label">{gateway.displayName}</span>}
      </button>
      <Handle type="source" position={Position.Bottom} id="out" isConnectable={!readOnly} className="graph-handle" />
    </div>
  );
}
