/**
 * Hedgehogger: Professional Mobile Edition
 * High-Precision Geometric Physics & Cartoon Vector Engine
 */

class Particle {
  constructor(x, y, color, vx, vy, size, life) {
    this.x = x;
    this.y = y;
    this.color = color;
    this.vx = vx;
    this.vy = vy;
    this.size = size;
    this.life = life;
    this.maxLife = life;
  }

  update(dt) {
    this.x += this.vx * dt * 60;
    this.y += this.vy * dt * 60;
    this.life -= dt;
  }

  draw(ctx) {
    const alpha = Math.max(0, this.life / this.maxLife);
    ctx.save();
    ctx.globalAlpha = alpha;
    ctx.fillStyle = this.color;
    ctx.beginPath();
    ctx.arc(this.x, this.y, this.size, 0, Math.PI * 2);
    ctx.fill();
    ctx.restore();
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
    this.y -= 35 * dt;
    this.life -= dt;
  }

  draw(ctx) {
    const alpha = Math.max(0, this.life / this.maxLife);
    ctx.save();
    ctx.globalAlpha = alpha;
    ctx.fillStyle = this.color;
    ctx.strokeStyle = '#000000';
    ctx.lineWidth = 3.5;
    ctx.font = '900 18px sans-serif';
    ctx.textAlign = 'center';
    ctx.strokeText(this.text, this.x, this.y);
    ctx.fillText(this.text, this.x, this.y);
    ctx.restore();
  }
}

class Hedgehogger {
  constructor() {
    this.canvas = document.getElementById('gameCanvas');
    this.ctx = this.canvas.getContext('2d');

    this.scale = 1;
    this.logicalWidth = 400;
    this.logicalHeight = 650;
    this.numCols = 9;
    this.laneHeight = 55;

    this.state = 'START'; // 'START', 'PLAYING', 'DEATH_ANIM', 'GAMEOVER'
    this.score = 0;
    this.highScore = parseInt(localStorage.getItem('hh_highscore') || '0', 10);
    this.apples = 0;
    this.beetles = 0;

    this.cameraY = 0;
    this.particles = [];
    this.floatingTexts = [];
    this.deathCause = null;
    this.deathTimer = 0;

    this.initCanvasSize();
    this.initPlayer();
    this.initLanes();
    this.initInput();

    this.lastTime = performance.now();
    requestAnimationFrame((ts) => this.loop(ts));
  }

  initCanvasSize() {
    const dpr = window.devicePixelRatio || 1;
    const w = window.innerWidth;
    const h = window.innerHeight;

    this.canvas.width = w * dpr;
    this.canvas.height = h * dpr;
    this.scale = dpr;

    this.logicalWidth = w;
    this.logicalHeight = h;
    this.laneHeight = Math.floor(this.logicalHeight / 11);
    this.colWidth = this.logicalWidth / this.numCols;
  }

  initPlayer() {
    this.player = {
      gridX: 4,
      laneIndex: 0,
      x: 0,
      y: 0,
      visualX: 0,
      visualY: 0,
      jumpZ: 0, // Elevation jump arc
      radius: this.laneHeight * 0.32,
      isRolling: false,
      jumpProgress: 1,
      deathType: null
    };
    this.updatePlayerWorldTarget(true);
  }

  updatePlayerWorldTarget(snap = false) {
    this.player.x = (this.player.gridX + 0.5) * this.colWidth;
    this.player.y = this.logicalHeight - (this.player.laneIndex + 0.5) * this.laneHeight;

    if (snap) {
      this.player.visualX = this.player.x;
      this.player.visualY = this.player.y;
    }
  }

  initLanes() {
    this.lanes = [];
    // Warmup Safe Zone (Lanes 0 - 3)
    for (let i = 0; i < 4; i++) {
      this.lanes.push({ type: 'SAFE', index: i, items: [], obstacles: [] });
    }
    // Procedurally generated garden
    for (let i = 4; i < 600; i++) {
      this.lanes.push(this.generateLane(i));
    }
  }

  generateLane(index) {
    // Progressive Difficulty Curve
    const speedMult = 1 + Math.min(index / 50, 1.8);
    const types = ['SAFE', 'ROAD', 'RIVER', 'SPRINKLER'];
    
    let type = 'SAFE';
    const r = Math.random();
    if (index < 6) {
      type = (r < 0.5) ? 'SAFE' : 'ROAD'; // Early gentle introduction
    } else {
      if (r < 0.25) type = 'SAFE';
      else if (r < 0.55) type = 'ROAD';
      else if (r < 0.80) type = 'RIVER';
      else type = 'SPRINKLER';
    }

    const dir = Math.random() < 0.5 ? 1 : -1;
    const baseSpeed = (Math.random() * 40 + 50) * speedMult * dir;

    const lane = {
      index: index,
      type: type,
      speed: baseSpeed,
      obstacles: [],
      items: [],
      timer: Math.random() * 10
    };

    if (type === 'ROAD') {
      const count = index < 10 ? 1 : Math.floor(Math.random() * 2) + 2;
      const spacing = (this.logicalWidth + 140) / count;
      for (let k = 0; k < count; k++) {
        lane.obstacles.push({
          x: k * spacing - 70,
          width: this.laneHeight * 1.3,
          height: this.laneHeight * 0.7,
          type: Math.random() < 0.5 ? 'MOWER' : 'CAT'
        });
      }
    } else if (type === 'RIVER') {
      const count = Math.floor(Math.random() * 2) + 2;
      const spacing = (this.logicalWidth + 180) / count;
      for (let k = 0; k < count; k++) {
        lane.obstacles.push({
          x: k * spacing - 90,
          width: this.laneHeight * (Math.random() < 0.4 ? 2.8 : 3.8),
          height: this.laneHeight * 0.75,
          type: 'LOG'
        });
      }
    } else if (type === 'SPRINKLER') {
      lane.obstacles.push({
        x: this.logicalWidth * (0.2 + Math.random() * 0.6),
        radius: this.laneHeight * 1.25,
        active: false,
        phase: Math.random() * Math.PI * 2
      });
    }

    // Collectibles (Apples & Beetles)
    if (type !== 'RIVER' && Math.random() < 0.4) {
      lane.items.push({
        gridX: Math.floor(Math.random() * 7) + 1,
        type: Math.random() < 0.7 ? 'APPLE' : 'BEETLE',
        collected: false
      });
    }

    return lane;
  }

  initInput() {
    let startX = 0, startY = 0, lastTap = 0;

    window.addEventListener('resize', () => this.initCanvasSize());

    this.canvas.addEventListener('touchstart', (e) => {
      e.preventDefault();
      const t = e.touches[0];
      startX = t.clientX;
      startY = t.clientY;

      const now = performance.now();
      if (now - lastTap < 220) this.performMove('ROLL');
      lastTap = now;
    }, { passive: false });

    this.canvas.addEventListener('touchend', (e) => {
      e.preventDefault();
      if (this.state !== 'PLAYING') {
        if (this.state === 'START' || this.state === 'GAMEOVER') this.restartGame();
        return;
      }

      const t = e.changedTouches[0];
      const dx = t.clientX - startX;
      const dy = t.clientY - startY;
      const thresh = 18;

      if (Math.abs(dx) > Math.abs(dy)) {
        if (dx > thresh) this.performMove('RIGHT');
        else if (dx < -thresh) this.performMove('LEFT');
      } else {
        if (dy < -thresh) this.performMove('UP');
        else if (dy > thresh) this.performMove('DOWN');
      }
    }, { passive: false });

    window.addEventListener('keydown', (e) => {
      if (this.state !== 'PLAYING') {
        if (e.code === 'Space' || e.code === 'Enter') this.restartGame();
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

    let targetGridX = this.player.gridX;
    let targetLane = this.player.laneIndex;

    if (dir === 'UP') targetLane++;
    else if (dir === 'DOWN') targetLane = Math.max(0, targetLane - 1);
    else if (dir === 'LEFT') targetGridX = Math.max(0, targetGridX - 1);
    else if (dir === 'RIGHT') targetGridX = Math.min(this.numCols - 1, targetGridX + 1);
    else if (dir === 'ROLL') {
      targetLane += 2;
      this.player.isRolling = true;
      setTimeout(() => { this.player.isRolling = false; }, 280);

      if (window.Capacitor && window.Capacitor.Plugins.Haptics) {
        window.Capacitor.Plugins.Haptics.impact({ style: 'MEDIUM' });
      }
    }

    this.player.gridX = targetGridX;
    this.player.laneIndex = targetLane;
    this.player.jumpProgress = 0; // Trigger Jump Elevation Arc
    this.updatePlayerWorldTarget();

    // Spawn hop dust
    this.spawnParticles(this.player.visualX, this.player.visualY + 10, '#d8f3dc', 4);

    // Score Check
    if (this.player.laneIndex > this.score) {
      const diff = this.player.laneIndex - this.score;
      this.score = this.player.laneIndex;
      this.floatingTexts.push(new FloatingText(this.player.visualX, this.player.visualY - 25, `+${diff * 10}`, '#ffb703'));

      if (this.score > this.highScore) {
        this.highScore = this.score;
        localStorage.setItem('hh_highscore', this.highScore.toString());
      }
    }
  }

  spawnParticles(x, y, color, count) {
    for (let i = 0; i < count; i++) {
      this.particles.push(new Particle(
        x + (Math.random() - 0.5) * 12,
        y + (Math.random() - 0.5) * 8,
        color,
        (Math.random() - 0.5) * 3,
        (Math.random() - 0.5) * 3,
        Math.random() * 3 + 2,
        0.4
      ));
    }
  }

  restartGame() {
    this.score = 0;
    this.apples = 0;
    this.beetles = 0;
    this.particles = [];
    this.floatingTexts = [];
    this.initPlayer();
    this.initLanes();
    this.state = 'PLAYING';
  }

  triggerDeath(type, reason) {
    this.state = 'DEATH_ANIM';
    this.deathCause = reason;
    this.player.deathType = type;
    this.deathTimer = 0.9;

    if (window.Capacitor && window.Capacitor.Plugins.Haptics) {
      window.Capacitor.Plugins.Haptics.notification({ type: 'ERROR' });
    }

    if (type === 'DROWN') {
      this.floatingTexts.push(new FloatingText(this.player.visualX, this.player.visualY - 30, 'SPLASH!', '#00b4d8'));
      this.spawnParticles(this.player.visualX, this.player.visualY, '#90e0ef', 16);
    } else if (type === 'SQUISH') {
      this.floatingTexts.push(new FloatingText(this.player.visualX, this.player.visualY - 30, 'SQUISHED!', '#e63946'));
      this.spawnParticles(this.player.visualX, this.player.visualY, '#ffb703', 12);
    } else if (type === 'SOAKED') {
      this.floatingTexts.push(new FloatingText(this.player.visualX, this.player.visualY - 30, 'SOAKED!', '#48cae4'));
    }
  }

  update(dt) {
    this.particles.forEach(p => p.update(dt));
    this.particles = this.particles.filter(p => p.life > 0);

    this.floatingTexts.forEach(ft => ft.update(dt));
    this.floatingTexts = this.floatingTexts.filter(ft => ft.life > 0);

    if (this.state === 'DEATH_ANIM') {
      this.deathTimer -= dt;
      if (this.deathTimer <= 0) this.state = 'GAMEOVER';
      return;
    }

    if (this.state !== 'PLAYING') return;

    // Smooth Visual Interpolation
    this.player.visualX += (this.player.x - this.player.visualX) * 18 * dt;
    this.player.visualY += (this.player.y - this.player.visualY) * 18 * dt;

    // Jump Elevation Trajectory Math
    if (this.player.jumpProgress < 1) {
      this.player.jumpProgress += 8 * dt;
      this.player.jumpZ = Math.sin(Math.min(1, this.player.jumpProgress) * Math.PI) * 16;
    } else {
      this.player.jumpZ = 0;
    }

    // Camera follow
    const targetCam = ((this.player.laneIndex) * this.laneHeight) - (this.logicalHeight * 0.38);
    this.cameraY += (targetCam - this.cameraY) * 6 * dt;

    // Update Lanes
    for (let i = 0; i < this.lanes.length; i++) {
      const lane = this.lanes[i];
      lane.timer += dt;

      if (lane.type === 'ROAD' || lane.type === 'RIVER') {
        lane.obstacles.forEach(obs => {
          obs.x += lane.speed * dt;
          if (lane.speed > 0 && obs.x > this.logicalWidth + 90) obs.x = -90;
          if (lane.speed < 0 && obs.x < -90) obs.x = this.logicalWidth + 90;
        });
      } else if (lane.type === 'SPRINKLER') {
        lane.obstacles.forEach(spr => {
          spr.active = Math.sin(lane.timer * 3.2 + spr.phase) > 0.15;
        });
      }
    }

    // --- RIGOROUS GEOMETRIC COLLISION ENGINE ---
    const currentLane = this.lanes[this.player.laneIndex];
    const laneCenterY = this.logicalHeight - (this.player.laneIndex + 0.5) * this.laneHeight;

    if (currentLane) {
      // 1. River Platform Checks
      if (currentLane.type === 'RIVER') {
        let safelyOnLog = false;

        currentLane.obstacles.forEach(log => {
          const logLeft = log.x;
          const logRight = log.x + log.width;

          // Checking player feet center point against log horizontal boundaries
          if (this.player.visualX >= logLeft && this.player.visualX <= logRight) {
            safelyOnLog = true;
            // Drifting along with river log stream
            this.player.x += currentLane.speed * dt;
            this.player.visualX += currentLane.speed * dt;
          }
        });

        if (!safelyOnLog && !this.player.isRolling) {
          this.triggerDeath('DROWN', 'Fell directly into the deep garden stream!');
          return;
        }
      }

      // 2. Road Obstacles Collision (Box-Circle Intersection)
      if (currentLane.type === 'ROAD' && !this.player.isRolling) {
        currentLane.obstacles.forEach(obs => {
          const obsLeft = obs.x;
          const obsRight = obs.x + obs.width;
          const obsTop = laneCenterY - obs.height / 2;
          const obsBottom = laneCenterY + obs.height / 2;

          // Closest point on rectangle to player circle center
          const closestX = Math.max(obsLeft, Math.min(this.player.visualX, obsRight));
          const closestY = Math.max(obsTop, Math.min(this.player.visualY, obsBottom));

          const distX = this.player.visualX - closestX;
          const distY = this.player.visualY - closestY;
          const distanceSquared = (distX * distX) + (distY * distY);

          if (distanceSquared < (this.player.radius * 0.8) * (this.player.radius * 0.8)) {
            this.triggerDeath('SQUISH', 'Crushed by fast garden machinery!');
          }
        });
      }

      // 3. Sprinkler Water Jet
      if (currentLane.type === 'SPRINKLER' && !this.player.isRolling) {
        currentLane.obstacles.forEach(spr => {
          if (spr.active) {
            const distX = this.player.visualX - spr.x;
            const distY = this.player.visualY - laneCenterY;
            const dist = Math.sqrt(distX * distX + distY * distY);

            if (dist < spr.radius * 0.85) {
              this.triggerDeath('SOAKED', 'Blasted by a pulsating water sprinkler!');
            }
          }
        });
      }

      // Collectibles Handling
      currentLane.items.forEach(item => {
        if (!item.collected) {
          const itemX = (item.gridX + 0.5) * this.colWidth;
          const dist = Math.abs(this.player.visualX - itemX);

          if (dist < this.colWidth * 0.45) {
            item.collected = true;
            if (item.type === 'APPLE') {
              this.apples++;
              this.score += 15;
              this.floatingTexts.push(new FloatingText(itemX, this.player.visualY - 20, '+15 APPLE!', '#ff3333'));
              this.spawnParticles(itemX, this.player.visualY, '#ff4d4d', 8);
            } else if (item.type === 'BEETLE') {
              this.beetles++;
              this.score += 30;
              this.floatingTexts.push(new FloatingText(itemX, this.player.visualY - 20, '+30 BEETLE!', '#33cc33'));
              this.spawnParticles(itemX, this.player.visualY, '#33cc33', 10);
            }
          }
        }
      });
    }

    // Boundary Overflow Death
    if (this.player.visualX < 5 || this.player.visualX > this.logicalWidth - 5) {
      this.triggerDeath('DROWN', 'Swept out of bounds!');
    }
  }

  // --- RENDER PIPELINE ---

  render() {
    this.ctx.save();
    this.ctx.scale(this.scale, this.scale);

    // Deep Garden Grass Base
    this.ctx.fillStyle = '#111b13';
    this.ctx.fillRect(0, 0, this.logicalWidth, this.logicalHeight);

    this.ctx.save();
    this.ctx.translate(0, this.cameraY);

    this.renderLanes();
    this.particles.forEach(p => p.draw(this.ctx));
    this.renderPlayer();
    this.floatingTexts.forEach(ft => ft.draw(this.ctx));

    this.ctx.restore();

    this.renderHUD();

    this.ctx.restore();
  }

  renderLanes() {
    const startIdx = Math.max(0, Math.floor(this.cameraY / this.laneHeight) - 2);
    const endIdx = Math.min(this.lanes.length, startIdx + Math.ceil(this.logicalHeight / this.laneHeight) + 4);

    for (let i = startIdx; i < endIdx; i++) {
      const lane = this.lanes[i];
      if (!lane) continue;

      const y = this.logicalHeight - (i + 1) * this.laneHeight;

      if (lane.type === 'SAFE') {
        this.ctx.fillStyle = (i % 2 === 0) ? '#2d5a27' : '#34622d';
        this.ctx.fillRect(0, y, this.logicalWidth, this.laneHeight);
      } else if (lane.type === 'ROAD') {
        this.ctx.fillStyle = '#22252a';
        this.ctx.fillRect(0, y, this.logicalWidth, this.laneHeight);

        // Caution Yellow Striping
        this.ctx.strokeStyle = '#f1c40f';
        this.ctx.lineWidth = 2;
        this.ctx.setLineDash([15, 15]);
        this.ctx.beginPath();
        this.ctx.moveTo(0, y + this.laneHeight / 2);
        this.ctx.lineTo(this.logicalWidth, y + this.laneHeight / 2);
        this.ctx.stroke();
        this.ctx.setLineDash([]);
      } else if (lane.type === 'RIVER') {
        this.ctx.fillStyle = '#1b4965';
        this.ctx.fillRect(0, y, this.logicalWidth, this.laneHeight);

        // Water Ripples
        this.ctx.fillStyle = '#62b6cb';
        const waveOffset = (lane.timer * 40) % 30;
        for (let wx = -30 + waveOffset; wx < this.logicalWidth; wx += 45) {
          this.ctx.fillRect(wx, y + 12, 16, 2.5);
          this.ctx.fillRect(wx + 20, y + 36, 12, 2.5);
        }
      } else if (lane.type === 'SPRINKLER') {
        this.ctx.fillStyle = '#264653';
        this.ctx.fillRect(0, y, this.logicalWidth, this.laneHeight);
      }

      // Items
      lane.items.forEach(item => {
        if (!item.collected) {
          const itemX = (item.gridX + 0.5) * this.colWidth;
          if (item.type === 'APPLE') this.drawApple(itemX, y + this.laneHeight / 2);
          else this.drawBeetle(itemX, y + this.laneHeight / 2);
        }
      });

      // Obstacles
      if (lane.type === 'RIVER') {
        lane.obstacles.forEach(log => this.drawLog(log.x, y + this.laneHeight / 2, log.width));
      } else if (lane.type === 'ROAD') {
        lane.obstacles.forEach(obs => {
          if (obs.type === 'MOWER') this.drawMower(obs.x, y + this.laneHeight / 2, obs.width);
          else this.drawCat(obs.x, y + this.laneHeight / 2);
        });
      } else if (lane.type === 'SPRINKLER') {
        lane.obstacles.forEach(spr => this.drawSprinkler(spr.x, y + this.laneHeight / 2, spr.radius, spr.active));
      }
    }
  }

  renderPlayer() {
    const p = this.player;
    this.ctx.save();

    const drawY = p.visualY - p.jumpZ;

    // Ground Shadow (Shrinks as Jump Z increases)
    const shadowScale = Math.max(0.4, 1 - p.jumpZ / 30);
    this.ctx.fillStyle = 'rgba(0,0,0,0.3)';
    this.ctx.beginPath();
    this.ctx.ellipse(p.visualX, p.visualY + p.radius * 0.4, p.radius * 0.8 * shadowScale, p.radius * 0.35 * shadowScale, 0, 0, Math.PI * 2);
    this.ctx.fill();

    this.ctx.translate(p.visualX, drawY);

    if (this.state === 'DEATH_ANIM') {
      if (p.deathType === 'DROWN') {
        this.ctx.scale(Math.max(0, this.deathTimer), Math.max(0, this.deathTimer));
      } else if (p.deathType === 'SQUISH') {
        this.ctx.scale(1.5, 0.25);
      }
    }

    if (p.isRolling) {
      // Rolling Ball Sprite
      this.ctx.fillStyle = '#6c584c';
      this.ctx.beginPath();
      this.ctx.arc(0, 0, p.radius, 0, Math.PI * 2);
      this.ctx.fill();

      this.ctx.strokeStyle = '#382b22';
      this.ctx.lineWidth = 4;
      for (let a = 0; a < Math.PI * 2; a += Math.PI / 3) {
        this.ctx.beginPath();
        this.ctx.moveTo(Math.cos(a) * 3, Math.sin(a) * 3);
        this.ctx.lineTo(Math.cos(a) * (p.radius + 4), Math.sin(a) * (p.radius + 4));
        this.ctx.stroke();
      }
    } else {
      // Cute Detailed Hedgehog
      // Body Quills
      this.ctx.fillStyle = (p.deathType === 'SOAKED') ? '#0077b6' : '#4a2810';
      this.ctx.beginPath();
      this.ctx.arc(0, 0, p.radius, 0, Math.PI * 2);
      this.ctx.fill();

      // Face Mask
      this.ctx.fillStyle = '#e3c290';
      this.ctx.beginPath();
      this.ctx.arc(0, -p.radius * 0.2, p.radius * 0.6, 0, Math.PI * 2);
      this.ctx.fill();

      // Ears
      this.ctx.fillStyle = '#cda36f';
      this.ctx.beginPath();
      this.ctx.arc(-p.radius * 0.5, -p.radius * 0.4, 4, 0, Math.PI * 2);
      this.ctx.arc(p.radius * 0.5, -p.radius * 0.4, 4, 0, Math.PI * 2);
      this.ctx.fill();

      // Eyes
      this.ctx.fillStyle = '#101010';
      this.ctx.beginPath();
      this.ctx.arc(-p.radius * 0.22, -p.radius * 0.35, 2.8, 0, Math.PI * 2);
      this.ctx.arc(p.radius * 0.22, -p.radius * 0.35, 2.8, 0, Math.PI * 2);
      this.ctx.fill();

      // Snout / Nose
      this.ctx.fillStyle = '#e63946';
      this.ctx.beginPath();
      this.ctx.arc(0, -p.radius * 0.55, 3.5, 0, Math.PI * 2);
      this.ctx.fill();
    }

    this.ctx.restore();
  }

  // --- DETAILED CARTOON VECTOR SPRITES ---

  drawLog(x, y, width) {
    this.ctx.fillStyle = '#6f432a';
    this.ctx.beginPath();
    this.ctx.roundRect(x, y - this.laneHeight * 0.35, width, this.laneHeight * 0.7, 10);
    this.ctx.fill();

    // Wood Bark Texture & Moss Patches
    this.ctx.fillStyle = '#8a5333';
    this.ctx.fillRect(x + 12, y - 4, width - 24, 8);

    this.ctx.fillStyle = '#52b788';
    this.ctx.beginPath();
    this.ctx.arc(x + 20, y - 10, 6, 0, Math.PI * 2);
    this.ctx.arc(x + width - 25, y + 8, 5, 0, Math.PI * 2);
    this.ctx.fill();
  }

  drawMower(x, y, width) {
    this.ctx.save();
    this.ctx.translate(x, y);

    // Mower Body
    this.ctx.fillStyle = '#e63946';
    this.ctx.beginPath();
    this.ctx.roundRect(0, -14, width, 28, 6);
    this.ctx.fill();

    // Wheels
    this.ctx.fillStyle = '#1d2d44';
    this.ctx.fillRect(4, -18, 8, 5);
    this.ctx.fillRect(width - 12, -18, 8, 5);
    this.ctx.fillRect(4, 13, 8, 5);
    this.ctx.fillRect(width - 12, 13, 8, 5);

    // Spinning Metallic Blade Hub
    this.ctx.fillStyle = '#f1faee';
    this.ctx.beginPath();
    this.ctx.arc(width / 2, 0, 7, 0, Math.PI * 2);
    this.ctx.fill();

    this.ctx.restore();
  }

  drawCat(x, y) {
    this.ctx.save();
    this.ctx.translate(x + 15, y);

    // Orange Tabby Cat
    this.ctx.fillStyle = '#f77f00';
    this.ctx.beginPath();
    this.ctx.arc(0, 0, 14, 0, Math.PI * 2);
    this.ctx.fill();

    // Pointy Ears
    this.ctx.beginPath();
    this.ctx.moveTo(-10, -8);
    this.ctx.lineTo(-4, -18);
    this.ctx.lineTo(-1, -8);
    this.ctx.fill();

    this.ctx.beginPath();
    this.ctx.moveTo(10, -8);
    this.ctx.lineTo(4, -18);
    this.ctx.lineTo(1, -8);
    this.ctx.fill();

    this.ctx.restore();
  }

  drawSprinkler(x, y, radius, active) {
    this.ctx.fillStyle = '#f4a261';
    this.ctx.beginPath();
    this.ctx.arc(x, y, 9, 0, Math.PI * 2);
    this.ctx.fill();

    if (active) {
      this.ctx.fillStyle = 'rgba(72, 202, 228, 0.45)';
      this.ctx.beginPath();
      this.ctx.arc(x, y, radius, 0, Math.PI * 2);
      this.ctx.fill();
    }
  }

  drawApple(x, y) {
    this.ctx.fillStyle = '#e63946';
    this.ctx.beginPath();
    this.ctx.arc(x, y, 10, 0, Math.PI * 2);
    this.ctx.fill();

    this.ctx.fillStyle = '#2a9d8f';
    this.ctx.beginPath();
    this.ctx.ellipse(x + 3, y - 10, 4, 2, Math.PI / 4, 0, Math.PI * 2);
    this.ctx.fill();
  }

  drawBeetle(x, y) {
    this.ctx.fillStyle = '#2a9d8f';
    this.ctx.beginPath();
    this.ctx.ellipse(x, y, 9, 6, 0, 0, Math.PI * 2);
    this.ctx.fill();

    this.ctx.fillStyle = '#e76f51';
    this.ctx.beginPath();
    this.ctx.arc(x + 6, y, 3, 0, Math.PI * 2);
    this.ctx.fill();
  }

  renderHUD() {
    // HUD Pill Badge
    this.ctx.fillStyle = 'rgba(13, 19, 14, 0.88)';
    this.ctx.beginPath();
    this.ctx.roundRect(16, 16, 170, 56, 14);
    this.ctx.fill();
    this.ctx.strokeStyle = '#2a9d8f';
    this.ctx.lineWidth = 2;
    this.ctx.stroke();

    this.ctx.fillStyle = '#ffffff';
    this.ctx.font = '900 16px sans-serif';
    this.ctx.fillText(`SCORE: ${this.score}`, 28, 38);

    this.ctx.fillStyle = '#ff4d4d';
    this.ctx.font = '700 12px sans-serif';
    this.ctx.fillText(`APPLES: ${this.apples}`, 28, 56);

    this.ctx.fillStyle = '#2a9d8f';
    this.ctx.fillText(`BEETLES: ${this.beetles}`, 105, 56);

    if (this.state === 'START') {
      this.renderBanner('HEDGEHOGGER', 'Walk over Red Apples & Green Beetles for big bonus points!', 'TAP TO PLAY');
    } else if (this.state === 'GAMEOVER') {
      this.renderBanner('GAME OVER!', this.deathCause || 'Try again!', `BEST SCORE: ${this.highScore}`);
    }
  }

  renderBanner(title, subtitle, action) {
    this.ctx.fillStyle = 'rgba(10, 15, 12, 0.85)';
    this.ctx.fillRect(0, 0, this.logicalWidth, this.logicalHeight);

    this.ctx.textAlign = 'center';

    this.ctx.fillStyle = '#1b2a1c';
    this.ctx.beginPath();
    this.ctx.roundRect(this.logicalWidth * 0.08, this.logicalHeight * 0.28, this.logicalWidth * 0.84, 250, 20);
    this.ctx.fill();
    this.ctx.strokeStyle = '#52b788';
    this.ctx.lineWidth = 3;
    this.ctx.stroke();

    this.ctx.fillStyle = '#52b788';
    this.ctx.font = '900 28px sans-serif';
    this.ctx.fillText(title, this.logicalWidth / 2, this.logicalHeight * 0.36);

    this.ctx.fillStyle = '#f4a261';
    this.ctx.font = '600 13px sans-serif';
    this.ctx.fillText(subtitle, this.logicalWidth / 2, this.logicalHeight * 0.43);

    // Controls Legend
    this.ctx.fillStyle = '#e9c46a';
    this.ctx.font = '500 12px sans-serif';
    this.ctx.fillText('Swipe Up/Down/Left/Right to Hop • Double Tap to Roll', this.logicalWidth / 2, this.logicalHeight * 0.48);

    this.ctx.fillStyle = '#e63946';
    this.ctx.beginPath();
    this.ctx.roundRect(this.logicalWidth * 0.22, this.logicalHeight * 0.54, this.logicalWidth * 0.56, 44, 14);
    this.ctx.fill();

    this.ctx.fillStyle = '#ffffff';
    this.ctx.font = 'bold 16px sans-serif';
    this.ctx.fillText(action, this.logicalWidth / 2, this.logicalHeight * 0.54 + 28);

    this.ctx.textAlign = 'start';
  }

  loop(timestamp) {
    const dt = Math.min((timestamp - this.lastTime) / 1000, 0.1);
    this.lastTime = timestamp;

    this.update(dt);
    this.render();

    requestAnimationFrame((ts) => this.loop(ts));
  }
}

window.addEventListener('load', () => {
  new Hedgehogger();
});