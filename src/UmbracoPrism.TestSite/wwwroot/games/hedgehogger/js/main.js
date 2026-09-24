import { HedgehoggerGame } from './engine.js';
import { LevelHub } from './hub.js';
import { LEVEL_1 } from './level1.js';
import { LEVEL_2 } from './level2.js';
import { LEVEL_3 } from './level3.js';

// Ordered level roster. The hub is the game's home screen — it shows every
// level's lock state and best score/time (read straight from localStorage)
// plus one trailing "coming soon" tile, and hands the chosen id to the
// engine. Completing OR dying out of a level returns to the hub rather than
// auto-advancing, so the player always chooses what's next: replay an
// already-beaten level to improve their score, or move on.
const LEVELS = [LEVEL_1, LEVEL_2, LEVEL_3];

window.addEventListener('load', () => {
  const canvas = document.getElementById('gameCanvas');
  const mapLink = document.getElementById('map-link');

  const showHub = () => {
    game.deactivate();
    mapLink.hidden = true;
    hub.show();
  };

  const hub = new LevelHub(canvas, LEVELS, (levelId) => {
    hub.hide();
    mapLink.hidden = false;
    game.startLevel(levelId);
  });

  const game = new HedgehoggerGame(canvas, LEVELS, { onExit: showHub });

  mapLink.addEventListener('click', (e) => {
    e.preventDefault();
    showHub();
  });

  // A single shared resize handler dispatches to whichever of hub/game
  // currently owns the canvas — both would otherwise fight over sizing it.
  window.addEventListener('resize', () => (hub.active ? hub : game).resize());

  showHub();
});
