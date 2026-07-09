import { ViewportPortal } from '@xyflow/react';
import { useGraphCallbacks } from '../graph-callbacks.js';
import { TOP_PADDING, type LaneGeometry } from '../workflow-graph-layout.js';

/**
 * Vertical queue swim-lane bands. Rendered through a ViewportPortal so they
 * live in flow coordinates (pan/zoom with the nodes) without participating in
 * selection, drag, or fitView.
 */
export function LaneLayer({ lanes, height }: { lanes: LaneGeometry[]; height: number }) {
  const callbacks = useGraphCallbacks();
  return (
    <ViewportPortal>
      <div className="graph-lane-layer" style={{ position: 'absolute', top: 0, left: 0, zIndex: -1 }}>
        {lanes.map(lane => {
          const headingId = `queue-heading-${lane.key}`;
          const copyId = `queue-copy-${lane.key}`;
          return (
            <section
              key={lane.key}
              className={`lane ${lane.surface === 'back-stage' ? 'lane-supporting' : 'lane-primary'}`}
              style={{
                position: 'absolute',
                top: TOP_PADDING,
                left: lane.x,
                width: lane.width,
                height: Math.max(0, height - TOP_PADDING * 2),
              }}
              tabIndex={0}
              aria-labelledby={headingId}
              aria-describedby={copyId}
              data-prism-role-queue={lane.key}
              data-prism-queue-container={lane.key}
              onFocus={() => callbacks.laneFocused(lane)}
            >
              <div className="lane-header" data-prism-queue-header={lane.key}>
                <div id={headingId} className="lane-heading">{lane.label}</div>
                <div className="lane-meta">{lane.stageCount} stage{lane.stageCount === 1 ? '' : 's'}</div>
              </div>
              <div id={copyId} className="lane-copy">{lane.description}</div>
            </section>
          );
        })}
      </div>
    </ViewportPortal>
  );
}
