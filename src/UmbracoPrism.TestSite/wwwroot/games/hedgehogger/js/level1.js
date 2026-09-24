/**
 * Level 1: "Garden Crossing"
 *
 * A deterministic, hand-authored playing field — no randomness anywhere in
 * this file. Every obstacle's starting position, speed and timing is a fixed
 * number, so the level plays out identically every single time: it's a
 * learnable puzzle, not a procedurally-random gauntlet. Future levels follow
 * the same shape (cols/rows/laneSize + a lanes array) so the engine stays
 * level-agnostic.
 *
 * Row 0 is the start (bottom); row `rows - 1` is the goal row (top).
 */

// Even spacing helper for obstacles that loop across a lane of width `w`,
// wrapping at +/-`margin`. Fully deterministic: same inputs, same layout.
function evenlySpaced(count, width, margin, phaseOffset = 0) {
  const cycle = width + margin * 2;
  const spacing = cycle / count;
  const out = [];
  for (let k = 0; k < count; k++) {
    let x = k * spacing - margin + phaseOffset;
    if (x > width + margin) x -= cycle;
    out.push(x);
  }
  return out;
}

const W = 392; // logical width = cols(7) * laneSize(56)

export const LEVEL_1 = {
  id: 'level1',
  name: 'Garden Crossing',
  cols: 7,
  rows: 11,
  laneSize: 56,
  startCol: 3,
  goalCols: [1, 3, 5],
  lanes: [
    // 0 — start
    { type: 'SAFE' },

    // 1 — buffer, first collectibles
    { type: 'SAFE', items: [{ col: 1, type: 'APPLE' }, { col: 5, type: 'APPLE' }] },

    // 2 — road, mowers moving right
    {
      type: 'ROAD',
      dir: 1,
      speed: 78,
      obstacles: evenlySpaced(2, W, 100).map((startX) => ({ kind: 'MOWER', width: 70, height: 34, startX })),
    },

    // 3 — road, cats moving left
    {
      type: 'ROAD',
      dir: -1,
      speed: 66,
      obstacles: evenlySpaced(2, W, 100, 148).map((startX) => ({ kind: 'CAT', width: 46, height: 30, startX })),
    },

    // 4 — safe median
    { type: 'SAFE', items: [{ col: 3, type: 'BEETLE' }] },

    // 5 — river, logs drifting right
    {
      type: 'RIVER',
      dir: 1,
      speed: 52,
      obstacles: evenlySpaced(3, W, 100).map((startX) => ({ kind: 'LOG', width: 118, height: 40, startX })),
    },

    // 6 — river, logs drifting left (zig-zag)
    {
      type: 'RIVER',
      dir: -1,
      speed: 60,
      obstacles: evenlySpaced(3, W, 100, 96).map((startX) => ({ kind: 'LOG', width: 132, height: 40, startX })),
    },

    // 7 — safe buffer
    { type: 'SAFE', items: [{ col: 2, type: 'APPLE' }] },

    // 8 — telegraphed sprinkler gauntlet: sprinklers are 180 degrees out of
    // phase, so there's always one "resting" side to cross through, and each
    // burst is preceded by a visible 0.6s charge-up — never a surprise.
    {
      type: 'SPRINKLER',
      sprinklers: [
        { col: 1, radius: 74, cycle: 3.4, idleFor: 1.8, chargeFor: 0.6, burstFor: 1.0, phase: 0 },
        { col: 5, radius: 74, cycle: 3.4, idleFor: 1.8, chargeFor: 0.6, burstFor: 1.0, phase: 1.7 },
      ],
    },

    // 9 — safe buffer
    { type: 'SAFE', items: [{ col: 4, type: 'BEETLE' }] },

    // 10 — goal row: reach any burrow to complete the level
    { type: 'GOAL' },
  ],
};
