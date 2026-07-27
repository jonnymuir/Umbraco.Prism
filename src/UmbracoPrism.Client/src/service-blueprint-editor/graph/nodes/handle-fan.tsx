import { Handle, Position } from '@xyflow/react';
import type { CSSProperties } from 'react';
import type { HandleSlot } from '../graph-model.js';

const POSITION_BY_SIDE: Record<HandleSlot['side'], Position> = {
  top: Position.Top,
  bottom: Position.Bottom,
  left: Position.Left,
  right: Position.Right,
};

/**
 * Renders one handle per slot, offset along its side rather than pinned to
 * the centre — see assignHandleSlots in graph-model.ts for why a node needs
 * more than one anchor point per side once it genuinely fans out.
 */
function styleForSlot(slot: HandleSlot): CSSProperties {
  const percent = `${slot.offset * 100}%`;
  return slot.side === 'left' || slot.side === 'right' ? { top: percent } : { left: percent };
}

export function HandleFan({
  handles,
  type,
  readOnly,
}: {
  handles: HandleSlot[];
  type: 'source' | 'target';
  readOnly: boolean;
}) {
  return (
    <>
      {handles.map(slot => (
        <Handle
          key={slot.id}
          type={type}
          position={POSITION_BY_SIDE[slot.side]}
          id={slot.id}
          isConnectable={!readOnly}
          className="graph-handle"
          style={styleForSlot(slot)}
        />
      ))}
    </>
  );
}
