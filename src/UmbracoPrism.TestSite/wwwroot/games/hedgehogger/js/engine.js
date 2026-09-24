import * as S from './sprites.js';

const HUD_HEIGHT = 72;
const HOP_RATE = 7.2; // jumpProgress units/sec -> ~0.14s hop
const LANDING_THRESHOLD = 0.5; // hazard checks only apply once this far into the hop
const INTERP_RATE = 20;
const CAMERA_RATE = 6;
const ROLL_DURATION = 0.32;
const ROLL_COOLDOWN = 0.9;
const ROLL_LANES = 2;
const OBSTACLE_MARGIN = 100;
const PROGRESS_KEY = 'hh_progress';

function wrapX(startX, dir, speed, t, width, margin) {
  const cycle = width + margin * 2;
  let x = startX + dir * speed * t;
  x = ((x + margin) % cycle + cycle) % cycle - margin;
  return x;
}

function clamp(v, min, max) {
  return Math.max(min, Math.min(max, v));
}

function loadProgress() {
  try {
    const parsed = JSON.parse(localStorage.getItem(PROGRESS_KEY) || 'null');
    return {
      unlocked: Array.isArray(parsed?.unlocked) ? parsed.unlocked : [],
      lastPlayed: typeof parsed?.lastPlayed === 'string' ? parsed.lastPlayed : null,
    };
  } catch {
    return { unlocked: [], lastPlayed: null };
  }
}

class Particle {
  constructor(x, y, kind, vx, vy, size, life, color) {
    this.x = x;
    this.y = y;
    this.kind = kind;
    this.vx = vx;
    this.vy = vy;
    this.size = size;
    this.life = life;
    this.maxLife = life;
    this.color = color;
    this.angle = Math.random() * Math.PI * 2;
    this.spin = (Math.random() - 0.5) * 6;
  }

  update(dt) {
    this.x += this.vx * dt * 60;
    this.y += this.vy * dt * 60;
    this.vy += dt * 40;
    this.angle += this.spin * dt;
    this.life -= dt;
  }

  draw(ctx) {
    const alpha = Math.max(0, this.life / this.maxLife);
    if (this.kind === 'splash') S.drawSplashParticle(ctx, this.x, this.y, this.size, alpha, this.angle);
    else if (this.kind === 'confetti') S.drawConfettiParticle(ctx, this.x, this.y, this.size, alpha, this.color, this.angle);
    else S.drawDustParticle(ctx, this.x, this.y, this.size, alpha);
  }
}

class FloatingText {
  constructor(x, y, text, color) {
    this.x = x;
    this.y = y;
    this.text = text;
    this.color = color;
    this.life = 0.9;
    this.maxLife = 0.9;
  }

  update(dt) {
    this.y -= 32 * dt;
    this.life -= dt;
  }

  draw(ctx) {
    const alpha = Math.max(0, this.life / this.maxLife);
    ctx.save();
    ctx.globalAlpha = alpha;
    ctx.fillStyle = this.color;
    ctx.strokeStyle = '#000000';
    ctx.lineWidth = 3.5;
    ctx.font = '900 17px sans-serif';
    ctx.textAlign = 'center';
    ctx.strokeText(this.text, this.x, this.y);
    ctx.fillText(this.text, this.x, this.y);
    ctx.restore();
  }
}

export class HedgehoggerGame {
  constructor(canvas, levels) {
    this.canvas = canvas;
    this.ctx = canvas.getContext('2d');
    this.levels = levels;
    this.levelsById = new Map(levels.map((l) => [l.id, l]));

    this.progress = loadProgress();
    if (!this.progress.unlocked.includes(levels[0].id)) this.progress.unlocked.unshift(levels[0].id);
    const startId = this.progress.lastPlayed && this.levelsById.has(this.progress.lastPlayed)
      ? this.progress.lastPlayed
      : levels[0].id;

    this.decorAge = 0;
    this.blinkTimer = 2 + Math.random() * 2;
    this.blink = false;

    this.resize = this.resize.bind(this);
    window.addEventListener('resize', this.resize);

    this.initInput();
    this.setLevel(this.levelsById.get(startId));

    this.lastTime = performance.now();
    requestAnimationFrame((ts) => this.loop(ts));

    // Test/debug hook — harmless in production, lets automated checks drive
    // the game deterministically without simulating raw pointer gestures.
    window.__hedgehogger = this;
  }

  // --- Level lifecycle ---------------------------------------------------

  setLevel(level) {
    this.level = level;
    this.gridWidth = level.cols * level.laneSize;
    this.gridHeight = level.rows * level.laneSize;
    this.laneSize = level.laneSize;
    this.colWidth = level.laneSize;
    this.viewportRows = level.viewportRows || level.rows;
    this.viewportHeight = this.viewportRows * level.laneSize;
    this.logicalWidth = this.gridWidth;
    this.logicalHeight = HUD_HEIGHT + this.viewportHeight;
    this.cameraY = 0;

    this.bestScore = parseInt(localStorage.getItem(`hh_${level.id}_bestscore`) || '0', 10);
    this.bestTimeMs = parseInt(localStorage.getItem(`hh_${level.id}_besttime`) || '0', 10) || null;

    this.progress.lastPlayed = level.id;
    localStorage.setItem(PROGRESS_KEY, JSON.stringify(this.progress));

    this.resize();
    this.resetRun();
  }

  resize() {
    const dpr = Math.min(window.devicePixelRatio || 1, 2);
    this.canvas.width = Math.round(this.logicalWidth * dpr);
    this.canvas.height = Math.round(this.logicalHeight * dpr);
    this.ctx.setTransform(dpr, 0, 0, dpr, 0, 0);

    const vw = window.innerWidth;
    const vh = window.innerHeight;
    const scale = Math.min(vw / this.logicalWidth, vh / this.logicalHeight);
    this.canvas.style.width = `${Math.floor(this.logicalWidth * scale)}px`;
    this.canvas.style.height = `${Math.floor(this.logicalHeight * scale)}px`;
  }

  resetRun() {
    this.state = 'START';
    this.score = 0;
    this.apples = 0;
    this.beetles = 0;
    this.furthestLane = 0;
    this.particles = [];
    this.floatingTexts = [];
    this.deathCause = null;
    this.deathTimer = 0;
    this.playAge = 0;
    this.winTimeMs = 0;
    this.goalClaimed = false;
    this.cameraY = 0;

    // Collectibles live on the level's own data object, so a fresh run must
    // explicitly reset them — otherwise "collected" persists forever once a
    // treat is eaten, even across restarts.
    for (const lane of this.level.lanes) {
      if (lane.items) for (const item of lane.items) item.collected = false;
    }

    this.player = {
      gridX: this.level.startCol,
      laneIndex: 0,
      x: 0,
      y: 0,
      visualX: 0,
      visualY: 0,
      jumpZ: 0,
      jumpProgress: 1,
      moveDir: { x: 0, y: 0 },
      radius: this.laneSize * 0.33,
      isRolling: false,
      rollTimer: 0,
      rollCooldown: 0,
      deathType: null,
    };
    this.updatePlayerWorldTarget(true);
  }

  updatePlayerWorldTarget(snap = false) {
    this.player.x = (this.player.gridX + 0.5) * this.colWidth;
    this.player.y = this.gridHeight - (this.player.laneIndex + 0.5) * this.laneSize;
    if (snap) {
      this.player.visualX = this.player.x;
      this.player.visualY = this.player.y;
    }
  }

  beginPlaying() {
    this.state = 'PLAYING';
    this.playAge = 0;
    this.levelStartTime = performance.now();
  }

  // --- Input -----------------------------------------------------------

  initInput() {
    let startX = 0;
    let startY = 0;
    let lastTap = 0;

    const onPrimary = () => {
      if (this.state === 'LEVEL_COMPLETE' && this.level.nextLevelId && this.levelsById.has(this.level.nextLevelId)) {
        this.setLevel(this.levelsById.get(this.level.nextLevelId));
        this.beginPlaying();
        return;
      }
      if (this.state === 'START' || this.state === 'GAMEOVER' || this.state === 'LEVEL_COMPLETE') {
        this.resetRun();
        this.beginPlaying();
      }
    };

    // Pointer Events unify mouse, touch and pen — this makes the game
    // playable with a mouse (desktop/Storybook/QA), not just touchscreens.
    this.canvas.addEventListener('pointerdown', (e) => {
      e.preventDefault();
      this.canvas.setPointerCapture(e.pointerId);
      startX = e.clientX;
      startY = e.clientY;
      const now = performance.now();
      if (this.state === 'PLAYING' && now - lastTap < 220) this.performMove('ROLL');
      lastTap = now;
    });

    this.canvas.addEventListener('pointerup', (e) => {
      e.preventDefault();
      if (this.state !== 'PLAYING') {
        onPrimary();
        return;
      }
      const dx = e.clientX - startX;
      const dy = e.clientY - startY;
      const thresh = 18;
      if (Math.abs(dx) > Math.abs(dy)) {
        if (dx > thresh) this.performMove('RIGHT');
        else if (dx < -thresh) this.performMove('LEFT');
      } else {
        if (dy < -thresh) this.performMove('UP');
        else if (dy > thresh) this.performMove('DOWN');
      }
    });

    window.addEventListener('keydown', (e) => {
      if (this.state !== 'PLAYING') {
        if (e.code === 'Space' || e.code === 'Enter') onPrimary();
        return;
      }
      switch (e.code) {
        case 'ArrowUp': case 'KeyW': this.performMove('UP'); break;
        case 'ArrowDown': case 'KeyS': this.performMove('DOWN'); break;
        case 'ArrowLeft': case 'KeyA': this.performMove('LEFT'); break;
        case 'ArrowRight': case 'KeyD': this.performMove('RIGHT'); break;
        case 'Space': this.performMove('ROLL'); break;
      }
    });
  }

  performMove(dir) {
    if (this.state !== 'PLAYING') return;
    const p = this.player;
    let targetGridX = p.gridX;
    let targetLane = p.laneIndex;
    const maxLane = this.level.rows - 1;

    if (dir === 'UP') { targetLane = Math.min(maxLane, targetLane + 1); p.moveDir = { x: 0, y: -1 }; }
    else if (dir === 'DOWN') { targetLane = Math.max(0, targetLane - 1); p.moveDir = { x: 0, y: 1 }; }
    else if (dir === 'LEFT') { targetGridX = Math.max(0, targetGridX - 1); p.moveDir = { x: -1, y: 0 }; }
    else if (dir === 'RIGHT') { targetGridX = Math.min(this.level.cols - 1, targetGridX + 1); p.moveDir = { x: 1, y: 0 }; }
    else if (dir === 'ROLL') {
      if (p.rollCooldown > 0) return;
      targetLane = Math.min(maxLane, targetLane + ROLL_LANES);
      p.isRolling = true;
      p.rollTimer = ROLL_DURATION;
      p.rollCooldown = ROLL_COOLDOWN;
      p.moveDir = { x: 0, y: -1 };
      if (window.Capacitor?.Plugins?.Haptics) window.Capacitor.Plugins.Haptics.impact({ style: 'MEDIUM' });
    }

    if (targetGridX === p.gridX && targetLane === p.laneIndex) return;

    p.gridX = targetGridX;
    p.laneIndex = targetLane;
    p.jumpProgress = 0;
    this.updatePlayerWorldTarget();

    this.spawnDust(p.visualX, p.visualY + p.radius * 0.6, 4);

    if (p.laneIndex > this.furthestLane) {
      const diff = p.laneIndex - this.furthestLane;
      this.furthestLane = p.laneIndex;
      this.score += diff * 10;
      this.floatingTexts.push(new FloatingText(p.visualX, p.visualY - 24, `+${diff * 10}`, '#ffb703'));
      this.maybeSaveBestScore();
    }
  }

  // --- Effects ---------------------------------------------------------

  spawnDust(x, y, count) {
    for (let i = 0; i < count; i++) {
      this.particles.push(new Particle(
        x + (Math.random() - 0.5) * 12, y + (Math.random() - 0.5) * 8, 'dust',
        (Math.random() - 0.5) * 3, (Math.random() - 0.5) * 3, Math.random() * 3 + 2, 0.4,
      ));
    }
  }

  spawnSplash(x, y, count) {
    for (let i = 0; i < count; i++) {
      this.particles.push(new Particle(
        x + (Math.random() - 0.5) * 14, y + (Math.random() - 0.5) * 6, 'splash',
        (Math.random() - 0.5) * 4, -Math.random() * 4 - 1, Math.random() * 3 + 3, 0.55,
      ));
    }
  }

  spawnConfetti(x, y, count) {
    const colors = ['#ffd166', '#ef476f', '#06d6a0', '#118ab2', '#f1faee'];
    for (let i = 0; i < count; i++) {
      this.particles.push(new Particle(
        x + (Math.random() - 0.5) * 30, y + (Math.random() - 0.5) * 10, 'confetti',
        (Math.random() - 0.5) * 5, -Math.random() * 5 - 2, Math.random() * 4 + 4, 1.1,
        colors[i % colors.length],
      ));
    }
  }

  maybeSaveBestScore() {
    if (this.score > this.bestScore) {
      this.bestScore = this.score;
      localStorage.setItem(`hh_${this.level.id}_bestscore`, String(this.bestScore));
    }
  }

  triggerDeath(type, reason) {
    this.state = 'DEATH_ANIM';
    this.deathCause = reason;
    this.player.deathType = type;
    this.deathTimer = 0.85;
    if (window.Capacitor?.Plugins?.Haptics) window.Capacitor.Plugins.Haptics.notification({ type: 'ERROR' });

    if (type === 'DROWN') {
      this.floatingTexts.push(new FloatingText(this.player.visualX, this.player.visualY - 30, 'SPLASH!', '#00b4d8'));
      this.spawnSplash(this.player.visualX, this.player.visualY, 14);
    } else if (type === 'SQUISH') {
      this.floatingTexts.push(new FloatingText(this.player.visualX, this.player.visualY - 30, 'SQUISHED!', '#e63946'));
      this.spawnDust(this.player.visualX, this.player.visualY, 12);
    } else if (type === 'SOAKED') {
      this.floatingTexts.push(new FloatingText(this.player.visualX, this.player.visualY - 30, 'SOAKED!', '#48cae4'));
      this.spawnSplash(this.player.visualX, this.player.visualY, 10);
    }
  }

  triggerWin() {
    this.state = 'LEVEL_COMPLETE';
    this.goalClaimed = true;
    this.winTimeMs = performance.now() - this.levelStartTime;
    this.score += 200;
    this.maybeSaveBestScore();
    if (!this.bestTimeMs || this.winTimeMs < this.bestTimeMs) {
      this.bestTimeMs = this.winTimeMs;
      localStorage.setItem(`hh_${this.level.id}_besttime`, String(Math.round(this.bestTimeMs)));
    }
    if (this.level.nextLevelId && !this.progress.unlocked.includes(this.level.nextLevelId)) {
      this.progress.unlocked.push(this.level.nextLevelId);
      localStorage.setItem(PROGRESS_KEY, JSON.stringify(this.progress));
    }
    this.spawnConfetti(this.player.visualX, this.player.visualY, 24);
    if (window.Capacitor?.Plugins?.Haptics) window.Capacitor.Plugins.Haptics.notification({ type: 'SUCCESS' });
  }

  // --- Deterministic obstacle/hazard state ------------------------------

  obstacleX(lane, obs) {
    return wrapX(obs.startX, lane.dir, lane.speed, this.playAge, this.gridWidth, OBSTACLE_MARGIN);
  }

  sprinklerState(spr) {
    const cyclePos = (((this.playAge + spr.phase) % spr.cycle) + spr.cycle) % spr.cycle;
    if (cyclePos < spr.idleFor) return { state: 'idle', phaseT: cyclePos };
    if (cyclePos < spr.idleFor + spr.chargeFor) return { state: 'charge', phaseT: cyclePos - spr.idleFor };
    return { state: 'burst', phaseT: cyclePos - spr.idleFor - spr.chargeFor };
  }

  // --- Update ------------------------------------------------------------

  update(dt) {
    this.decorAge += dt;
    this.blinkTimer -= dt;
    if (this.blinkTimer <= 0) {
      this.blink = !this.blink;
      this.blinkTimer = this.blink ? 0.12 : 2 + Math.random() * 3;
    }

    this.particles.forEach((p) => p.update(dt));
    this.particles = this.particles.filter((p) => p.life > 0);
    this.floatingTexts.forEach((f) => f.update(dt));
    this.floatingTexts = this.floatingTexts.filter((f) => f.life > 0);

    if (this.state === 'DEATH_ANIM') {
      this.deathTimer -= dt;
      if (this.deathTimer <= 0) this.state = 'GAMEOVER';
      return;
    }
    if (this.state !== 'PLAYING') return;

    this.playAge += dt;

    const p = this.player;
    const interp = 1 - Math.exp(-INTERP_RATE * dt);
    p.visualX += (p.x - p.visualX) * interp;
    p.visualY += (p.y - p.visualY) * interp;

    if (p.jumpProgress < 1) {
      p.jumpProgress = Math.min(1, p.jumpProgress + HOP_RATE * dt);
      p.jumpZ = Math.sin(p.jumpProgress * Math.PI) * this.laneSize * 0.32;
    } else {
      p.jumpZ = 0;
    }

    if (p.rollTimer > 0) {
      p.rollTimer -= dt;
      if (p.rollTimer <= 0) p.isRolling = false;
    }
    if (p.rollCooldown > 0) p.rollCooldown -= dt;

    // Camera follows the player's logical (pre-interpolation) target, giving
    // a slight forward anticipation, and is clamped to the level's bounds —
    // for a level that fits entirely in the viewport this always resolves
    // to 0, so short levels render exactly as if there were no camera at all.
    const maxCameraY = Math.max(0, this.gridHeight - this.viewportHeight);
    const targetCameraY = clamp(p.y - this.viewportHeight * 0.62, 0, maxCameraY);
    this.cameraY += (targetCameraY - this.cameraY) * (1 - Math.exp(-CAMERA_RATE * dt));

    const landed = p.jumpProgress >= LANDING_THRESHOLD;
    const lane = this.level.lanes[p.laneIndex];
    const laneCenterY = this.gridHeight - (p.laneIndex + 0.5) * this.laneSize;

    if (lane.type === 'RIVER') {
      let onLog = false;
      for (const obs of lane.obstacles) {
        const x = this.obstacleX(lane, obs);
        if (p.visualX >= x && p.visualX <= x + obs.width) {
          onLog = true;
          const drift = lane.dir * lane.speed * dt;
          p.x += drift;
          p.visualX += drift;
        }
      }
      if (!onLog && landed && !p.isRolling) {
        this.triggerDeath('DROWN', 'Fell into the garden stream — hop onto a log!');
        return;
      }
    } else if (lane.type === 'ROAD' && landed && !p.isRolling) {
      for (const obs of lane.obstacles) {
        const x = this.obstacleX(lane, obs);
        const top = laneCenterY - obs.height / 2;
        const bottom = laneCenterY + obs.height / 2;
        const closestX = Math.max(x, Math.min(p.visualX, x + obs.width));
        const closestY = Math.max(top, Math.min(p.visualY, bottom));
        const dx = p.visualX - closestX;
        const dy = p.visualY - closestY;
        if (dx * dx + dy * dy < (p.radius * 0.72) ** 2) {
          this.triggerDeath('SQUISH', obs.kind === 'MOWER' ? 'Caught by a lawnmower!' : "Bowled over by next door's cat!");
          return;
        }
      }
    } else if (lane.type === 'SPRINKLER' && landed && !p.isRolling) {
      for (const spr of lane.sprinklers) {
        const { state } = this.sprinklerState(spr);
        if (state !== 'burst') continue;
        const sx = (spr.col + 0.5) * this.colWidth;
        const dx = p.visualX - sx;
        const dy = p.visualY - laneCenterY;
        if (Math.sqrt(dx * dx + dy * dy) < spr.radius * 0.8) {
          this.triggerDeath('SOAKED', 'Blasted by the sprinkler — watch for the charge-up glow!');
          return;
        }
      }
    } else if (lane.type === 'GOAL' && landed && !this.goalClaimed) {
      if (this.level.goalCols.includes(p.gridX)) {
        this.triggerWin();
        return;
      }
    }

    if (lane.items) {
      for (const item of lane.items) {
        if (item.collected) continue;
        const itemX = (item.col + 0.5) * this.colWidth;
        if (Math.abs(p.visualX - itemX) < this.colWidth * 0.45 && Math.abs(p.visualY - laneCenterY) < this.laneSize * 0.6) {
          item.collected = true;
          if (item.type === 'APPLE') {
            this.apples++;
            this.score += 15;
            this.floatingTexts.push(new FloatingText(itemX, p.visualY - 20, '+15', '#ff8a7a'));
            this.spawnDust(itemX, p.visualY, 6);
          } else {
            this.beetles++;
            this.score += 30;
            this.floatingTexts.push(new FloatingText(itemX, p.visualY - 20, '+30', '#5fd4c0'));
            this.spawnDust(itemX, p.visualY, 8);
          }
          this.maybeSaveBestScore();
        }
      }
    }
  }

  // --- Render --------------------------------------------------------

  render() {
    const ctx = this.ctx;
    ctx.fillStyle = '#111b13';
    ctx.fillRect(0, 0, this.logicalWidth, this.logicalHeight);

    ctx.save();
    ctx.translate(0, HUD_HEIGHT - this.cameraY);
    this.renderLanes();
    this.particles.forEach((pt) => pt.draw(ctx));
    this.renderPlayer();
    this.floatingTexts.forEach((f) => f.draw(ctx));
    ctx.restore();

    this.renderHUD();

    const levelNum = this.levels.indexOf(this.level) + 1;
    if (this.state === 'START') {
      this.renderBanner('HEDGEHOGGER', `Level ${levelNum} · ${this.level.name}`, this.level.introText || 'Hop apples & beetles for points. Reach a burrow to win!', 'TAP TO PLAY');
    } else if (this.state === 'GAMEOVER') {
      this.renderBanner('OH NO!', this.deathCause || 'Try again!', `Best score: ${this.bestScore}`, 'TAP TO RETRY');
    } else if (this.state === 'LEVEL_COMPLETE') {
      const secs = (this.winTimeMs / 1000).toFixed(1);
      const bestSecs = this.bestTimeMs ? (this.bestTimeMs / 1000).toFixed(1) : secs;
      const action = this.level.nextLevelId ? 'NEXT LEVEL' : 'PLAY AGAIN';
      this.renderBanner('LEVEL COMPLETE!', `Time: ${secs}s  ·  Best: ${bestSecs}s`, `Score: ${this.score}`, action);
    }
  }

  renderLanes() {
    const ctx = this.ctx;
    const viewTop = this.cameraY - this.laneSize;
    const viewBottom = this.cameraY + this.viewportHeight + this.laneSize;

    for (let i = 0; i < this.level.rows; i++) {
      const y = this.gridHeight - (i + 1) * this.laneSize;
      if (y + this.laneSize < viewTop || y > viewBottom) continue;

      const lane = this.level.lanes[i];
      const h = this.laneSize;

      if (lane.type === 'SAFE') S.drawGrassLane(ctx, 0, y, this.logicalWidth, h, i, this.decorAge);
      else if (lane.type === 'ROAD') S.drawRoadLane(ctx, 0, y, this.logicalWidth, h);
      else if (lane.type === 'RIVER') S.drawRiverLane(ctx, 0, y, this.logicalWidth, h, this.decorAge);
      else if (lane.type === 'SPRINKLER') S.drawSprinklerLane(ctx, 0, y, this.logicalWidth, h);
      else if (lane.type === 'GOAL') S.drawGoalLane(ctx, 0, y, this.logicalWidth, h, i, this.decorAge);

      if (lane.items) {
        for (const item of lane.items) {
          if (item.collected) continue;
          const x = (item.col + 0.5) * this.colWidth;
          if (item.type === 'APPLE') S.drawApple(ctx, x, y + h / 2, this.decorAge);
          else S.drawBeetle(ctx, x, y + h / 2, this.decorAge);
        }
      }

      if (lane.type === 'RIVER') {
        for (const obs of lane.obstacles) S.drawLog(ctx, this.obstacleX(lane, obs), y + h / 2, obs.width, this.laneSize, this.decorAge);
      } else if (lane.type === 'ROAD') {
        for (const obs of lane.obstacles) {
          const x = this.obstacleX(lane, obs);
          if (obs.kind === 'MOWER') S.drawMower(ctx, x, y + h / 2, obs.width, this.decorAge);
          else S.drawCat(ctx, x, y + h / 2, lane.dir, this.decorAge);
        }
      } else if (lane.type === 'SPRINKLER') {
        for (const spr of lane.sprinklers) {
          const { state, phaseT } = this.sprinklerState(spr);
          S.drawSprinkler(ctx, (spr.col + 0.5) * this.colWidth, y + h / 2, spr.radius, state, phaseT);
        }
      } else if (lane.type === 'GOAL') {
        for (const col of this.level.goalCols) {
          const claimed = this.goalClaimed && this.player.gridX === col && this.player.laneIndex === i;
          S.drawBurrow(ctx, (col + 0.5) * this.colWidth, y + h / 2, claimed, this.decorAge);
        }
      }
    }
  }

  renderPlayer() {
    const ctx = this.ctx;
    const p = this.player;
    if (this.state === 'LEVEL_COMPLETE' && !this.goalClaimed) return;

    ctx.save();
    const drawY = p.visualY - p.jumpZ;

    const shadowScale = Math.max(0.35, 1 - p.jumpZ / (this.laneSize * 0.5));
    ctx.fillStyle = 'rgba(0,0,0,0.3)';
    ctx.beginPath();
    ctx.ellipse(p.visualX, p.visualY + p.radius * 0.5, p.radius * 0.8 * shadowScale, p.radius * 0.32 * shadowScale, 0, 0, Math.PI * 2);
    ctx.fill();

    ctx.translate(p.visualX, drawY);

    if (this.state === 'DEATH_ANIM') {
      if (p.deathType === 'DROWN') ctx.scale(Math.max(0, this.deathTimer / 0.85), Math.max(0, this.deathTimer / 0.85));
      else if (p.deathType === 'SQUISH') ctx.scale(1.5, 0.25);
    } else if (this.state === 'LEVEL_COMPLETE') {
      const bounce = 1 + Math.sin(this.decorAge * 10) * 0.06;
      ctx.scale(bounce, bounce);
    }

    if (p.isRolling) {
      S.drawRollingBall(ctx, p.radius, this.decorAge * 14);
    } else {
      S.drawHedgehog(ctx, p.radius, p.deathType, p.moveDir, this.state === 'PLAYING' ? p.jumpProgress : 1, this.blink && this.state !== 'DEATH_ANIM');
    }

    ctx.restore();
  }

  renderHUD() {
    const ctx = this.ctx;
    const levelNum = this.levels.indexOf(this.level) + 1;
    ctx.save();
    ctx.fillStyle = 'rgba(13, 19, 14, 0.92)';
    ctx.fillRect(0, 0, this.logicalWidth, HUD_HEIGHT);
    ctx.strokeStyle = 'rgba(82, 183, 136, 0.4)';
    ctx.lineWidth = 1;
    ctx.beginPath();
    ctx.moveTo(0, HUD_HEIGHT - 0.5);
    ctx.lineTo(this.logicalWidth, HUD_HEIGHT - 0.5);
    ctx.stroke();

    ctx.textAlign = 'left';
    ctx.fillStyle = '#f1faee';
    ctx.font = '900 15px sans-serif';
    ctx.fillText(`LEVEL ${levelNum} · ${this.level.name.toUpperCase()}`, 14, 22);

    ctx.font = '900 14px sans-serif';
    ctx.fillStyle = '#ffb703';
    ctx.fillText(`SCORE ${this.score}`, 14, 42);

    ctx.font = '700 12px sans-serif';
    ctx.fillStyle = '#ff8a7a';
    ctx.fillText(`🍎 ${this.apples}`, 14, 60);
    ctx.fillStyle = '#5fd4c0';
    ctx.fillText(`🪲 ${this.beetles}`, 62, 60);

    if (this.state === 'PLAYING') {
      const secs = ((performance.now() - this.levelStartTime) / 1000).toFixed(1);
      ctx.textAlign = 'right';
      ctx.fillStyle = '#caf0f8';
      ctx.font = '900 20px sans-serif';
      ctx.fillText(`${secs}s`, this.logicalWidth - 14, 34);
      ctx.font = '700 11px sans-serif';
      ctx.fillStyle = 'rgba(255,255,255,0.55)';
      ctx.fillText(`BEST ${this.bestScore}`, this.logicalWidth - 14, 54);
    }
    ctx.restore();
  }

  renderBanner(title, subtitle, footer, action) {
    const ctx = this.ctx;
    ctx.save();
    ctx.fillStyle = 'rgba(8, 12, 9, 0.86)';
    ctx.fillRect(0, 0, this.logicalWidth, this.logicalHeight);
    ctx.textAlign = 'center';

    const cardW = this.logicalWidth * 0.86;
    const cardH = 220;
    const cardX = (this.logicalWidth - cardW) / 2;
    const cardY = this.logicalHeight * 0.3;

    ctx.fillStyle = '#16261a';
    ctx.beginPath();
    ctx.roundRect(cardX, cardY, cardW, cardH, 20);
    ctx.fill();
    ctx.strokeStyle = '#52b788';
    ctx.lineWidth = 3;
    ctx.stroke();

    ctx.fillStyle = '#52b788';
    ctx.font = '900 26px sans-serif';
    ctx.fillText(title, this.logicalWidth / 2, cardY + 42);

    ctx.fillStyle = '#f4a261';
    ctx.font = '600 13px sans-serif';
    wrapText(ctx, subtitle, this.logicalWidth / 2, cardY + 68, cardW - 40, 17);

    ctx.fillStyle = '#e9c46a';
    ctx.font = '700 13px sans-serif';
    ctx.fillText(footer, this.logicalWidth / 2, cardY + 112);

    ctx.fillStyle = 'rgba(255,255,255,0.6)';
    ctx.font = '500 11px sans-serif';
    ctx.fillText('Swipe to hop · double-tap to roll through danger', this.logicalWidth / 2, cardY + 138);

    ctx.fillStyle = '#e63946';
    ctx.beginPath();
    ctx.roundRect(this.logicalWidth * 0.2, cardY + cardH - 54, this.logicalWidth * 0.6, 42, 14);
    ctx.fill();
    ctx.fillStyle = '#ffffff';
    ctx.font = 'bold 15px sans-serif';
    ctx.fillText(action, this.logicalWidth / 2, cardY + cardH - 27);

    ctx.restore();
  }

  loop(timestamp) {
    const dt = Math.min((timestamp - this.lastTime) / 1000, 0.1);
    this.lastTime = timestamp;
    this.update(dt);
    this.render();
    requestAnimationFrame((ts) => this.loop(ts));
  }
}

function wrapText(ctx, text, x, y, maxWidth, lineHeight) {
  const words = text.split(' ');
  let line = '';
  let cy = y;
  for (const word of words) {
    const test = line ? `${line} ${word}` : word;
    if (ctx.measureText(test).width > maxWidth && line) {
      ctx.fillText(line, x, cy);
      line = word;
      cy += lineHeight;
    } else {
      line = test;
    }
  }
  ctx.fillText(line, x, cy);
}
