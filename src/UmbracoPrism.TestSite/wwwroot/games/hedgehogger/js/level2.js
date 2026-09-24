/**
 * Level 2: "Rain Garden"
 *
 * Deliberately introduces exactly one new thing, per the design doc's rule 2:
 * the level is longer than one screen, so the camera scrolls and the player
 * can't see the whole board at once. No new hazard TYPE appears here — every
 * lane below reuses Level 1's road/river/sprinkler vocabulary, just in fresh
 * arrangements (mixed obstacle types sharing one lane, tighter river gaps,
 * a second sprinkler gauntlet) and at a slightly faster pace. Still fully
 * deterministic — no randomness anywhere in this file.
 */

import { evenlySpaced } from './level-utils.js';

const W = 392; // logical width = cols(7) * laneSize(56)

export const LEVEL_2 = {
  id: 'level2',
  name: 'Rain Garden',
  cols: 7,
  rows: 19,
  viewportRows: 11, // shorter than `rows` -> the camera scrolls as you climb
  laneSize: 56,
  startCol: 3,
  goalCols: [1, 3, 5],
  introText: "It's a longer garden this time — keep an eye out, you won't see it all at once.",
  lanes: [
    // 0 — start
    { type: 'SAFE' },

    // 1 — buffer, first collectibles
    { type: 'SAFE', items: [{ col: 1, type: 'APPLE' }, { col: 5, type: 'APPLE' }] },

    // 2 — road, mowers moving right. Same start-column safety margin as
    // Level 1's row 2 — see that file's comment for why this phase isn't
    // arbitrary.
    {
      type: 'ROAD',
      dir: 1,
      speed: 84,
      obstacles: evenlySpaced(2, W, 100, 14).map((startX) => ({ kind: 'MOWER', width: 70, height: 34, startX })),
    },

    // 3 — road, cats moving left
    {
      type: 'ROAD',
      dir: -1,
      speed: 70,
      obstacles: evenlySpaced(2, W, 100, 224).map((startX) => ({ kind: 'CAT', width: 46, height: 30, startX })),
    },

    // 4 — safe median
    { type: 'SAFE', items: [{ col: 3, type: 'BEETLE' }] },

    // 5 — river, logs drifting right
    {
      type: 'RIVER',
      dir: 1,
      speed: 54,
      obstacles: evenlySpaced(3, W, 100).map((startX) => ({ kind: 'LOG', width: 118, height: 40, startX })),
    },

    // 6 — river, logs drifting left
    {
      type: 'RIVER',
      dir: -1,
      speed: 62,
      obstacles: evenlySpaced(3, W, 100, 96).map((startX) => ({ kind: 'LOG', width: 132, height: 40, startX })),
    },

    // 7 — safe buffer
    { type: 'SAFE', items: [{ col: 2, type: 'APPLE' }] },

    // 8 — telegraphed sprinkler gauntlet (same configuration Level 1 taught)
    {
      type: 'SPRINKLER',
      sprinklers: [
        { col: 1, radius: 74, cycle: 3.4, idleFor: 1.8, chargeFor: 0.6, burstFor: 1.0, phase: 0 },
        { col: 5, radius: 74, cycle: 3.4, idleFor: 1.8, chargeFor: 0.6, burstFor: 1.0, phase: 1.7 },
      ],
    },

    // 9 — safe buffer
    { type: 'SAFE', items: [{ col: 4, type: 'BEETLE' }] },

    // 10 — new combination: a road lane going the OTHER way and faster than
    // any road lane in Level 1 — same mechanic, sharper pace.
    {
      type: 'ROAD',
      dir: -1,
      speed: 96,
      obstacles: evenlySpaced(2, W, 100, 60).map((startX) => ({ kind: 'MOWER', width: 70, height: 34, startX })),
    },

    // 11 — breather after the faster road, with two collectibles
    { type: 'SAFE', items: [{ col: 1, type: 'APPLE' }, { col: 5, type: 'APPLE' }] },

    // 12 — new combination: a tighter river crossing (4 narrower logs
    // instead of 3 wide ones) — same mechanic, less margin for error.
    {
      type: 'RIVER',
      dir: 1,
      speed: 58,
      obstacles: evenlySpaced(4, W, 100).map((startX) => ({ kind: 'LOG', width: 84, height: 40, startX })),
    },

    // 13 — safe buffer
    { type: 'SAFE', items: [{ col: 3, type: 'BEETLE' }] },

    // 14 — new combination: a second sprinkler gauntlet, three sprinklers
    // this time instead of two, phases spread so there's still always a
    // resting column somewhere.
    {
      type: 'SPRINKLER',
      sprinklers: [
        { col: 1, radius: 68, cycle: 3.6, idleFor: 1.9, chargeFor: 0.6, burstFor: 1.1, phase: 0 },
        { col: 3, radius: 68, cycle: 3.6, idleFor: 1.9, chargeFor: 0.6, burstFor: 1.1, phase: 1.2 },
        { col: 5, radius: 68, cycle: 3.6, idleFor: 1.9, chargeFor: 0.6, burstFor: 1.1, phase: 2.4 },
      ],
    },

    // 15 — new combination: a road lane mixing a mower AND a cat together —
    // Level 1 never put two different obstacle kinds in the same lane.
    {
      type: 'ROAD',
      dir: 1,
      speed: 80,
      obstacles: [
        { kind: 'MOWER', width: 70, height: 34, startX: -100 },
        { kind: 'CAT', width: 46, height: 30, startX: 196 },
      ],
    },

    // 16 — final river crossing
    {
      type: 'RIVER',
      dir: -1,
      speed: 60,
      obstacles: evenlySpaced(3, W, 100, 40).map((startX) => ({ kind: 'LOG', width: 128, height: 40, startX })),
    },

    // 17 — safe buffer before the goal
    { type: 'SAFE', items: [{ col: 3, type: 'APPLE' }] },

    // 18 — goal row
    { type: 'GOAL' },
  ],
};
