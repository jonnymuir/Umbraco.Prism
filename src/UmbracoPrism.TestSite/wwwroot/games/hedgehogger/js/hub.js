/**
 * The progress hub — the game's home screen. Shows every level with its
 * lock state and best score/time, read straight from localStorage (the
 * hub has no dependency on a live engine instance), plus one trailing
 * "coming soon" placeholder tile for whatever level doesn't exist yet.
 * Tapping an unlocked tile hands its id to `onSelect`.
 */

const TILE_H = 108;
const TILE_GAP = 14;
const HEADER_H = 96;
const PAD = 18;
const WIDTH = 392;

function readProgress() {
  try {
    const parsed = JSON.parse(localStorage.getItem('hh_progress') || 'null');
    return { unlocked: Array.isArray(parsed?.unlocked) ? parsed.unlocked : [] };
  } catch {
    return { unlocked: [] };
  }
}

export class LevelHub {
  constructor(canvas, levels, onSelect) {
    this.canvas = canvas;
    this.ctx = canvas.getContext('2d');
    this.levels = levels;
    this.onSelect = onSelect;
    this.active = false;
    this.age = 0;
    this.lastTime = performance.now();

    this.logicalWidth = WIDTH;
    this.logicalHeight = HEADER_H + (levels.length + 1) * (TILE_H + TILE_GAP) + PAD;

    this.onPointerUp = this.onPointerUp.bind(this);
    this.canvas.addEventListener('pointerup', this.onPointerUp);

    this.loopBound = (ts) => this.loop(ts);
    requestAnimationFrame(this.loopBound);
  }

  show() {
    this.active = true;
    this.resize();
  }

  hide() {
    this.active = false;
  }

  resize() {
    if (!this.active) return;
    const dpr = Math.min(window.devicePixelRatio || 1, 2);
    this.canvas.width = Math.round(this.logicalWidth * dpr);
    this.canvas.height = Math.round(this.logicalHeight * dpr);
    this.ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    const scale = Math.min(window.innerWidth / this.logicalWidth, window.innerHeight / this.logicalHeight);
    this.canvas.style.width = `${Math.floor(this.logicalWidth * scale)}px`;
    this.canvas.style.height = `${Math.floor(this.logicalHeight * scale)}px`;
  }

  tileRect(index) {
    return { x: PAD, y: HEADER_H + index * (TILE_H + TILE_GAP), w: this.logicalWidth - PAD * 2, h: TILE_H };
  }

  levelStatus(level) {
    const { unlocked } = readProgress();
    const isUnlocked = unlocked.includes(level.id);
    const bestScore = parseInt(localStorage.getItem(`hh_${level.id}_bestscore`) || '0', 10);
    const bestTimeMs = parseInt(localStorage.getItem(`hh_${level.id}_besttime`) || '0', 10) || null;
    return { unlocked: isUnlocked, bestScore, bestTimeMs };
  }

  onPointerUp(e) {
    if (!this.active) return;
    const rect = this.canvas.getBoundingClientRect();
    if (rect.width === 0 || rect.height === 0) return;
    const x = (e.clientX - rect.left) * (this.logicalWidth / rect.width);
    const y = (e.clientY - rect.top) * (this.logicalHeight / rect.height);
    for (let i = 0; i < this.levels.length; i++) {
      const t = this.tileRect(i);
      if (x >= t.x && x <= t.x + t.w && y >= t.y && y <= t.y + t.h) {
        if (this.levelStatus(this.levels[i]).unlocked) this.onSelect(this.levels[i].id);
        return;
      }
    }
  }

  loop(ts) {
    const dt = Math.min((ts - this.lastTime) / 1000, 0.1);
    this.lastTime = ts;
    if (this.active) {
      this.age += dt;
      this.render();
    }
    requestAnimationFrame(this.loopBound);
  }

  render() {
    const ctx = this.ctx;
    ctx.fillStyle = '#0d130e';
    ctx.fillRect(0, 0, this.logicalWidth, this.logicalHeight);

    ctx.textAlign = 'center';
    ctx.fillStyle = '#52b788';
    ctx.font = '900 24px sans-serif';
    ctx.fillText('HEDGEHOGGER', this.logicalWidth / 2, 38);
    ctx.fillStyle = 'rgba(255,255,255,0.6)';
    ctx.font = '600 13px sans-serif';
    ctx.fillText('Choose a level', this.logicalWidth / 2, 60);

    this.levels.forEach((level, i) => this.renderTile(level, i));
    this.renderComingSoonTile(this.levels.length);
  }

  renderTile(level, index) {
    const ctx = this.ctx;
    const { x, y, w, h } = this.tileRect(index);
    const { unlocked, bestScore, bestTimeMs } = this.levelStatus(level);
    const bob = Math.sin(this.age * 2 + index) * (unlocked ? 1.5 : 0);

    ctx.save();
    ctx.translate(0, bob);

    const grad = ctx.createLinearGradient(0, y, 0, y + h);
    if (unlocked) { grad.addColorStop(0, '#1b2a1c'); grad.addColorStop(1, '#16261a'); }
    else { grad.addColorStop(0, '#161616'); grad.addColorStop(1, '#111111'); }
    ctx.fillStyle = grad;
    ctx.beginPath();
    ctx.roundRect(x, y, w, h, 16);
    ctx.fill();
    ctx.strokeStyle = unlocked ? '#52b788' : '#3a3a3a';
    ctx.lineWidth = 2;
    ctx.stroke();

    ctx.textAlign = 'left';
    ctx.fillStyle = unlocked ? '#f1faee' : 'rgba(255,255,255,0.45)';
    ctx.font = '900 17px sans-serif';
    ctx.fillText(`LEVEL ${index + 1}`, x + 18, y + 30);
    ctx.fillStyle = unlocked ? '#a3d977' : 'rgba(255,255,255,0.35)';
    ctx.font = '700 14px sans-serif';
    ctx.fillText(level.name, x + 18, y + 52);

    ctx.textAlign = 'right';
    if (!unlocked) {
      ctx.fillStyle = 'rgba(255,255,255,0.45)';
      ctx.font = '700 12px sans-serif';
      ctx.fillText('🔒 LOCKED', x + w - 16, y + h / 2 + 5);
    } else if (bestScore > 0 || bestTimeMs) {
      ctx.fillStyle = '#ffb703';
      ctx.font = '900 15px sans-serif';
      ctx.fillText(`BEST ${bestScore}`, x + w - 16, y + h - 36);
      ctx.fillStyle = '#caf0f8';
      ctx.font = '700 12px sans-serif';
      ctx.fillText(`TIME ${bestTimeMs ? (bestTimeMs / 1000).toFixed(1) + 's' : '—'}`, x + w - 16, y + h - 16);
    } else {
      ctx.fillStyle = '#e9c46a';
      ctx.font = '700 12px sans-serif';
      ctx.fillText('TAP TO PLAY', x + w - 16, y + h / 2 + 5);
    }

    ctx.restore();
  }

  renderComingSoonTile(index) {
    const ctx = this.ctx;
    const { x, y, w, h } = this.tileRect(index);
    ctx.save();
    ctx.globalAlpha = 0.4;
    ctx.setLineDash([6, 6]);
    ctx.strokeStyle = '#666';
    ctx.lineWidth = 2;
    ctx.beginPath();
    ctx.roundRect(x, y, w, h, 16);
    ctx.stroke();
    ctx.setLineDash([]);
    ctx.textAlign = 'center';
    ctx.fillStyle = 'rgba(255,255,255,0.5)';
    ctx.font = '900 15px sans-serif';
    ctx.fillText(`LEVEL ${index + 1}`, this.logicalWidth / 2, y + h / 2 - 6);
    ctx.font = '700 12px sans-serif';
    ctx.fillText('Coming soon!', this.logicalWidth / 2, y + h / 2 + 14);
    ctx.restore();
  }
}
