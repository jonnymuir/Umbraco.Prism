import type { NodeProps } from '@xyflow/react';
import { useGraphCallbacks } from '../graph-callbacks.js';
import type { StageFlowNode } from '../graph-model.js';
import { iconForStage } from '../node-icons.js';
import { HandleFan } from './handle-fan.js';
import { NodeIcon } from './node-icon.js';

export function StageNode({ data }: NodeProps<StageFlowNode>) {
  const callbacks = useGraphCallbacks();
  const { node, rowRank, sourceHandles, targetHandles, selected, simulationPath, simulationCurrent, readOnly } = data;
  const stage = node.stage;

  const handleKeyDown = (event: React.KeyboardEvent<HTMLButtonElement>) => {
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      callbacks.selectStage(stage.stateKey);
      return;
    }
    if (event.key.toLowerCase() === 'e') {
      event.preventDefault();
      callbacks.selectStage(stage.stateKey, { openInspector: true });
      return;
    }
    if (event.key === 'Delete' || event.key === 'Backspace') {
      event.preventDefault();
      callbacks.requestDeleteStage(stage.stateKey, event.currentTarget);
      return;
    }
    if (event.key === 'ContextMenu' || (event.shiftKey && event.key === 'F10')) {
      event.preventDefault();
      const rect = event.currentTarget.getBoundingClientRect();
      callbacks.openContextMenu(
        { clientX: rect.left + rect.width / 2, clientY: rect.bottom },
        { kind: 'stage', stageKey: stage.stateKey },
        event.currentTarget
      );
    }
  };

  const handleContextMenu = (event: React.MouseEvent<HTMLButtonElement>) => {
    if (readOnly) {
      return;
    }
    event.preventDefault();
    event.stopPropagation();
    callbacks.openContextMenu(
      { clientX: event.clientX, clientY: event.clientY },
      { kind: 'stage', stageKey: stage.stateKey },
      event.currentTarget
    );
  };

  const className = [
    'stage-node',
    node.surface,
    selected ? 'selected' : '',
    simulationPath ? 'simulation-path' : '',
    simulationCurrent ? 'simulation-current' : '',
  ].filter(Boolean).join(' ');

  return (
    <div
      className="stage-node-shell"
      data-prism-stage-card={stage.stateKey}
      data-prism-row-rank={String(rowRank)}
    >
      <HandleFan handles={targetHandles} type="target" readOnly={readOnly} />
      <button
        type="button"
        className={className}
        aria-pressed={selected}
        aria-label={`${stage.displayName}, ${node.queueLabel} queue`}
        data-prism-stage={stage.stateKey}
        data-prism-queue={node.queueKey}
        data-prism-stage-simulation-path={String(simulationPath)}
        data-prism-stage-simulation-current={String(simulationCurrent)}
        onClick={() => callbacks.selectStage(stage.stateKey)}
        onDoubleClick={() => callbacks.selectStage(stage.stateKey, { openInspector: true })}
        onKeyDown={handleKeyDown}
        onContextMenu={handleContextMenu}
      >
        <span className="node-header">
          <span className="node-icon-chip"><NodeIcon icon={iconForStage(stage)} /></span>
          <span className="node-meta">{stage.kind}</span>
        </span>
        <span className="node-label">{stage.displayName}</span>
      </button>
      <HandleFan handles={sourceHandles} type="source" readOnly={readOnly} />
    </div>
  );
}
