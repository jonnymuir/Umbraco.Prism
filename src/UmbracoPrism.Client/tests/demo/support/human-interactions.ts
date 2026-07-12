import type { Locator, Page } from '@playwright/test';

// Headless/CI Playwright has no visible pointer, and locator.fill()/click() teleport instantly —
// fine for assertions, unreadable for an audience watching a recording. These helpers make the
// mouse visibly travel to what it's about to do, and make on-screen typing happen keystroke by
// keystroke, so a viewer (or a presenter narrating live) can actually track what's happening.

// Position is tracked per-page across calls so each new move animates from wherever the cursor
// visually last was, rather than teleporting in from an unknown previous spot.
const lastPosition = new WeakMap<Page, { x: number; y: number }>();

async function ensureCursor(page: Page): Promise<void> {
  await page.evaluate(() => {
    if (document.getElementById('demo-cursor')) return;
    const cursor = document.createElement('div');
    cursor.id = 'demo-cursor';
    Object.assign(cursor.style, {
      position: 'fixed',
      left: '0px',
      top: '0px',
      width: '22px',
      height: '22px',
      marginLeft: '-11px',
      marginTop: '-11px',
      borderRadius: '50%',
      background: 'rgba(250, 204, 21, 0.35)',
      border: '2px solid rgba(250, 204, 21, 0.9)',
      boxShadow: '0 0 0 2px rgba(0,0,0,0.25)',
      zIndex: '2147483647',
      pointerEvents: 'none',
      transition: 'left 40ms linear, top 40ms linear',
      display: 'none'
    } satisfies Partial<CSSStyleDeclaration>);
    document.body.appendChild(cursor);
  });
}

async function setCursorPosition(page: Page, x: number, y: number): Promise<void> {
  await page.evaluate(
    ({ x, y }) => {
      const cursor = document.getElementById('demo-cursor');
      if (!cursor) return;
      cursor.style.display = 'block';
      cursor.style.left = `${x}px`;
      cursor.style.top = `${y}px`;
    },
    { x, y }
  );
}

/** Briefly grow + flash the cursor ring so a click reads clearly on screen, then settle back. */
async function pulseCursor(page: Page): Promise<void> {
  await page.evaluate(() => {
    const cursor = document.getElementById('demo-cursor');
    if (!cursor) return;
    cursor.style.transition = 'transform 120ms ease-out, left 40ms linear, top 40ms linear';
    cursor.style.transform = 'scale(1.6)';
    setTimeout(() => {
      cursor.style.transform = 'scale(1)';
    }, 130);
  });
}

/**
 * Animate the real OS/CDP pointer (so genuine hover states fire — Umbraco's row "+" affordance
 * only renders on :hover) and the visible overlay dot together, from the last known position to
 * the target, in small steps rather than one instant jump.
 */
export async function humanMoveTo(page: Page, x: number, y: number, steps = 18): Promise<void> {
  await ensureCursor(page);
  const from = lastPosition.get(page) ?? { x, y };
  for (let i = 1; i <= steps; i++) {
    const t = i / steps;
    const cx = from.x + (x - from.x) * t;
    const cy = from.y + (y - from.y) * t;
    await page.mouse.move(cx, cy);
    await setCursorPosition(page, cx, cy);
    await page.waitForTimeout(12);
  }
  lastPosition.set(page, { x, y });
}

/** Move the visible cursor to a locator's center, click it, and pulse the cursor on contact. */
export async function humanClick(page: Page, locator: Locator): Promise<void> {
  await locator.scrollIntoViewIfNeeded();
  const box = await locator.boundingBox();
  if (!box) {
    // Fall back to a plain click for anything we genuinely can't get a box for (e.g. hidden until
    // interacted with) — better than throwing away the whole demo take over a cosmetic flourish.
    await locator.click();
    return;
  }
  const x = box.x + box.width / 2;
  const y = box.y + box.height / 2;
  await humanMoveTo(page, x, y);
  await pulseCursor(page);
  await page.mouse.down();
  await page.waitForTimeout(60);
  await page.mouse.up();
}

/**
 * Move to the field, click into it, then type character-by-character with a slight human jitter
 * — realistic on-screen typing rather than fill()'s instant value-set.
 */
export async function humanType(
  page: Page,
  locator: Locator,
  text: string,
  opts: { delay?: number; jitter?: number } = {}
): Promise<void> {
  const { delay = 65, jitter = 35 } = opts;
  await humanClick(page, locator);
  // Some fields (e.g. the create-stage dialog's Title) start with real pre-filled text, not just
  // an empty placeholder — select-all + delete first so typing *replaces* it, the way a human
  // clearing a field before typing over it would, rather than pressSequentially inserting our
  // text into the middle of whatever was already there.
  await locator.press('ControlOrMeta+A');
  await locator.press('Backspace');
  for (const char of text) {
    await locator.pressSequentially(char, { delay: 0 });
    await page.waitForTimeout(delay + Math.round((Math.random() - 0.5) * jitter));
  }
}
