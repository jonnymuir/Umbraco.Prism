/**
 * Level 3: "Midnight Prowl"
 *
 * Deliberately the same terrain as Level 2 ("Rain Garden"), lane for lane —
 * the player already knows this ground. The one new thing is the prowling
 * cat (see docs/games/hedgehogger-design.md's "Mechanic spec"): a chaser
 * whose row can only ever be at or below the player's own row, so it always
 * approaches from a known, visible direction. Tuned deliberately gently
 * (long start delay, slow climb) since this is only the third level — it
 * should add a new layer of jeopardy, not a difficulty spike.
 */

import { evenlySpaced } from './level-utils.js';

const W = 392; // logical width = cols(7) * laneSize(56)

export const LEVEL_3 = {
  id: 'level3',
  name: 'Midnight Prowl',
  cols: 7,
  rows: 19,
  viewportRows: 11,
  laneSize: 56,
  startCol: 3,
  goalCols: [1, 3, 5],
  introText: "Something's stalking the garden tonight — keep moving, and remember: rolling shakes off a pounce.",
  chaser: {
    idleGrace: 2.5, // seconds you can stand still (any lane) before it starts closing in at all
    climbRate: 0.5, // rows/sec it closes the gap WHILE you're idle beyond idleGrace
    turnSpeed: 2.5, // how fast it eases toward the player's column
    prowlRange: 2.5, // rows away before the stalking telegraph (crouch pose) kicks in
    catchRange: 0.6, // rows away before a pounce is actually possible
  },
  lanes: [
    // 0 — start
    { type: 'SAFE' },

    // 1 — buffer, first collectibles
    { type: 'SAFE', items: [{ col: 1, type: 'APPLE' }, { col: 5, type: 'APPLE' }] },

    // 2 — road, mowers moving right
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

    // 8 — telegraphed sprinkler gauntlet
    {
      type: 'SPRINKLER',
      sprinklers: [
        { col: 1, radius: 74, cycle: 3.4, idleFor: 1.8, chargeFor: 0.6, burstFor: 1.0, phase: 0 },
        { col: 5, radius: 74, cycle: 3.4, idleFor: 1.8, chargeFor: 0.6, burstFor: 1.0, phase: 1.7 },
      ],
    },

    // 9 — safe buffer
    { type: 'SAFE', items: [{ col: 4, type: 'BEETLE' }] },

    // 10 — road, faster mowers going the other way
    {
      type: 'ROAD',
      dir: -1,
      speed: 96,
      obstacles: evenlySpaced(2, W, 100, 60).map((startX) => ({ kind: 'MOWER', width: 70, height: 34, startX })),
    },

    // 11 — breather, with two collectibles
    { type: 'SAFE', items: [{ col: 1, type: 'APPLE' }, { col: 5, type: 'APPLE' }] },

    // 12 — tighter river crossing
    {
      type: 'RIVER',
      dir: 1,
      speed: 58,
      obstacles: evenlySpaced(4, W, 100).map((startX) => ({ kind: 'LOG', width: 84, height: 40, startX })),
    },

    // 13 — safe buffer
    { type: 'SAFE', items: [{ col: 3, type: 'BEETLE' }] },

    // 14 — three-sprinkler gauntlet
    {
      type: 'SPRINKLER',
      sprinklers: [
        { col: 1, radius: 68, cycle: 3.6, idleFor: 1.9, chargeFor: 0.6, burstFor: 1.1, phase: 0 },
        { col: 3, radius: 68, cycle: 3.6, idleFor: 1.9, chargeFor: 0.6, burstFor: 1.1, phase: 1.2 },
        { col: 5, radius: 68, cycle: 3.6, idleFor: 1.9, chargeFor: 0.6, burstFor: 1.1, phase: 2.4 },
      ],
    },

    // 15 — road lane mixing a mower and a cat
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
