# Hedgehogger: game design document

Hedgehogger is the undersold easter egg on the `UmbracoPrism.TestSite` home page (a plain 🦔
link, no card, no icon — see `homePage.cshtml`). It has no bearing on Prism's own tenancy/
branding/mobile feature set; this document exists purely so the game's own design stays
coherent as it grows across sessions. It lives at
`src/UmbracoPrism.TestSite/wwwroot/games/hedgehogger/`, outside Umbraco's content pipeline,
built with zero dependencies and no build step (plain ES modules, loaded directly by the
browser). This doc is **not** served by the app (it lives under `docs/`, not `wwwroot/`).

## Design pillars

These are the rules we've agreed on. Treat them as load-bearing — a change that breaks one of
these should be a deliberate decision, not a side effect.

1. **Every level is deterministic.** No `Math.random()` anywhere in a level's gameplay-relevant
   data or timing. Obstacle positions, speeds, and hazard cycles are all fixed numbers, or pure
   functions of elapsed play time (`playAge`). The same level plays out identically every single
   run, on every device. This is what makes a level a *puzzle* instead of a slot machine — it's
   learnable and masterable, and a screenshot/video of a run is reproducible.
2. **The game evolves into a puzzle game, one mechanic at a time.** Each new level either
   introduces exactly one new mechanic, or recombines existing mechanics in a new arrangement/
   difficulty. Never both at once, and never more than one brand-new mechanic per level — that's
   how the pacing teaches players instead of overwhelming them. See "Mechanic ledger" below for
   the running list of what's been taught and when.
3. **Pacing should always feel fun, never unfair.** A death must always be something the player
   could have seen coming and reacted to. Concretely: hazards are telegraphed before they're
   lethal (see the sprinkler's charge-up glow), hazard checks only apply once a hop has actually
   landed (no mid-air/instant deaths), and gaps/timings are generous enough that an attentive
   player — not a lucky one — always has a route through. We verify this with an automated
   "informed dodge" bot (see "Fairness testing" below), not by eyeballing it.
4. **Progress and scores persist locally.** No backend — everything lives in `localStorage`.
   See "Save data" below for the exact schema.
5. **Timing matters.** Every level is scored on both a running point total *and* completion
   time, and the completion time is what's remembered as the level's "best" (alongside best
   score). Speed is a real skill axis here, not a decoration — a level's obstacle pacing should
   be tuned so that a clean, no-hesitation run is meaningfully faster than a cautious one, which
   is what gives the timer teeth.

## Scoring rules

| Event | Points |
|---|---|
| Advancing to a new furthest lane (first time only, per run) | +10 per lane |
| Eating an apple | +15 |
| Eating a beetle | +30 |
| Reaching the goal (completing the level) | +200 |

Score and best-time are tracked **per level** (see save schema) — a longer, harder level
naturally scores higher, so comparing raw scores across levels isn't meaningful.

## Engine architecture

```
wwwroot/games/hedgehogger/
  index.html        — page shell: the canvas, the back-to-app link, the map link, loads js/main.js
  game.css           — layout/letterboxing/link styling only; all game visuals are Canvas
  js/
    main.js          — orchestrator: builds the level roster, owns hub<->engine mode switching
    engine.js         — state machine, physics, collision, camera, save data, rendering orchestration
    hub.js             — the level-select home screen (see "The progress hub" below)
    sprites.js          — every visual: layered/shaded Canvas 2D vector art, zero image assets
    level-utils.js       — shared level-authoring helpers (e.g. `evenlySpaced` obstacle placement)
    level1.js, level2.js, level3.js, ...  — one file per level, pure declarative data
```

Adding a level means writing a new `levelN.js` data file and adding it to the `LEVELS` array in
`main.js` — the engine itself should never need level-specific `if` branches. If a new mechanic
needs new engine code (a new lane `type`, a new hazard shape), that code goes in `engine.js` +
`sprites.js` generically, driven entirely by data on the level object, not hardcoded to a level
id.

### Level data shape

```js
{
  id: 'level2',            // used for save-data keys; must be unique and stable once shipped
  name: 'Rain Garden',       // shown in the HUD and START banner
  nextLevelId: 'level3',     // omit on the last level — banner falls back to "PLAY AGAIN"
  introText: '...',          // optional level-specific blurb on the START banner
  cols: 7,                   // playing-field width, in columns
  rows: 11,                  // level LENGTH, in rows — can exceed the viewport (see below)
  viewportRows: 11,          // optional; how many rows are visible on screen at once.
                              // Omit it (or set it equal to `rows`) for a single-screen level
                              // with no camera scroll, like Level 1. Set it smaller than `rows`
                              // for a longer level the camera scrolls through as you climb.
  laneSize: 56,               // px per row/column (logical, pre-DPR/scale)
  startCol: 3,
  goalCols: [1, 3, 5],        // burrow columns on the GOAL row (the last row)
  lanes: [ /* one entry per row, index 0 = start (bottom) */ ],
}
```

Lane `type`s implemented so far: `SAFE`, `ROAD` (`MOWER`/`CAT` obstacles), `RIVER` (`LOG`
obstacles), `SPRINKLER` (telegraphed radial hazard), `GOAL` (the last row; only present once,
must be `rows - 1`).

### Fixed playing field, scrollable when a level needs length

The **viewport** (what's rendered on screen, i.e. `logicalWidth` × `logicalHeight`) is always a
fixed, letterboxed-to-fit size — that's what makes the puzzle "the same playing field on every
device." A level's own length (`rows`) is independent of that: a short level like Level 1 sets
no `viewportRows`, so the whole level fits on one screen and the camera never moves (`cameraY`
stays `0` for its whole run, verified — this is a special case of the general camera logic below,
not a separate code path). A longer level sets `viewportRows` below `rows`, and the engine's
camera follows the player upward, clamped to the level's bounds, panning through a level that's
taller than what's ever on screen at once.

### Fairness testing

Level fairness isn't judged by eye, and a naive "bot" isn't automatically trustworthy either —
building this out for Level 2 turned up several ways a fairness test can lie to you. The
methodology that actually held up:

1. **Exhaustive proof for continuous hazards (ROAD/RIVER).** These have no advance-warning
   telegraph, so their fairness bar is: *at every instant, across a lane's full periodic cycle,
   is there never a run of 3+ consecutive columns that are all simultaneously unsafe?* A run of
   2 is fine (a player arriving from any of the 3 reachable columns — `col-1`/`col`/`col+1` —
   always has at least one safe option); a run of 3 can strand the middle column. Check this by
   calling the *real* engine functions (`obstacleX()`, `sprinklerState()`) directly against a
   fine-grained time sweep across one full cycle — not a hand-rolled reimplementation of the
   collision math, which is exactly how a subtle mismatch (e.g. reading a level's raw `laneSize`
   under the wrong property name) can silently make a checker vacuously always-pass or
   always-fail without erroring. This check is time-independent: if it passes, the lane is safe
   *no matter when* a player arrives, which matters because arrival time varies across playthroughs.
2. **A generous fixed-time margin for the very first hazard lane specifically.** Every other lane
   gets visited at a time that varies with how the player actually played, which is what makes
   the exhaustive per-cycle proof above sufficient on its own. The *first* hazard lane
   (Level 1/2's row 2) is a special case: a fresh run always reaches it at ~0 seconds in, at a
   fixed column, with zero prior chance to observe or react. A lane can pass the general
   exhaustive proof and *still* have a narrow real danger window right at t=0 if its phase
   happens to place an obstacle near the start column early on — that's exactly what shipped in
   Level 1 originally (danger only until t≈0.17s, narrow enough that testing with realistic
   reaction speeds missed it, but a fast enough double-hop could still hit it). The fix: search
   for the obstacle phase that maximizes the safe window at the start column specifically
   (`find_safe_start_phase.js`-style search), and target a couple of seconds of margin, not
   fractions of a second — this is the one lane where "provably safe forever" isn't the bar, but
   "safe for any humanly-plausible arrival speed, with real margin" is.
3. **A continuously-reactive bot for telegraphed hazards (SPRINKLER) and as a final end-to-end
   check.** The exhaustive per-instant check above will show sprinkler lanes as "unsafe" (a
   burst's radius can span 3 columns) — that's an expected false positive, because it doesn't
   model the 0.6s charge-phase warning a real player gets to react to. For these, and as a final
   full-level sanity check, use a bot that polls frequently (~100ms) and, at *every* tick, checks
   whether its *current* tile is becoming unsafe soon — not just whether the *next* lane is safe
   before advancing. That distinction matters: several earlier "failures" during this level's
   development turned out to be an earlier, less careful bot simply standing still in the middle
   of a live road/sprinkler lane for the length of its whole poll interval — which is genuinely
   dangerous (cars keep moving, sprinklers keep cycling), but is also not something an attentive
   player would ever do. "Keep moving once you're on a hazard lane, don't idle in traffic" is a
   basic, expected rule of this genre, not a hidden unfairness — the reactive bot's job is to
   model *that*, not to model an unrealistic player who freezes.
4. Separately, a "blind rush" test (always move the same direction, ignore all hazards) is
   expected to die sometimes — that's the game being a game. Don't confuse that with the tests
   above.

The bar for 1–3 is **zero deaths**. A death there means a real bug, not a skill check.

## Save data (localStorage)

| Key | Shape | Meaning |
|---|---|---|
| `hh_progress` | `{ unlocked: string[], lastPlayed: string \| null }` | Which level ids are unlocked (Level 1's id is always force-unlocked, persisted immediately on boot — see the hub note below), and which one was last played (tracked, not currently used to skip the hub). |
| `hh_<levelId>_bestscore` | integer | Best score ever reached on that level, saved incrementally as it's beaten (lane advances, collectibles), not only on a full win. |
| `hh_<levelId>_besttime` | integer (ms) | Fastest completion time on that level (only set on an actual win). |

Saving progress means: completing a level immediately unlocks and persists the next one, and your
best score/time per level persists whether or not you actually finish that particular run. It does
**not** mean mid-level checkpointing — every level run always starts fresh from row 0, by design
(rule 1: deterministic, replayable, no partial/resumed state to keep in sync).

## The progress hub

The hub (`hub.js`) is the game's home screen and the actual point of the whole save system: **the
real game is maximizing your score on each level**, not just reaching the end once — the hub is
where that becomes visible and actionable. It shows every level as a tile (name, lock state, best
score, best time) plus one trailing "coming soon" placeholder tile for whichever level doesn't
exist yet, reading everything straight from `localStorage` — it has no dependency on a live engine
instance. Tapping an unlocked tile starts that level immediately (skips the engine's own START
banner, since tapping the tile already *is* the "play" action); tapping a locked tile does nothing.

Completing a level, or dying out of one, **always returns to the hub** rather than auto-advancing
to whatever's next — the hub is where the player chooses: replay an already-beaten level to
improve their score, or move on to a newly-unlocked one. The one exception is dying mid-level:
`GAMEOVER`'s primary tap retries the same level directly (no hub round-trip for "let me try
again"), but a small persistent "🗺 Map" link (bottom-left, shown whenever a level is active) is
always available to bail out to the hub instead.

### Coexistence architecture

The hub and the engine (`HedgehoggerGame`) are two independent objects that both exist for the
whole page lifetime and share one canvas, each with its own `active` flag gating both its own
`requestAnimationFrame` work and its own input handlers — only one is ever actually doing anything.
`main.js` is the thin orchestrator: it owns the single window `resize` listener (dispatching to
whichever of the two is currently active — letting both listen independently would mean they fight
over sizing the canvas) and wires the hub's `onSelect` callback to `game.startLevel(id)` and the
engine's `onExit` option back to showing the hub. `HedgehoggerGame.setLevel()` sets `active = true`
as a side effect (so existing direct-manipulation test scripts that call it without going through
the hub keep working unmodified); `deactivate()` sets it back to `false`.

One real bug from building this: the engine's constructor computes Level 1's forced-unlock
in-memory, but used to only *persist* that to `localStorage` inside `setLevel()` — harmless when
a level always auto-loaded on boot, but once the hub became the entry point, `setLevel()` might
never run before the player's first tap, so the hub would read an empty `unlocked` list and treat
even Level 1 as locked. Fixed by persisting that initial unlock immediately in the constructor,
not deferred to whenever a level happens to load.

## Mechanic ledger

Tracks what each level introduces, so pacing decisions are visible at a glance.

| Level | New mechanic(s) | Reused/recombined |
|---|---|---|
| 1 — Garden Crossing | Road (mower/cat traffic), river (logs), telegraphed sprinkler hazard, goal burrows, apples/beetles | — (this is the tutorial level) |
| 2 — Rain Garden | The scrolling camera / extended length itself — a level too long to see all at once, so the player has to plan ahead without seeing the whole board | Longer arrangement of the same road/river/sprinkler vocabulary from Level 1, no new hazard *type* |
| 3 — Midnight Prowl | The prowling cat (see spec below) | Same terrain as Level 2, lane for lane — the only difference is the cat; roll gains a second meaning (see below) |

Rule 2, applied here: scrolling and "the prowling cat" are each their own new thing, so they get
their own levels rather than landing together — Level 2 is deliberately just "the existing
vocabulary, but longer, and you can't see it all at once," with zero new hazard types. Level 3
then goes further: it doesn't even introduce new *terrain*, only the cat, isolating the one new
mechanic as cleanly as possible.

### Mechanic spec: the prowling cat (Level 3)

The forcing hazard is a single chaser cat, climbing the garden from below (`level.chaser` on the
level data; engine state lives in `this.chaser`, see `engine.js`'s chaser block in `update()`).
Design goals: it must still satisfy rule 3 (telegraphed, never a surprise) and rule 1
(deterministic — see the note below on what that means for a reactive hazard).

- **Vertical position** (`chaser.row`, world-space, float) only climbs while the player has gone
  `idleGrace` seconds **without moving at all** — any move, forward, lateral, or even backward,
  resets that idle clock back to zero. This is not an incidental detail, it's the load-bearing
  design decision: the first implementation climbed on *absolute elapsed time since the level
  started* instead, and it was a real, serious bug — since the chaser's row is clamped to never
  exceed the player's own row, and a fresh run starts with both at row 0 by definition, an
  absolute-time model read as an immediate, unavoidable catch on the very first frame for any
  player who took more than an instant to make their first move. No real human reacts within one
  rendered frame of tapping "play." It only went undetected by the "informed dodge" bot because
  that bot reacts inside the same JS tick — faster than any human ever could — so it always beat
  the race before the bug could fire. It was caught by testing hesitation directly (stepping the
  engine's own `update()` in a loop with the player deliberately left stationary), not by playing
  the game via a bot. Idle-time-based climbing fixes this categorically: it gives a fresh grace
  period after *every* legitimate pause (reading the intro text, sidestepping while timing a
  sprinkler), not just once at t=0, and it's the only signal that actually means "hasn't moved in
  a while" as opposed to "the level is long."
- **Horizontal position** (`chaser.col`, float) eases toward the player's current column at a
  capped rate (`turnSpeed`, columns/sec) — it's always visibly hunting your column, but can never
  teleport onto you.
- **Telegraph ("stalking")**: once idle time exceeds `idleGrace` *and* the chaser's row closes to
  within `prowlRange` of the player, its sprite switches to a crouched, tail-flicking stalking
  pose (flattened ears, narrowed glowing eyes) — a clear warning before it's actually dangerous.
- **Pounce/catch**: only once idle time exceeds `idleGrace`, the row gap is within `catchRange`
  (smaller than `prowlRange`), the column matches, and the hop has landed, does it actually catch
  you — same landing-grace rule as every other hazard, so there's no mid-air surprise.
- **The cat can only ever be behind/below the player**, never ahead — it approaches from a fixed,
  known direction and is always on screen before it's a threat.
- **Rolling dodges it.** A hedgehog's actual real-world defense against a predator is rolling
  into a spiky ball — so the existing roll move (until now, purely a "cross hazards faster and
  briefly invulnerable" tool) becomes narratively double-purposed: it's now explicitly how you
  shrug off the cat's pounce too. No new input, no new rule — a payoff for reusing rule 2
  ("recombine an existing mechanic in a new way") in the most literal sense.

Level 3's shipped tuning: `idleGrace: 2.5`s, `climbRate: 0.5` rows/sec, `turnSpeed: 2.5`,
`prowlRange: 2.5`, `catchRange: 0.6`. Verified directly (stepping the engine, not just bot play):
any single pause under 2.5s is always completely safe regardless of when it happens; a genuinely
stationary player is eventually caught, but only after a real, sustained idle stretch (tuned
deliberately generous for a third level — "adds jeopardy, doesn't difficulty-spike"); catchRange,
rolling, and mid-air immunity all verified at their exact boundaries; the full 20-trial informed-
dodge sweep passes 20/20 on all three levels with this tuning.

A note on "deterministic" for a reactive hazard like this: rule 1 means *no `Math.random()`
anywhere in gameplay-relevant state* — most other hazards in this game are a pure function of
`playAge` alone, but the cat is deliberately also a function of the player's own move history
(specifically, how long since their last move). That's still fully deterministic (replay the same
inputs at the same times and you get the identical run); it's the whole point of a *chaser*. It
just means fairness testing for this mechanic can't rely purely on a fast bot (see above) — it
needs the engine's own state stepped directly, with the player deliberately left idle, to catch
the class of bug an instant-reacting bot can never trigger.

## Open design questions

- none currently — see "Progress hub" above for the level-select screen, now built.
