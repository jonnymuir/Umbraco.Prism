export type ChipBox = { id: string; x: number; y: number; width: number; height: number };
export type ObstacleBox = { x: number; y: number; width: number; height: number };

function overlap(a: ObstacleBox, b: ObstacleBox): { ox: number; oy: number } | null {
  const ox = Math.min(a.x + a.width, b.x + b.width) - Math.max(a.x, b.x);
  const oy = Math.min(a.y + a.height, b.y + b.height) - Math.max(a.y, b.y);
  return ox > 0 && oy > 0 ? { ox, oy } : null;
}

const centerX = (box: ObstacleBox) => box.x + box.width / 2;
const centerY = (box: ObstacleBox) => box.y + box.height / 2;

/**
 * Nudges edge-label chips apart from each other and away from node bodies.
 * Each chip starts at its edge's natural path anchor (plus same-edge
 * stacking, applied by the caller); on a service blueprint with real fan-out/fan-in,
 * many of those anchors land on top of each other and on top of the gateway
 * they cluster around. Repeated passes of AABB separation (push along the
 * axis of least overlap) settle them into a legible, non-overlapping layout
 * without needing to know anything about edge routing itself.
 *
 * Every pass is Jacobi-style: displacement from every pairwise/obstacle
 * overlap involving a box is accumulated first and applied to all boxes at
 * once at the end of the pass, rather than mutating a box mid-pass and
 * letting later comparisons in the same pass read the half-updated result.
 * The latter (Gauss-Seidel-style immediate mutation) is order-dependent and
 * can settle into a stable non-converged state in a dense cluster — a box
 * pinned between several obstacles/chips keeps "solving" whichever
 * comparison ran last without the others ever being re-checked against its
 * new position in the same pass.
 *
 * Displacement is damped (scaled down) before being applied. Undamped, two
 * conflicting full-strength forces (e.g. an obstacle push and a sibling-chip
 * push pointing opposite ways) can land a box in a position that recreates
 * the exact opposite pair of overlaps next pass — a stable 2-cycle that
 * `moved` never stops seeing, so the loop never breaks and the box never
 * settles even after many iterations. Damping shrinks the oscillation's
 * amplitude every pass instead of repeating it exactly, so it decays toward
 * a resting position.
 */
const DAMPING = 0.6;

export function declutterChips(chips: ChipBox[], obstacles: ObstacleBox[], iterations = 120): Map<string, { x: number; y: number }> {
  const boxes = chips.map(chip => ({ ...chip }));
  const dx = new Array(boxes.length).fill(0) as number[];
  const dy = new Array(boxes.length).fill(0) as number[];

  for (let pass = 0; pass < iterations; pass++) {
    dx.fill(0);
    dy.fill(0);
    let moved = false;

    for (let i = 0; i < boxes.length; i++) {
      for (let j = i + 1; j < boxes.length; j++) {
        const box = overlap(boxes[i], boxes[j]);
        if (!box) {
          continue;
        }
        moved = true;
        if (box.ox < box.oy) {
          const push = box.ox / 2 + 1;
          const sign = centerX(boxes[i]) <= centerX(boxes[j]) ? -1 : 1;
          dx[i] += sign * push;
          dx[j] -= sign * push;
        } else {
          const push = box.oy / 2 + 1;
          const sign = centerY(boxes[i]) <= centerY(boxes[j]) ? -1 : 1;
          dy[i] += sign * push;
          dy[j] -= sign * push;
        }
      }
      for (const obstacle of obstacles) {
        const box = overlap(boxes[i], obstacle);
        if (!box) {
          continue;
        }
        moved = true;
        if (box.ox < box.oy) {
          const push = box.ox + 4;
          dx[i] += centerX(boxes[i]) <= centerX(obstacle) ? -push : push;
        } else {
          const push = box.oy + 4;
          dy[i] += centerY(boxes[i]) <= centerY(obstacle) ? -push : push;
        }
      }
    }

    if (!moved) {
      break;
    }
    for (let i = 0; i < boxes.length; i++) {
      boxes[i].x += dx[i] * DAMPING;
      boxes[i].y += dy[i] * DAMPING;
    }
  }

  return new Map(boxes.map(box => [box.id, { x: box.x, y: box.y }]));
}
