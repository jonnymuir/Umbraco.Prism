import { execFileSync, spawn } from 'node:child_process';
import type { Page } from '@playwright/test';

// A recording-friendly terminal surface: the real session lives in tmux (on its own socket, so
// its environment and lifecycle are fully ours), input goes in via `tmux send-keys`, and the
// recorded page renders a styled mirror of `tmux capture-pane` output as plain DOM.
//
// This replaces driving ttyd/xterm.js in the recorded browser, which failed in ways that were
// invisible to assertions and fatal to footage: the font-size hook produced a terminal whose
// grid was computed at one font metric and painted at another (tiny text in a corner of the
// screen, a huge unpainted grey band below it), and the canvas visibly froze for minutes at a
// time mid-recording while the underlying session kept working. A DOM mirror fed from
// capture-pane cannot desync from the real session, needs no focus, renders at exactly the
// font size we choose, and has no canvas to freeze.

const SOCKET = 'prism-demo';
const SESSION = 'prism-demo-terminal';

// 150×36 at 20px/27px Menlo fills a 1920×1080 frame (minus title bar and padding) almost
// exactly — chosen together with the CSS in installMirrorChrome below.
export const TERMINAL_COLS = 150;
export const TERMINAL_ROWS = 36;

function tmux(...args: string[]): string {
  // stderr piped (not inherited) so an expected first-call failure like "no server running"
  // doesn't leak noise into the test reporter's output.
  return execFileSync('tmux', ['-L', SOCKET, ...args], { encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] });
}

/**
 * Kills any previous demo terminal server and starts a fresh session of exactly
 * TERMINAL_COLS×TERMINAL_ROWS, wrapping `script` so everything (including a later claude
 * launch and its one-time consent gate) is captured to `logPath` from the very first command.
 * CLAUDECODE/CLAUDE_CODE_* env vars are stripped so a claude launched inside is a genuinely
 * independent process, not a child session of the recording orchestrator.
 */
export function startDemoTerminalSession(logPath: string, cwd: string): void {
  try {
    tmux('kill-server');
  } catch {
    // No server running — nothing to kill.
  }
  const strippedEnv = Object.fromEntries(
    Object.entries(process.env).filter(
      ([key]) => !/^(CLAUDECODE|CLAUDE_CODE_|AI_AGENT|CLAUDE_EFFORT)/.test(key)
    )
  ) as NodeJS.ProcessEnv;
  const child = spawn(
    'tmux',
    [
      '-L', SOCKET,
      'new-session', '-d', '-s', SESSION,
      '-x', String(TERMINAL_COLS), '-y', String(TERMINAL_ROWS),
      '-c', cwd,
      'script', '-q', '-F', logPath, 'bash'
    ],
    { env: strippedEnv, stdio: 'ignore' }
  );
  child.unref();
  // The session was created with the status bar counted inside the -y height; turning it off
  // gives the pane the full TERMINAL_ROWS. Retry briefly — the server may still be starting.
  const deadline = Date.now() + 5_000;
  for (;;) {
    try {
      tmux('set-option', '-g', 'status', 'off');
      break;
    } catch {
      if (Date.now() > deadline) throw new Error('tmux demo server did not start in time');
    }
  }
}

/** Sends literal text to the session, one character at a time, at a human-looking pace. */
export async function sendTerminalText(text: string, delayMs = 8): Promise<void> {
  for (const ch of text) {
    // tmux parses a lone ";" argument as its own command separator BEFORE send-keys sees it —
    // even after "--" and even with -l — so a bare semicolon is silently swallowed. This cost a
    // full recorded take: a `remove ...; add ...` command line lost its ";", collapsed into one
    // broken command whose errors were hidden by 2>/dev/null, and the MCP registration it was
    // supposed to perform never happened. "\;" is tmux's documented escape for a literal one.
    tmux('send-keys', '-t', SESSION, '-l', '--', ch === ';' ? '\\;' : ch);
    await new Promise(resolve => setTimeout(resolve, delayMs));
  }
}

/** Sends a named key (Enter, Escape, C-c, ...) to the session. */
export function sendTerminalKey(key: string): void {
  tmux('send-keys', '-t', SESSION, key);
}

/** The visible pane content, with SGR colour escapes preserved for the mirror to render. */
export function captureTerminal(): string {
  return tmux('capture-pane', '-p', '-e', '-t', SESSION);
}

function cursorPosition(): { x: number; y: number; visible: boolean } {
  try {
    const raw = tmux('display-message', '-p', '-t', SESSION, '#{cursor_x} #{cursor_y} #{cursor_flag}').trim();
    const [x, y, flag] = raw.split(/\s+/).map(Number);
    return { x: x || 0, y: y || 0, visible: flag !== 0 };
  } catch {
    return { x: 0, y: 0, visible: false };
  }
}

// ------------------------------------------------------------------ ANSI → HTML

// GitHub-dark-flavoured 16-colour palette — legible on the mirror's near-black background.
const BASE_COLORS = [
  '#21262d', '#f47067', '#57ab5a', '#c69026', '#539bf5', '#b083f0', '#39c5cf', '#adbac7',
  '#545d68', '#ff938a', '#6bc46d', '#daaa3f', '#6cb6ff', '#dcbdfb', '#56d4dd', '#cdd9e5'
];

function xterm256(n: number): string {
  if (n < 16) return BASE_COLORS[n];
  if (n < 232) {
    const idx = n - 16;
    const steps = [0, 95, 135, 175, 215, 255];
    const r = steps[Math.floor(idx / 36)];
    const g = steps[Math.floor(idx / 6) % 6];
    const b = steps[idx % 6];
    return `rgb(${r},${g},${b})`;
  }
  const v = 8 + (n - 232) * 10;
  return `rgb(${v},${v},${v})`;
}

interface SgrState {
  bold: boolean;
  dim: boolean;
  italic: boolean;
  underline: boolean;
  reverse: boolean;
  fg: string | null;
  bg: string | null;
}

function escapeHtml(text: string): string {
  return text.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
}

function styleFor(state: SgrState): string {
  let fg = state.fg;
  let bg = state.bg;
  if (state.reverse) {
    [fg, bg] = [bg ?? '#e6edf3', fg ?? '#101418'];
  }
  const parts: string[] = [];
  if (fg) parts.push(`color:${fg}`);
  if (bg) parts.push(`background:${bg}`);
  if (state.bold) parts.push('font-weight:600');
  if (state.dim) parts.push('opacity:.62');
  if (state.italic) parts.push('font-style:italic');
  if (state.underline) parts.push('text-decoration:underline');
  return parts.join(';');
}

function applySgr(state: SgrState, params: number[]): void {
  for (let i = 0; i < params.length; i++) {
    const p = params[i];
    if (p === 0) Object.assign(state, { bold: false, dim: false, italic: false, underline: false, reverse: false, fg: null, bg: null });
    else if (p === 1) state.bold = true;
    else if (p === 2) state.dim = true;
    else if (p === 3) state.italic = true;
    else if (p === 4) state.underline = true;
    else if (p === 7) state.reverse = true;
    else if (p === 22) { state.bold = false; state.dim = false; }
    else if (p === 23) state.italic = false;
    else if (p === 24) state.underline = false;
    else if (p === 27) state.reverse = false;
    else if (p >= 30 && p <= 37) state.fg = BASE_COLORS[p - 30 + (state.bold ? 8 : 0)];
    else if (p === 38 && params[i + 1] === 5) { state.fg = xterm256(params[i + 2] ?? 0); i += 2; }
    else if (p === 38 && params[i + 1] === 2) { state.fg = `rgb(${params[i + 2] ?? 0},${params[i + 3] ?? 0},${params[i + 4] ?? 0})`; i += 4; }
    else if (p === 39) state.fg = null;
    else if (p >= 40 && p <= 47) state.bg = BASE_COLORS[p - 40];
    else if (p === 48 && params[i + 1] === 5) { state.bg = xterm256(params[i + 2] ?? 0); i += 2; }
    else if (p === 48 && params[i + 1] === 2) { state.bg = `rgb(${params[i + 2] ?? 0},${params[i + 3] ?? 0},${params[i + 4] ?? 0})`; i += 4; }
    else if (p === 49) state.bg = null;
    else if (p >= 90 && p <= 97) state.fg = BASE_COLORS[p - 90 + 8];
    else if (p >= 100 && p <= 107) state.bg = BASE_COLORS[p - 100 + 8];
  }
}

/** Converts capture-pane -e output (text + SGR escapes only) into styled HTML. */
export function ansiToHtml(raw: string): string {
  // capture-pane -e emits SGR only, but strip any other stray escapes defensively.
  const cleaned = raw
    .replace(/\x1b\][^\x07\x1b]*(?:\x07|\x1b\\)/g, '')
    .replace(/\x1b\[[0-9;?]*[A-LN-Za-ln-z]/g, '');
  const state: SgrState = { bold: false, dim: false, italic: false, underline: false, reverse: false, fg: null, bg: null };
  let html = '';
  let last = 0;
  const sgr = /\x1b\[([0-9;]*)m/g;
  const emit = (text: string) => {
    if (!text) return;
    const style = styleFor(state);
    html += style ? `<span style="${style}">${escapeHtml(text)}</span>` : escapeHtml(text);
  };
  for (let match = sgr.exec(cleaned); match; match = sgr.exec(cleaned)) {
    emit(cleaned.slice(last, match.index));
    applySgr(state, match[1].split(';').filter(Boolean).map(Number));
    last = match.index + match[0].length;
  }
  emit(cleaned.slice(last));
  return html;
}

// ------------------------------------------------------------------ mirror lifecycle

let mirrorTimer: ReturnType<typeof setInterval> | null = null;
let lastFrame = '';

async function installMirrorChrome(page: Page): Promise<void> {
  await page.evaluate(({ cols }) => {
    if (document.getElementById('demo-terminal')) return;
    document.body.style.margin = '0';
    document.body.style.background = '#0b0e13';
    const root = document.createElement('div');
    root.id = 'demo-terminal';
    Object.assign(root.style, {
      display: 'flex', flexDirection: 'column', height: '100vh', overflow: 'hidden',
      background: '#101418'
    } satisfies Partial<CSSStyleDeclaration>);

    const bar = document.createElement('div');
    Object.assign(bar.style, {
      display: 'flex', alignItems: 'center', gap: '8px', padding: '0 18px', height: '44px',
      background: '#1a2029', flex: '0 0 auto',
      font: '500 15px/1 -apple-system, "Segoe UI", system-ui, sans-serif', color: '#768390'
    } satisfies Partial<CSSStyleDeclaration>);
    for (const color of ['#f47067', '#c69026', '#57ab5a']) {
      const light = document.createElement('span');
      Object.assign(light.style, {
        width: '13px', height: '13px', borderRadius: '50%', background: color, flex: '0 0 auto'
      } satisfies Partial<CSSStyleDeclaration>);
      bar.appendChild(light);
    }
    const title = document.createElement('span');
    title.textContent = `bash — ${cols} cols`;
    title.style.marginLeft = '12px';
    bar.appendChild(title);
    root.appendChild(bar);

    const wrapper = document.createElement('div');
    Object.assign(wrapper.style, {
      position: 'relative', flex: '1 1 auto', padding: '20px 28px', overflow: 'hidden'
    } satisfies Partial<CSSStyleDeclaration>);
    const content = document.createElement('pre');
    content.id = 'demo-terminal-content';
    Object.assign(content.style, {
      margin: '0',
      font: '400 20px/27px "SF Mono", ui-monospace, Menlo, Consolas, monospace',
      color: '#e6edf3', whiteSpace: 'pre', position: 'relative'
    } satisfies Partial<CSSStyleDeclaration>);
    const cursor = document.createElement('div');
    cursor.id = 'demo-terminal-cursor';
    Object.assign(cursor.style, {
      position: 'absolute', width: '1ch', height: '27px',
      background: 'rgba(230, 237, 243, 0.45)', borderRadius: '2px',
      font: '400 20px/27px "SF Mono", ui-monospace, Menlo, Consolas, monospace',
      pointerEvents: 'none', transition: 'left 60ms linear, top 60ms linear'
    } satisfies Partial<CSSStyleDeclaration>);
    wrapper.appendChild(content);
    wrapper.appendChild(cursor);
    root.appendChild(wrapper);
    document.body.appendChild(root);
  }, { cols: TERMINAL_COLS });
}

/**
 * Navigates the recorded page to the terminal mirror (installing it if needed) and starts the
 * capture-pane poll. Idempotent — safe to call again in a later act on the same page.
 */
export async function showTerminalMirror(page: Page): Promise<void> {
  if (!page.url().startsWith('about:blank')) {
    await page.goto('about:blank');
  }
  await installMirrorChrome(page);
  lastFrame = '';
  if (mirrorTimer) return;
  let busy = false;
  mirrorTimer = setInterval(() => {
    if (busy) return;
    busy = true;
    void (async () => {
      try {
        const raw = captureTerminal();
        const cursor = cursorPosition();
        const frameKey = `${raw}@${cursor.x},${cursor.y}`;
        if (frameKey !== lastFrame) {
          lastFrame = frameKey;
          const html = ansiToHtml(raw.replace(/\n$/, ''));
          await page.evaluate(({ html, cursor }) => {
            const content = document.getElementById('demo-terminal-content');
            const cursorEl = document.getElementById('demo-terminal-cursor');
            if (!content || !cursorEl) return;
            content.innerHTML = html;
            cursorEl.style.display = cursor.visible ? 'block' : 'none';
            cursorEl.style.left = `calc(28px + ${cursor.x}ch)`;
            cursorEl.style.top = `${20 + cursor.y * 27}px`;
          }, { html, cursor });
        }
      } catch {
        // Page navigating or capture hiccup — the next tick will catch up.
      }
      busy = false;
    })();
  }, 300);
}

/** Stops the mirror poll — call when the recording is done with the terminal for good. */
export function stopTerminalMirror(): void {
  if (mirrorTimer) {
    clearInterval(mirrorTimer);
    mirrorTimer = null;
  }
}
