/**
 * Hedgehogger — sprite drawing library.
 *
 * Every visual in the game is a hand-drawn Canvas 2D vector shape (gradients,
 * strokes, layered paths) — there are no image/SVG assets. Every function here
 * takes a rendering context plus plain numeric params and an `age` (seconds,
 * for idle/motion animation) — it never reads game state directly, so these
 * stay reusable across future levels.
 */

export const PALETTE = {
  grassA: '#2f6b3a',
  grassB: '#356f3f',
  grassShadow: '#1d4726',
  flowerColors: ['#ffd166', '#ef476f', '#f1faee', '#a3d977'],
  pebble: '#5b6b5f',
  road: '#24262b',
  roadFleck: '#34373d',
  roadLine: '#f1c40f',
  riverDeep: '#123047',
  riverMid: '#1b4965',
  riverRipple: '#8ecae6',
  riverSparkle: '#caf0f8',
  sprinklerTile: '#243b47',
  sprinklerTileLine: '#1a2c36',
};

function roundRectPath(ctx, x, y, w, h, r) {
  ctx.beginPath();
  ctx.roundRect(x, y, w, h, r);
}

function seededRng(seed) {
  let s = seed >>> 0;
  return () => {
    s = (s + 0x6d2b79f5) >>> 0;
    let t = s;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

// --- Lane backgrounds -------------------------------------------------

export function drawGrassLane(ctx, x, y, w, h, laneIndex, age) {
  ctx.fillStyle = laneIndex % 2 === 0 ? PALETTE.grassA : PALETTE.grassB;
  ctx.fillRect(x, y, w, h);

  ctx.strokeStyle = PALETTE.grassShadow;
  ctx.lineWidth = 3;
  ctx.beginPath();
  ctx.moveTo(x, y + h - 1.5);
  ctx.lineTo(x + w, y + h - 1.5);
  ctx.stroke();

  // Deterministic decoration: same rng seed every time this lane is drawn.
  const rng = seededRng(laneIndex * 977 + 13);
  const count = 6;
  for (let i = 0; i < count; i++) {
    const px = x + rng() * w;
    const py = y + 8 + rng() * (h - 16);
    const kind = rng();
    if (kind < 0.35) {
      // grass tuft
      ctx.strokeStyle = 'rgba(255,255,255,0.12)';
      ctx.lineWidth = 2;
      for (let b = -1; b <= 1; b++) {
        ctx.beginPath();
        ctx.moveTo(px + b * 3, py + 6);
        ctx.lineTo(px + b * 5, py - 6 + Math.sin(age * 1.4 + i) * 1.5);
        ctx.stroke();
      }
    } else if (kind < 0.6) {
      // flower
      const c = PALETTE.flowerColors[i % PALETTE.flowerColors.length];
      ctx.fillStyle = c;
      for (let p = 0; p < 5; p++) {
        const a = (p / 5) * Math.PI * 2;
        ctx.beginPath();
        ctx.arc(px + Math.cos(a) * 3.2, py + Math.sin(a) * 3.2, 2, 0, Math.PI * 2);
        ctx.fill();
      }
      ctx.fillStyle = '#fff8e1';
      ctx.beginPath();
      ctx.arc(px, py, 1.8, 0, Math.PI * 2);
      ctx.fill();
    } else {
      // pebble
      ctx.fillStyle = PALETTE.pebble;
      ctx.beginPath();
      ctx.ellipse(px, py, 3.5, 2.4, 0, 0, Math.PI * 2);
      ctx.fill();
    }
  }
}

export function drawRoadLane(ctx, x, y, w, h) {
  ctx.fillStyle = PALETTE.road;
  ctx.fillRect(x, y, w, h);

  const rng = seededRng(Math.floor(y) * 31 + 7);
  ctx.fillStyle = PALETTE.roadFleck;
  for (let i = 0; i < 14; i++) {
    ctx.fillRect(x + rng() * w, y + rng() * h, 2, 2);
  }

  ctx.strokeStyle = PALETTE.roadLine;
  ctx.lineWidth = 3;
  ctx.setLineDash([16, 14]);
  ctx.beginPath();
  ctx.moveTo(x, y + h / 2);
  ctx.lineTo(x + w, y + h / 2);
  ctx.stroke();
  ctx.setLineDash([]);
}

export function drawRiverLane(ctx, x, y, w, h, age) {
  const grad = ctx.createLinearGradient(0, y, 0, y + h);
  grad.addColorStop(0, PALETTE.riverMid);
  grad.addColorStop(1, PALETTE.riverDeep);
  ctx.fillStyle = grad;
  ctx.fillRect(x, y, w, h);

  ctx.fillStyle = PALETTE.riverRipple;
  ctx.globalAlpha = 0.55;
  const offset = (age * 26) % 34;
  for (let wx = -34 + offset; wx < w; wx += 34) {
    ctx.fillRect(x + wx, y + h * 0.28, 18, 2.5);
    ctx.fillRect(x + wx + 17, y + h * 0.68, 13, 2.5);
  }
  ctx.globalAlpha = 1;

  ctx.fillStyle = PALETTE.riverSparkle;
  ctx.globalAlpha = 0.5 + Math.sin(age * 3) * 0.2;
  ctx.fillRect(x + ((offset * 3) % w), y + h * 0.5, 3, 3);
  ctx.globalAlpha = 1;
}

export function drawSprinklerLane(ctx, x, y, w, h) {
  ctx.fillStyle = PALETTE.sprinklerTile;
  ctx.fillRect(x, y, w, h);
  ctx.strokeStyle = PALETTE.sprinklerTileLine;
  ctx.lineWidth = 1;
  for (let tx = x; tx < x + w; tx += 28) {
    ctx.beginPath();
    ctx.moveTo(tx, y);
    ctx.lineTo(tx, y + h);
    ctx.stroke();
  }
}

export function drawGoalLane(ctx, x, y, w, h, laneIndex, age) {
  drawGrassLane(ctx, x, y, w, h, laneIndex, age);
  ctx.fillStyle = 'rgba(255, 214, 102, 0.06)';
  ctx.fillRect(x, y, w, h);
}

// --- Obstacles ----------------------------------------------------------

export function drawLog(ctx, x, y, width, laneHeight, age) {
  const h = laneHeight * 0.66;
  ctx.save();
  ctx.translate(0, Math.sin(age * 2 + x * 0.01) * 1.5);

  const grad = ctx.createLinearGradient(0, y - h / 2, 0, y + h / 2);
  grad.addColorStop(0, '#8a5333');
  grad.addColorStop(1, '#5c371e');
  ctx.fillStyle = grad;
  roundRectPath(ctx, x, y - h / 2, width, h, h * 0.45);
  ctx.fill();
  ctx.strokeStyle = '#3f2414';
  ctx.lineWidth = 2;
  ctx.stroke();

  // End-grain rings
  for (const ex of [x + h * 0.45, x + width - h * 0.45]) {
    ctx.fillStyle = '#a9743f';
    ctx.beginPath();
    ctx.ellipse(ex, y, h * 0.42, h * 0.46, 0, 0, Math.PI * 2);
    ctx.fill();
    ctx.strokeStyle = '#6f4425';
    ctx.lineWidth = 1.5;
    for (const r of [0.28, 0.16]) {
      ctx.beginPath();
      ctx.ellipse(ex, y, h * 0.46 * r + 2, h * 0.5 * r + 2, 0, 0, Math.PI * 2);
      ctx.stroke();
    }
  }

  // Bark texture lines
  ctx.strokeStyle = 'rgba(0,0,0,0.18)';
  ctx.lineWidth = 1.5;
  for (let lx = x + h * 0.8; lx < x + width - h * 0.8; lx += 14) {
    ctx.beginPath();
    ctx.moveTo(lx, y - h * 0.3);
    ctx.lineTo(lx + 5, y + h * 0.3);
    ctx.stroke();
  }

  // Moss patches
  ctx.fillStyle = '#52b788';
  ctx.beginPath();
  ctx.arc(x + h + 6, y - h * 0.15, 5, 0, Math.PI * 2);
  ctx.arc(x + width - h - 10, y + h * 0.18, 4, 0, Math.PI * 2);
  ctx.fill();

  ctx.restore();
}

export function drawMower(ctx, x, y, width, age) {
  ctx.save();
  ctx.translate(x, y);

  ctx.fillStyle = 'rgba(0,0,0,0.25)';
  ctx.beginPath();
  ctx.ellipse(width / 2, 20, width * 0.5, 5, 0, 0, Math.PI * 2);
  ctx.fill();

  const body = ctx.createLinearGradient(0, -16, 0, 16);
  body.addColorStop(0, '#ff5a5f');
  body.addColorStop(1, '#b32330');
  ctx.fillStyle = body;
  roundRectPath(ctx, 0, -14, width, 28, 7);
  ctx.fill();
  ctx.strokeStyle = '#7a1620';
  ctx.lineWidth = 2;
  ctx.stroke();

  // Deck stripe
  ctx.fillStyle = 'rgba(255,255,255,0.25)';
  ctx.fillRect(6, -14, width - 12, 5);

  // Wheels
  ctx.fillStyle = '#161a1d';
  ctx.fillRect(3, -18, 9, 6);
  ctx.fillRect(width - 12, -18, 9, 6);
  ctx.fillRect(3, 12, 9, 6);
  ctx.fillRect(width - 12, 12, 9, 6);

  // Spinning blade hub
  ctx.save();
  ctx.translate(width / 2, 0);
  ctx.rotate(age * 22);
  const hub = ctx.createRadialGradient(0, 0, 1, 0, 0, 8);
  hub.addColorStop(0, '#ffffff');
  hub.addColorStop(1, '#9aa5ad');
  ctx.fillStyle = hub;
  ctx.beginPath();
  ctx.arc(0, 0, 8, 0, Math.PI * 2);
  ctx.fill();
  ctx.strokeStyle = '#5c6670';
  ctx.lineWidth = 2;
  for (let a = 0; a < Math.PI * 2; a += Math.PI / 2) {
    ctx.beginPath();
    ctx.moveTo(Math.cos(a) * 2, Math.sin(a) * 2);
    ctx.lineTo(Math.cos(a) * 8, Math.sin(a) * 8);
    ctx.stroke();
  }
  ctx.restore();

  // Headlamp
  ctx.fillStyle = '#fff3b0';
  ctx.beginPath();
  ctx.arc(width - 4, 0, 3, 0, Math.PI * 2);
  ctx.fill();

  ctx.restore();
}

export function drawCat(ctx, x, y, dir, age) {
  ctx.save();
  ctx.translate(x + 15, y);
  const bob = Math.sin(age * 9) * 1.4;
  ctx.translate(0, bob);

  ctx.fillStyle = 'rgba(0,0,0,0.25)';
  ctx.beginPath();
  ctx.ellipse(0, 16 - bob, 15, 4, 0, 0, Math.PI * 2);
  ctx.fill();

  // Tail
  ctx.strokeStyle = '#e0690b';
  ctx.lineWidth = 5;
  ctx.lineCap = 'round';
  ctx.beginPath();
  const tailBase = -dir * 13;
  ctx.moveTo(tailBase, 4);
  ctx.quadraticCurveTo(tailBase - dir * 14, -8 + Math.sin(age * 6) * 4, tailBase - dir * 8, -18);
  ctx.stroke();

  const body = ctx.createRadialGradient(-4, -4, 2, 0, 0, 16);
  body.addColorStop(0, '#ffab54');
  body.addColorStop(1, '#d46200');
  ctx.fillStyle = body;
  ctx.beginPath();
  ctx.arc(0, 0, 14, 0, Math.PI * 2);
  ctx.fill();
  ctx.strokeStyle = '#8a3f00';
  ctx.lineWidth = 1.5;
  ctx.stroke();

  // Tabby stripes
  ctx.strokeStyle = 'rgba(139, 69, 19, 0.5)';
  ctx.lineWidth = 2;
  for (const sx of [-6, 0, 6]) {
    ctx.beginPath();
    ctx.arc(sx, -2, 4, Math.PI * 1.1, Math.PI * 1.8);
    ctx.stroke();
  }

  // Ears
  ctx.fillStyle = '#d46200';
  for (const s of [-1, 1]) {
    ctx.beginPath();
    ctx.moveTo(s * 10, -8);
    ctx.lineTo(s * 4, -19);
    ctx.lineTo(s * 1, -8);
    ctx.fill();
    ctx.fillStyle = '#ffb4a2';
    ctx.beginPath();
    ctx.moveTo(s * 8, -9.5);
    ctx.lineTo(s * 4.5, -16);
    ctx.lineTo(s * 2, -9.5);
    ctx.fill();
    ctx.fillStyle = '#d46200';
  }

  // Face
  ctx.fillStyle = '#161a1d';
  ctx.beginPath();
  ctx.arc(dir * 4, -1, 1.6, 0, Math.PI * 2);
  ctx.fill();
  ctx.strokeStyle = 'rgba(20,20,20,0.55)';
  ctx.lineWidth = 1;
  for (const wy of [-1, 2]) {
    ctx.beginPath();
    ctx.moveTo(dir * 6, wy);
    ctx.lineTo(dir * 14, wy - 1);
    ctx.stroke();
  }

  ctx.restore();
}

// --- Sprinkler (state: 'idle' | 'charge' | 'burst') ---------------------

export function drawSprinkler(ctx, x, y, radius, state, phaseT) {
  if (state === 'burst') {
    const burstT = Math.min(1, phaseT * 4);
    const alpha = 0.5 * (1 - Math.max(0, phaseT - 0.6) / 0.4);
    const grad = ctx.createRadialGradient(x, y, radius * 0.1, x, y, radius);
    grad.addColorStop(0, `rgba(144, 224, 239, ${alpha})`);
    grad.addColorStop(1, `rgba(72, 202, 228, 0)`);
    ctx.fillStyle = grad;
    ctx.beginPath();
    ctx.arc(x, y, radius * Math.min(1, burstT + 0.4), 0, Math.PI * 2);
    ctx.fill();

    ctx.strokeStyle = `rgba(202, 240, 248, ${alpha})`;
    ctx.lineWidth = 2;
    for (let a = 0; a < Math.PI * 2; a += Math.PI / 6) {
      const jitter = Math.sin(phaseT * 40 + a * 5) * 4;
      ctx.beginPath();
      ctx.moveTo(x, y);
      ctx.lineTo(x + Math.cos(a) * (radius + jitter), y + Math.sin(a) * (radius + jitter));
      ctx.stroke();
    }
  } else if (state === 'charge') {
    // Unmistakable warning telegraph — this is the fairness mechanic that
    // gives a player a clear window to see danger coming before it's live.
    const pulse = 0.55 + Math.sin(phaseT * 28) * 0.35;
    ctx.strokeStyle = `rgba(255, 214, 10, ${Math.max(0.2, pulse)})`;
    ctx.lineWidth = 4;
    ctx.beginPath();
    ctx.arc(x, y, radius * 0.62, 0, Math.PI * 2);
    ctx.stroke();
    ctx.fillStyle = `rgba(255, 214, 10, ${Math.max(0.12, pulse * 0.32)})`;
    ctx.beginPath();
    ctx.arc(x, y, radius * 0.5, 0, Math.PI * 2);
    ctx.fill();

    ctx.font = '900 13px sans-serif';
    ctx.textAlign = 'center';
    ctx.fillStyle = `rgba(255, 214, 10, ${Math.max(0.4, pulse)})`;
    ctx.fillText('!', x, y - radius * 0.75);
  }

  // Base head (always drawn last, on top)
  const shake = state === 'charge' ? Math.sin(phaseT * 60) * 1.4 : 0;
  ctx.save();
  ctx.translate(x + shake, y);
  ctx.fillStyle = 'rgba(0,0,0,0.25)';
  ctx.beginPath();
  ctx.ellipse(0, 8, 8, 2.5, 0, 0, Math.PI * 2);
  ctx.fill();
  // Base plate + riser stem
  ctx.fillStyle = '#3a5a40';
  ctx.beginPath();
  ctx.ellipse(0, 6, 10, 3, 0, 0, Math.PI * 2);
  ctx.fill();
  ctx.fillStyle = '#8d99ae';
  ctx.fillRect(-2.5, -2, 5, 9);

  const headGrad = ctx.createRadialGradient(-2, -2, 1, 0, 0, 8);
  headGrad.addColorStop(0, '#ffd699');
  headGrad.addColorStop(1, '#c47f3a');
  ctx.fillStyle = headGrad;
  ctx.beginPath();
  ctx.arc(0, -3, 7, 0, Math.PI * 2);
  ctx.fill();
  ctx.strokeStyle = '#7a4a1e';
  ctx.lineWidth = 1.5;
  ctx.stroke();

  // Spray nozzle cross
  ctx.strokeStyle = '#4a2c10';
  ctx.lineWidth = 1.8;
  ctx.beginPath();
  ctx.moveTo(-6, -3); ctx.lineTo(6, -3);
  ctx.moveTo(0, -9); ctx.lineTo(0, 3);
  ctx.stroke();
  ctx.restore();
}

// --- Collectibles ---------------------------------------------------------

export function drawApple(ctx, x, y, age) {
  const bob = Math.sin(age * 2.4 + x * 0.05) * 2;
  ctx.save();
  ctx.translate(x, y + bob);

  ctx.fillStyle = 'rgba(0,0,0,0.2)';
  ctx.beginPath();
  ctx.ellipse(0, 12 - bob, 8, 2.5, 0, 0, Math.PI * 2);
  ctx.fill();

  const grad = ctx.createRadialGradient(-3, -4, 1, 0, 0, 10);
  grad.addColorStop(0, '#ff8a7a');
  grad.addColorStop(0.5, '#e63946');
  grad.addColorStop(1, '#b3212e');
  ctx.fillStyle = grad;
  ctx.beginPath();
  ctx.arc(0, 0, 10, 0, Math.PI * 2);
  ctx.fill();
  ctx.strokeStyle = '#7a121c';
  ctx.lineWidth = 1;
  ctx.stroke();

  ctx.fillStyle = 'rgba(255,255,255,0.55)';
  ctx.beginPath();
  ctx.ellipse(-3.5, -3.5, 2.2, 3.4, -0.5, 0, Math.PI * 2);
  ctx.fill();

  ctx.strokeStyle = '#5c3a21';
  ctx.lineWidth = 1.5;
  ctx.beginPath();
  ctx.moveTo(0, -10);
  ctx.lineTo(1, -14);
  ctx.stroke();

  ctx.fillStyle = '#2a9d8f';
  ctx.beginPath();
  ctx.ellipse(3, -13, 4, 2, Math.PI / 4, 0, Math.PI * 2);
  ctx.fill();

  ctx.restore();
}

export function drawBeetle(ctx, x, y, age) {
  const wiggle = Math.sin(age * 8 + x) * 1.5;
  ctx.save();
  ctx.translate(x, y);
  ctx.rotate(wiggle * 0.04);

  ctx.fillStyle = 'rgba(0,0,0,0.2)';
  ctx.beginPath();
  ctx.ellipse(0, 8, 9, 2.5, 0, 0, Math.PI * 2);
  ctx.fill();

  // Legs
  ctx.strokeStyle = '#1b2a1c';
  ctx.lineWidth = 1.5;
  for (const s of [-1, 1]) {
    for (const lx of [-3, 0, 3]) {
      ctx.beginPath();
      ctx.moveTo(lx, 0);
      ctx.lineTo(lx + s * 5, 5 + wiggle * s * 0.3);
      ctx.stroke();
    }
  }

  const grad = ctx.createRadialGradient(-2, -3, 1, 0, 0, 9);
  grad.addColorStop(0, '#5fd4c0');
  grad.addColorStop(0.6, '#2a9d8f');
  grad.addColorStop(1, '#1b6e63');
  ctx.fillStyle = grad;
  ctx.beginPath();
  ctx.ellipse(0, 0, 9, 6.2, 0, 0, Math.PI * 2);
  ctx.fill();
  ctx.strokeStyle = '#134039';
  ctx.lineWidth = 1;
  ctx.stroke();

  ctx.beginPath();
  ctx.moveTo(0, -5.5);
  ctx.lineTo(0, 5.5);
  ctx.stroke();

  ctx.fillStyle = 'rgba(255,255,255,0.45)';
  ctx.beginPath();
  ctx.ellipse(-2.5, -2, 1.6, 2.4, -0.4, 0, Math.PI * 2);
  ctx.fill();

  ctx.fillStyle = '#e76f51';
  ctx.beginPath();
  ctx.arc(7, 0, 3, 0, Math.PI * 2);
  ctx.fill();
  ctx.strokeStyle = '#1b2a1c';
  ctx.lineWidth = 1;
  ctx.beginPath();
  ctx.moveTo(9, -1.5);
  ctx.lineTo(12, -4);
  ctx.moveTo(9, 1.5);
  ctx.lineTo(12, 4);
  ctx.stroke();

  ctx.restore();
}

// --- Goal burrow ------------------------------------------------------

export function drawBurrow(ctx, x, y, claimed, age) {
  ctx.save();
  ctx.translate(x, y);

  const grad = ctx.createRadialGradient(0, 2, 2, 0, 2, 20);
  grad.addColorStop(0, '#6f4a2b');
  grad.addColorStop(1, '#4a3018');
  ctx.fillStyle = grad;
  ctx.beginPath();
  ctx.ellipse(0, 4, 19, 10, 0, 0, Math.PI * 2);
  ctx.fill();

  const holeGrad = ctx.createRadialGradient(0, 2, 1, 0, 2, 12);
  holeGrad.addColorStop(0, '#0a0705');
  holeGrad.addColorStop(1, '#241509');
  ctx.fillStyle = holeGrad;
  ctx.beginPath();
  ctx.ellipse(0, 2, 12, 7, 0, 0, Math.PI * 2);
  ctx.fill();

  // Grass tufts around rim
  ctx.strokeStyle = '#3f8d46';
  ctx.lineWidth = 2.5;
  for (const a of [-2.4, -1.9, -0.7, -0.2, 0.5, 1.1, 2.6]) {
    const bx = Math.cos(a) * 17;
    const by = 4 + Math.sin(a) * 8;
    ctx.beginPath();
    ctx.moveTo(bx, by);
    ctx.lineTo(bx + Math.cos(a) * 5, by - 8 + Math.sin(age * 1.6 + a) * 1.2);
    ctx.stroke();
  }

  if (claimed) {
    const peek = Math.min(1, age * 3);
    ctx.save();
    ctx.beginPath();
    ctx.ellipse(0, 2, 12, 7, 0, 0, Math.PI * 2);
    ctx.clip();
    ctx.translate(0, 6 - peek * 6);
    ctx.fillStyle = '#4a2810';
    ctx.beginPath();
    ctx.arc(0, 0, 7, 0, Math.PI * 2);
    ctx.fill();
    ctx.fillStyle = '#101010';
    ctx.beginPath();
    ctx.arc(-2.5, -1, 1.2, 0, Math.PI * 2);
    ctx.arc(2.5, -1, 1.2, 0, Math.PI * 2);
    ctx.fill();
    ctx.restore();
  }

  ctx.restore();
}

// --- Particles ------------------------------------------------------------

export function drawDustParticle(ctx, x, y, size, alpha) {
  const grad = ctx.createRadialGradient(x, y, 0, x, y, size);
  grad.addColorStop(0, `rgba(216, 243, 220, ${alpha})`);
  grad.addColorStop(1, `rgba(216, 243, 220, 0)`);
  ctx.fillStyle = grad;
  ctx.beginPath();
  ctx.arc(x, y, size, 0, Math.PI * 2);
  ctx.fill();
}

export function drawSplashParticle(ctx, x, y, size, alpha, angle) {
  ctx.save();
  ctx.globalAlpha = alpha;
  ctx.fillStyle = '#90e0ef';
  ctx.translate(x, y);
  ctx.rotate(angle);
  ctx.beginPath();
  ctx.ellipse(0, 0, size * 0.5, size, 0, 0, Math.PI * 2);
  ctx.fill();
  ctx.restore();
}

export function drawConfettiParticle(ctx, x, y, size, alpha, color, angle) {
  ctx.save();
  ctx.globalAlpha = alpha;
  ctx.translate(x, y);
  ctx.rotate(angle);
  ctx.fillStyle = color;
  ctx.fillRect(-size / 2, -size / 3, size, size * 0.66);
  ctx.restore();
}

// --- Player -----------------------------------------------------------

export function drawHedgehog(ctx, radius, deathType, moveDir, jumpProgress, blink) {
  const bodyGrad = ctx.createRadialGradient(-radius * 0.3, -radius * 0.3, 1, 0, 0, radius);
  if (deathType === 'SOAKED') {
    bodyGrad.addColorStop(0, '#4ea8de');
    bodyGrad.addColorStop(1, '#023e7d');
  } else {
    bodyGrad.addColorStop(0, '#6b4226');
    bodyGrad.addColorStop(1, '#38200f');
  }
  ctx.fillStyle = bodyGrad;
  ctx.beginPath();
  ctx.arc(0, 0, radius, 0, Math.PI * 2);
  ctx.fill();

  // Quill fan along the back arc
  ctx.strokeStyle = deathType === 'SOAKED' ? '#012a4a' : '#241207';
  ctx.lineWidth = Math.max(1.5, radius * 0.09);
  ctx.lineCap = 'round';
  const backStart = Math.PI * 0.15;
  const backEnd = Math.PI * 1.85;
  const quillCount = 9;
  for (let i = 0; i <= quillCount; i++) {
    const a = backStart + ((backEnd - backStart) * i) / quillCount;
    const inner = radius * 0.78;
    const outer = radius * (1.28 + (i % 2 === 0 ? 0.12 : 0));
    ctx.beginPath();
    ctx.moveTo(Math.cos(a) * inner, Math.sin(a) * inner);
    ctx.lineTo(Math.cos(a) * outer, Math.sin(a) * outer);
    ctx.stroke();
  }

  // Face mask
  ctx.fillStyle = '#e9c99a';
  ctx.beginPath();
  ctx.arc(0, -radius * 0.15, radius * 0.62, 0, Math.PI * 2);
  ctx.fill();

  // Ears
  ctx.fillStyle = '#c98a52';
  ctx.beginPath();
  ctx.arc(-radius * 0.52, -radius * 0.42, radius * 0.16, 0, Math.PI * 2);
  ctx.arc(radius * 0.52, -radius * 0.42, radius * 0.16, 0, Math.PI * 2);
  ctx.fill();
  ctx.fillStyle = '#8a5a34';
  ctx.beginPath();
  ctx.arc(-radius * 0.52, -radius * 0.42, radius * 0.08, 0, Math.PI * 2);
  ctx.arc(radius * 0.52, -radius * 0.42, radius * 0.08, 0, Math.PI * 2);
  ctx.fill();

  // Eyes (blink = closed lids)
  ctx.fillStyle = '#141414';
  if (blink) {
    ctx.strokeStyle = '#141414';
    ctx.lineWidth = 1.6;
    ctx.beginPath();
    ctx.moveTo(-radius * 0.32, -radius * 0.3);
    ctx.lineTo(-radius * 0.12, -radius * 0.3);
    ctx.moveTo(radius * 0.12, -radius * 0.3);
    ctx.lineTo(radius * 0.32, -radius * 0.3);
    ctx.stroke();
  } else {
    ctx.beginPath();
    ctx.arc(-radius * 0.22, -radius * 0.32, radius * 0.09, 0, Math.PI * 2);
    ctx.arc(radius * 0.22, -radius * 0.32, radius * 0.09, 0, Math.PI * 2);
    ctx.fill();
    ctx.fillStyle = '#ffffff';
    ctx.beginPath();
    ctx.arc(-radius * 0.19, -radius * 0.36, radius * 0.03, 0, Math.PI * 2);
    ctx.arc(radius * 0.25, -radius * 0.36, radius * 0.03, 0, Math.PI * 2);
    ctx.fill();
  }

  // Snout
  ctx.fillStyle = '#e63946';
  ctx.beginPath();
  ctx.arc(0, -radius * 0.52, radius * 0.11, 0, Math.PI * 2);
  ctx.fill();

  // Paws — peek out based on hop phase / last direction
  if (jumpProgress < 1) {
    const swing = Math.sin(Math.min(1, jumpProgress) * Math.PI) * radius * 0.35;
    ctx.fillStyle = '#e9c99a';
    ctx.beginPath();
    ctx.ellipse(-radius * 0.5 - moveDir.x * swing * 0.4, radius * 0.75, radius * 0.18, radius * 0.13, 0, 0, Math.PI * 2);
    ctx.ellipse(radius * 0.5 + moveDir.x * swing * 0.4, radius * 0.75, radius * 0.18, radius * 0.13, 0, 0, Math.PI * 2);
    ctx.fill();
  }
}

export function drawRollingBall(ctx, radius, spinAngle) {
  const grad = ctx.createRadialGradient(-radius * 0.3, -radius * 0.3, 1, 0, 0, radius);
  grad.addColorStop(0, '#8a6a4d');
  grad.addColorStop(1, '#3f2c1a');
  ctx.fillStyle = grad;
  ctx.beginPath();
  ctx.arc(0, 0, radius, 0, Math.PI * 2);
  ctx.fill();

  ctx.save();
  ctx.rotate(spinAngle);
  ctx.strokeStyle = '#241609';
  ctx.lineWidth = Math.max(2, radius * 0.12);
  for (let a = 0; a < Math.PI * 2; a += Math.PI / 4) {
    ctx.beginPath();
    ctx.moveTo(Math.cos(a) * radius * 0.2, Math.sin(a) * radius * 0.2);
    ctx.lineTo(Math.cos(a) * radius * 0.95, Math.sin(a) * radius * 0.95);
    ctx.stroke();
  }
  ctx.restore();

  ctx.fillStyle = 'rgba(255,255,255,0.15)';
  ctx.beginPath();
  ctx.arc(-radius * 0.3, -radius * 0.3, radius * 0.35, 0, Math.PI * 2);
  ctx.fill();
}
