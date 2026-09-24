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
  index.html        — page shell, loads js/main.js as an ES module
  game.css           — layout/letterboxing only; all game visuals are Canvas
  js/
    main.js          — entry point: builds the level roster, boots the engine
    engine.js         — state machine, physics, collision, camera, save data, rendering orchestration
    sprites.js         — every visual: layered/shaded Canvas 2D vector art, zero image assets
    level1.js, level2.js, ...  — one file per level, pure declarative data
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
| `hh_progress` | `{ unlocked: string[], lastPlayed: string \| null }` | Which level ids are unlocked, and which one to resume into on next visit. Level 1's id is always force-unlocked. |
| `hh_<levelId>_bestscore` | integer | Best score ever reached on that level. |
| `hh_<levelId>_besttime` | integer (ms) | Fastest completion time on that level (only set on an actual win). |

"Automatically save progress" means: closing the tab and coming back resumes on whichever level
you last played (`lastPlayed`), and completing a level immediately unlocks and persists the next
one. It does **not** mean mid-level checkpointing — every level run always starts fresh from row
0, by design (rule 1: deterministic, replayable, no partial/resumed state to keep in sync).

## Mechanic ledger

Tracks what each level introduces, so pacing decisions are visible at a glance.

| Level | New mechanic(s) | Reused/recombined |
|---|---|---|
| 1 — Garden Crossing | Road (mower/cat traffic), river (logs), telegraphed sprinkler hazard, goal burrows, apples/beetles | — (this is the tutorial level) |
| 2 — Rain Garden (working title) | The scrolling camera / extended length itself — a level too long to see all at once, so the player has to plan ahead without seeing the whole board | Longer arrangement of the same road/river/sprinkler vocabulary from Level 1, no new hazard *type* |
| 3 — *TBD* | The prowling cat (see spec below) | Roll gains a second meaning (see below); builds on a longer/scrolling level now that that's established |

Rule 2, applied here: scrolling and "the prowling cat" are each their own new thing, so they get
their own levels rather than landing together — Level 2 is deliberately just "the existing
vocabulary, but longer, and you can't see it all at once," with zero new hazard types.

### Mechanic spec: the prowling cat (Level 3)

The forcing hazard is a single chaser cat, climbing the garden from below. Design goals: it must
still satisfy rule 3 (telegraphed, never a surprise) and rule 1 (deterministic — see the note on
what "deterministic" means for a reactive hazard, below).

- **Vertical position** (`frontierRow`, world-space, float): rises at a constant rate
  (`catClimbRate`, rows/sec) starting `catStartDelay` seconds after the run begins. This part is
  a pure function of elapsed time — nothing the player does speeds it up or slows it down. It's
  the same idea as the "rising flood" alternative we didn't pick, just re-skinned as a stalking
  cat instead of a rising water line, and it's what actually creates the "keep moving or it
  catches up" pressure.
- **Horizontal position** (`catCol`, float): eases toward the player's current column at a capped
  rate (`catTurnSpeed`, columns/sec) — it's always visibly hunting your column, but can never
  teleport onto you.
- **Telegraph ("stalking")**: once `frontierRow` closes to within `PROWL_RANGE` rows of the
  player, the cat's sprite switches to a crouched, tail-flicking stalking pose — a clear warning
  the player is in range, well before it's actually dangerous.
- **Pounce / catch**: only once `frontierRow` is within `CATCH_RANGE` (smaller than
  `PROWL_RANGE`) *and* the cat's column matches the player's, on a landed hop, does it actually
  catch you — same landing-grace rule as every other hazard, so there's no mid-air surprise.
- **The cat can only ever be behind/below the player**, never ahead — it approaches from a fixed,
  known direction and is always on screen before it's a threat. That's the fairness guarantee,
  structurally identical to why the rising-flood alternative would have been fair too.
- **Rolling dodges it.** A hedgehog's actual real-world defense against a predator is rolling
  into a spiky ball — so the existing roll move (until now, purely a "cross hazards faster and
  briefly invulnerable" tool) becomes narratively double-purposed: it's now explicitly how you
  shrug off the cat's pounce, not just a speed move. No new input, no new rule — just a nice
  payoff for reusing rule 2 ("recombine an existing mechanic in a new way") in the most literal
  sense.

A note on "deterministic" for a reactive hazard like this: rule 1 means *no `Math.random()`
anywhere in gameplay-relevant state* — every other hazard in this game is a pure function of
`playAge` alone, but the cat is deliberately also a function of the player's own position
history. That's still fully deterministic (replay the same inputs at the same times and you get
the identical run) and it's the whole point of a *chaser* — it just means fairness testing for
this level needs the "informed dodge" bot to actually dodge the cat, not just static hazards.

## Open design questions

- **Level select / menu.** With only two levels there's no dedicated level-select screen yet —
  the engine just resumes `lastPlayed`. Revisit once there are enough levels that jumping
  straight back into an earlier one matters.
- **Tuning `catClimbRate`/`catStartDelay`/`PROWL_RANGE`/`CATCH_RANGE`.** These need actual
  playtesting/bot-sweep numbers once built — treat the first implementation as a draft to tune,
  not final.
