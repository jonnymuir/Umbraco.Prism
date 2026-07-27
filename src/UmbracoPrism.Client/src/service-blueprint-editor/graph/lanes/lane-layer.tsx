import { ViewportPortal } from '@xyflow/react';
import { useGraphCallbacks } from '../graph-callbacks.js';
import { TOP_PADDING, type LaneGeometry } from '../service-blueprint-graph-layout.js';

/**
 * Vertical queue swim-lane bands. Rendered through a ViewportPortal so they
 * live in flow coordinates (pan/zoom with the nodes) without participating in
 * selection, drag, or fitView.
 *
 * Split into two portals: a purely decorative backdrop (background wash +
 * border) sunk behind nodes/edges with a negative z-index, and the
 * interactive/accessible band (header, description, keyboard focus target)
 * painted in normal stacking order so its text is never crossed out by a
 * route line drawn through the header zone. The interactive band has no
 * background of its own and disables pointer events so it never steals
 * clicks from the stage/gateway cards it visually overlaps.
 */
export function LaneLayer({ lanes, height }: { lanes: LaneGeometry[]; height: number }) {
  const callbacks = useGraphCallbacks();
  return (
    <>
      <ViewportPortal>
        <div className="graph-lane-backdrop" style={{ position: 'absolute', top: 0, left: 0, zIndex: -1 }} aria-hidden="true">
          {lanes.map(lane => (
            <div
              key={lane.key}
              className={`lane-band ${lane.surface === 'back-stage' ? 'lane-supporting' : 'lane-primary'}`}
              style={{
                position: 'absolute',
                top: TOP_PADDING,
                left: lane.x,
                width: lane.width,
                height: Math.max(0, height - TOP_PADDING * 2),
              }}
            />
          ))}
        </div>
      </ViewportPortal>
      <ViewportPortal>
        <div className="graph-lane-layer" style={{ position: 'absolute', top: 0, left: 0, zIndex: 10 }}>
          {lanes.map(lane => {
            const headingId = `queue-heading-${lane.key}`;
            return (
              <section
                key={lane.key}
                className="lane"
                style={{
                  position: 'absolute',
                  top: TOP_PADDING,
                  left: lane.x,
                  width: lane.width,
                  height: Math.max(0, height - TOP_PADDING * 2),
                  pointerEvents: 'none',
                }}
                tabIndex={0}
                aria-labelledby={headingId}
                data-prism-role-queue={lane.key}
                data-prism-queue-container={lane.key}
                onFocus={() => callbacks.laneFocused(lane)}
              >
                <div className="lane-header" data-prism-queue-header={lane.key}>
                  <div id={headingId} className="lane-heading">{lane.label}</div>
                  <div className="lane-meta">{lane.stageCount} stage{lane.stageCount === 1 ? '' : 's'}</div>
                </div>
              </section>
            );
          })}
        </div>
      </ViewportPortal>
    </>
  );
}
