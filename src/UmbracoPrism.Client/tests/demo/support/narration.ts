import type { Page } from '@playwright/test';

// Professional lower-third + full-screen slate system for the demo recording. Replaces the old
// flash-caption (caption.ts): every beat is tagged (setup/intent/recap) so the story always reads
// "here's what we have -> here's what we'll do -> [it happens] -> here's what just happened",
// and hold time is computed from the text itself (word count / spoken pace) rather than a fixed
// 2.2s, since a one-clause caption and a two-sentence recap need very different amounts of time
// to actually be read aloud during a live talk.

export type BeatKind = 'setup' | 'intent' | 'recap' | 'note';

const BEAT_LABEL: Record<BeatKind, string> = {
  setup: 'WHAT WE HAVE',
  intent: "WHAT WE'RE ABOUT TO DO",
  recap: 'WHAT JUST HAPPENED',
  note: ''
};

const BEAT_ACCENT: Record<BeatKind, string> = {
  setup: '#7dd3fc',
  intent: '#facc15',
  recap: '#86efac',
  note: '#e5e7eb'
};

// ~2.6 words/sec is a comfortable, unhurried spoken pace for a room full of people — faster than
// that and a presenter reading the caption aloud is racing the fade-out.
const READING_MS_PER_WORD = 380;
const MIN_HOLD_MS = 3200;
// A caption that would need longer than this to read comfortably should be shortened, not held
// on screen longer — capping here keeps pacing tight instead of letting a long caption stall the
// whole take (per direct viewer feedback: ~5s is plenty to read a typical beat).
const MAX_HOLD_MS = 5000;

function computeHoldMs(text: string): number {
  const words = text.trim().split(/\s+/).filter(Boolean).length;
  return Math.min(MAX_HOLD_MS, Math.max(MIN_HOLD_MS, Math.round(words * READING_MS_PER_WORD)));
}

/**
 * Every beat/slate's real on-screen text and video-relative timestamp, recorded as a side effect
 * of the calls below — the source data a voiced-narration pass (recording v2) needs to know
 * exactly when to speak each line. `startNarrationTimeline()` must be called right after the
 * recorded page is created (video capture starts then), so `atMs` lines up with the actual output
 * file's timeline, not wall-clock time.
 */
export interface NarrationTimelineEntry {
  atMs: number;
  kind: string;
  text: string;
  holdMs: number;
}

let recordingStartedAt: number | null = null;
const timeline: NarrationTimelineEntry[] = [];

export function startNarrationTimeline(): void {
  recordingStartedAt = Date.now();
  timeline.length = 0;
}

export function getNarrationTimeline(): readonly NarrationTimelineEntry[] {
  return timeline;
}

function recordTimelineEntry(kind: string, text: string, holdMs: number): void {
  if (recordingStartedAt === null) {
    return;
  }
  timeline.push({ atMs: Date.now() - recordingStartedAt, kind, text, holdMs });
}

/**
 * Several beats in this recording fire immediately after a real navigation (a nav-link click, a
 * form submit) — `waitForLoadState('networkidle')` doesn't fully rule out a trailing redirect
 * still tearing down the document, which kills `page.evaluate`'s execution context mid-call
 * ("Execution context was destroyed"). Retry once after a short settle rather than failing the
 * whole act over what's actually just late-arriving navigation, not a real problem with the page.
 */
async function evaluateResilient<Args>(page: Page, fn: (args: Args) => void, args: Args): Promise<void> {
  for (let attempt = 0; ; attempt++) {
    try {
      // Playwright's own PageFunction typing maps Args through an internal Unboxed<> transform
      // that a generic passthrough like this can't unify against structurally — every real call
      // site below still passes a concretely-typed callback/args pair, so this cast is local to
      // the wrapper, not a loss of type safety for callers.
      await page.evaluate(fn as Parameters<Page['evaluate']>[0], args);
      return;
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      if (attempt >= 2 || !/execution context|context was destroyed/i.test(message)) {
        throw error;
      }
      await page.waitForTimeout(300);
    }
  }
}

export type NarrationPosition = 'top' | 'bottom';

// Both resolve to a `top` value so the move between them is a genuine animatable CSS transition,
// not a bottom/top swap through `auto` (which can't interpolate). ~160px clears the bar's own
// height plus a bottom margin at the sizes this renders at.
const POSITION_TOP: Record<NarrationPosition, string> = {
  top: '5%',
  bottom: 'calc(100% - 160px)'
};

/**
 * Show one narration beat as a lower-third (or upper-third) bar and hold for a reading-paced
 * duration (or an explicit override). Awaiting this call is the pacing primitive for the whole
 * recording — every beat's hold is real time the video will actually contain.
 *
 * `position` defaults to `'bottom'`. Pass `'top'` for any beat shown while the audience needs the
 * lower part of the screen clear — e.g. narrating over a CLI about to stream typed text where a
 * bottom-anchored bar would sit right on top of the prompt.
 */
export async function beat(
  page: Page,
  kind: BeatKind,
  text: string,
  opts: { holdMs?: number; position?: NarrationPosition } = {}
): Promise<void> {
  // A cross-origin redirect (e.g. a Keycloak sign-in) has been observed to leave the recorded
  // video frozen on a stale frame for tens of seconds afterward, even in headed mode — the
  // underlying automation keeps working correctly (assertions still pass), but the video capture
  // itself stalls, plausibly from losing real OS-level foreground focus. Every beat forcing focus
  // back is cheap insurance against that recurring anywhere in a take.
  await page.bringToFront().catch(() => {});
  const hold = opts.holdMs ?? computeHoldMs(text);
  const position = opts.position ?? 'bottom';
  recordTimelineEntry(kind, text, hold);
  await evaluateResilient(
    page,
    ({ text, label, accent, top }) => {
      const id = 'demo-narration';
      let bar = document.getElementById(id) as HTMLDivElement | null;
      if (!bar) {
        bar = document.createElement('div');
        bar.id = id;
        Object.assign(bar.style, {
          position: 'fixed',
          left: '50%',
          top,
          transform: 'translateX(-50%)',
          width: 'min(80%, 1200px)',
          background: 'rgba(15, 20, 30, 0.88)',
          color: '#ffffff',
          font: '500 27px/1.45 -apple-system, "Segoe UI", system-ui, sans-serif',
          padding: '20px 30px',
          borderRadius: '10px',
          zIndex: '2147483647',
          textAlign: 'left',
          boxShadow: '0 8px 30px rgba(0,0,0,0.35)',
          opacity: '0',
          transition: 'opacity 220ms ease, top 550ms cubic-bezier(0.4, 0, 0.2, 1)',
          pointerEvents: 'none'
        } satisfies Partial<CSSStyleDeclaration>);
        const labelEl = document.createElement('div');
        labelEl.id = `${id}-label`;
        Object.assign(labelEl.style, {
          font: '700 14px/1 -apple-system, "Segoe UI", system-ui, sans-serif',
          letterSpacing: '0.12em',
          marginBottom: '8px'
        } satisfies Partial<CSSStyleDeclaration>);
        const textEl = document.createElement('div');
        textEl.id = `${id}-text`;
        bar.appendChild(labelEl);
        bar.appendChild(textEl);
        document.body.appendChild(bar);
      } else {
        bar.style.top = top;
      }
      const labelEl = document.getElementById(`${id}-label`)!;
      const textEl = document.getElementById(`${id}-text`)!;
      labelEl.textContent = label;
      labelEl.style.color = accent;
      labelEl.style.display = label ? 'block' : 'none';
      textEl.textContent = text;
      requestAnimationFrame(() => {
        bar!.style.opacity = '1';
      });
    },
    { text, label: BEAT_LABEL[kind], accent: BEAT_ACCENT[kind], top: POSITION_TOP[position] }
  );
  await page.waitForTimeout(hold);
}

/**
 * Smoothly slide the narration bar between its top and bottom anchors without changing its text —
 * for moving a beat that's already on screen out of the way of something about to happen
 * underneath it (e.g. sliding up before a CLI starts streaming typed text at the bottom).
 */
export async function moveNarrationTo(page: Page, position: NarrationPosition, settleMs = 620): Promise<void> {
  await evaluateResilient(page, top => {
    const bar = document.getElementById('demo-narration');
    if (bar) bar.style.top = top;
  }, POSITION_TOP[position]);
  await page.waitForTimeout(settleMs);
}

/** Fade the lower-third out. Call before a moment that needs the full screen (e.g. a reveal). */
export async function clearBeat(page: Page): Promise<void> {
  await evaluateResilient(page, () => {
    const bar = document.getElementById('demo-narration');
    if (bar) bar.style.opacity = '0';
  }, undefined);
  await page.waitForTimeout(260);
}

/**
 * Full-screen title slate — used for the cold open (introduce the whole premise before touching
 * any app) and the closing recap. Covers whatever's currently rendered underneath, so it works
 * identically on a blank about:blank page or mid-navigation.
 */
export async function showSlate(
  page: Page,
  opts: { eyebrow?: string; title: string; body: string; holdMs?: number }
): Promise<void> {
  const slateHold = opts.holdMs ?? computeHoldMs(`${opts.title} ${opts.body}`) + 1500;
  recordTimelineEntry('slate', `${opts.title}. ${opts.body}`, slateHold);
  await evaluateResilient(
    page,
    ({ eyebrow, title, body }) => {
      const id = 'demo-slate';
      document.getElementById(id)?.remove();
      const slate = document.createElement('div');
      slate.id = id;
      Object.assign(slate.style, {
        position: 'fixed',
        inset: '0',
        background: 'linear-gradient(160deg, #0b1220 0%, #111827 100%)',
        color: '#f8fafc',
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        justifyContent: 'center',
        textAlign: 'center',
        padding: '5vh 8vw',
        zIndex: '2147483647',
        opacity: '0',
        transition: 'opacity 320ms ease',
        font: '400 20px/1.6 -apple-system, "Segoe UI", system-ui, sans-serif'
      } satisfies Partial<CSSStyleDeclaration>);

      if (eyebrow) {
        const eyebrowEl = document.createElement('div');
        eyebrowEl.textContent = eyebrow;
        Object.assign(eyebrowEl.style, {
          font: '700 15px/1 -apple-system, "Segoe UI", system-ui, sans-serif',
          letterSpacing: '0.16em',
          color: '#7dd3fc',
          marginBottom: '18px'
        } satisfies Partial<CSSStyleDeclaration>);
        slate.appendChild(eyebrowEl);
      }

      const titleEl = document.createElement('div');
      titleEl.textContent = title;
      Object.assign(titleEl.style, {
        font: '700 44px/1.25 -apple-system, "Segoe UI", system-ui, sans-serif',
        maxWidth: '900px',
        marginBottom: '22px'
      } satisfies Partial<CSSStyleDeclaration>);
      slate.appendChild(titleEl);

      const bodyEl = document.createElement('div');
      bodyEl.textContent = body;
      Object.assign(bodyEl.style, {
        maxWidth: '820px',
        fontSize: '25px',
        // Was a dim slate-grey (#cbd5e1) — legible on a desktop monitor but too low-contrast on a
        // phone screen at normal brightness. Match the title's near-white instead.
        color: '#f4f6f8'
      } satisfies Partial<CSSStyleDeclaration>);
      slate.appendChild(bodyEl);

      document.body.appendChild(slate);
      requestAnimationFrame(() => {
        slate.style.opacity = '1';
      });
    },
    { eyebrow: opts.eyebrow, title: opts.title, body: opts.body }
  );
  await page.waitForTimeout(slateHold);
}

/** Fade the slate out and remove it, ready for the recording to move on to real content. */
export async function clearSlate(page: Page): Promise<void> {
  await evaluateResilient(page, () => {
    const slate = document.getElementById('demo-slate');
    if (slate) slate.style.opacity = '0';
  }, undefined);
  await page.waitForTimeout(340);
  await evaluateResilient(page, () => document.getElementById('demo-slate')?.remove(), undefined);
}
