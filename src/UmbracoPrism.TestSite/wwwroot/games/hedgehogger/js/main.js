import { HedgehoggerGame } from './engine.js';
import { LEVEL_1 } from './level1.js';

window.addEventListener('load', () => {
  const canvas = document.getElementById('gameCanvas');
  new HedgehoggerGame(canvas, LEVEL_1);
});
