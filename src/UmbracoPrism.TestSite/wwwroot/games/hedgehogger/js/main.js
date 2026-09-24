import { HedgehoggerGame } from './engine.js';
import { LEVEL_1 } from './level1.js';
import { LEVEL_2 } from './level2.js';

// Ordered level roster — the engine resumes whichever level localStorage
// says was last played, and unlocks the next one in this order on win.
const LEVELS = [LEVEL_1, LEVEL_2];

window.addEventListener('load', () => {
  const canvas = document.getElementById('gameCanvas');
  new HedgehoggerGame(canvas, LEVELS);
});
