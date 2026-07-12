#!/usr/bin/env node
// Builds the "v2" scored cut of the garden-waste-permit demo: the plain recording
// (demo-footage/garden-waste-permit-demo.webm) with Debussy's Rêverie muxed on top as a backing
// track. No narration — v2 is music-only by design (voiced narration was tried and pulled; revisit
// once a real TTS API is wired in, see tests/demo/README.md).
//
// Not part of `npm run demo:record` — a separate, manual post-production pass:
//   node scripts/build-scored-demo.mjs
//
// scripts/assets/debussy-reverie.mid is a MIDI sequence of Debussy's Rêverie (1890) — the
// composition itself is unambiguously public domain (Debussy died 1918). Sequence by Dario
// Galimberti, sourced from kunstderfuge.com (a long-running classical-MIDI archive that describes
// its files as "authorized and free"). Rendered locally to audio with fluidsynth against a bundled
// GM soundfont — this file is our own render of a public-domain score, not a copy of any particular
// recorded performance.
//
// Requires: `ffmpeg`/`ffprobe`, `fluidsynth` (brew install fluid-synth — also installs the
// VintageDreamsWaves-v2.sf2 soundfont this script renders with) on PATH.

import { execFileSync } from 'node:child_process';
import { mkdtempSync, rmSync, existsSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const footageDir = path.join(__dirname, '..', 'demo-footage');
const sourceWebm = path.join(footageDir, 'garden-waste-permit-demo.webm');
const outWebm = path.join(footageDir, 'garden-waste-permit-demo2.webm');
const outMp4 = path.join(footageDir, 'garden-waste-permit-demo2.mp4');
const reverieMidi = path.join(__dirname, 'assets', 'debussy-reverie.mid');

function run(cmd, args) {
  return execFileSync(cmd, args, { encoding: 'utf8' });
}

function ffprobeDurationSeconds(file) {
  const out = run('ffprobe', [
    '-v', 'error', '-show_entries', 'format=duration', '-of', 'default=noprint_wrappers=1:nokey=1', file
  ]);
  return parseFloat(out.trim());
}

if (!existsSync(sourceWebm)) {
  console.error(`Missing ${sourceWebm} — run npm run demo:record first.`);
  process.exit(1);
}
if (!existsSync(reverieMidi)) {
  console.error(`Missing ${reverieMidi}.`);
  process.exit(1);
}

const videoDurationSec = ffprobeDurationSeconds(sourceWebm);
console.log(`Source video: ${videoDurationSec.toFixed(1)}s.`);

const workDir = mkdtempSync(path.join(tmpdir(), 'prism-score-'));
console.log(`Working dir: ${workDir}`);

// --- Render the MIDI to audio with fluidsynth -----------------------------------------------
// `-a file` is required, not just `-F` — without it fluidsynth tries to open a live realtime
// audio driver first and blocks indefinitely in a headless environment with no device to open
// (confirmed live: a plain `-F` render sat at ~0% CPU for minutes; adding `-a file` fixed it
// immediately). VintageDreamsWaves-v2.sf2 ships with the fluid-synth brew formula itself
// specifically for this kind of quick render/test — no separate soundfont download needed.
const soundfontDir = run('brew', ['--prefix', 'fluid-synth']).trim();
const soundfont = path.join(soundfontDir, 'share', 'fluid-synth', 'sf2', 'VintageDreamsWaves-v2.sf2');
const reverieRender = path.join(workDir, 'reverie.wav');
run('fluidsynth', ['-ni', '-a', 'file', '-F', reverieRender, '-r', '44100', soundfont, reverieMidi]);
console.log('Rêverie rendered from MIDI.');

const reverieDurationSec = ffprobeDurationSeconds(reverieRender);
console.log(`Rêverie render: ${reverieDurationSec.toFixed(1)}s — looping to cover the full video.`);

// --- Loop/trim to the video's length, boost level, fade out at the very end -------------------
// A hard loop (no crossfade) back to the piece's own soft opening — simple, and Rêverie's own
// quiet opening/closing bars make the seam considerably less jarring than a hard-looped upbeat
// track would be. `-stream_loop -1` repeats the single input indefinitely; `-t` trims the result
// to the exact video length regardless of where in a repetition that lands.
const backingTrack = path.join(workDir, 'backing.wav');
run('ffmpeg', [
  '-y',
  '-stream_loop', '-1', '-i', reverieRender,
  '-t', String(videoDurationSec),
  // A flat `volume=` gain (the previous synthesized-track approach) clips here: piano recordings
  // have a much wider dynamic range than a synth pad/drone, so a gain picked to raise the quiet
  // *average* level pushes the loud note-attack *peaks* well past 0dBFS (confirmed live via
  // astats — max level 1.14, i.e. real clipping, not just a hot but valid signal). `loudnorm`
  // (EBU R128) normalizes perceived loudness to a target instead of applying one blanket
  // multiplier, with its own true-peak limiter (TP) keeping transients safely under 0dBFS — the
  // right tool for programme material with real dynamics.
  '-af', `afade=t=out:st=${Math.max(0, videoDurationSec - 3)}:d=3,loudnorm=I=-16:TP=-1.5:LRA=11`,
  backingTrack
]);
console.log('Backing track assembled.');

// --- Mux the backing track onto a copy of the silent video --------------------------------------
run('ffmpeg', [
  '-y',
  '-i', sourceWebm,
  '-i', backingTrack,
  '-map', '0:v', '-map', '1:a',
  '-c:v', 'copy', '-c:a', 'libopus', '-shortest', outWebm
]);
console.log(`Wrote ${outWebm}`);

try {
  run('ffmpeg', [
    '-y', '-i', outWebm, '-c:v', 'libx264', '-preset', 'medium', '-crf', '18', '-c:a', 'aac', outMp4
  ]);
  console.log(`Wrote ${outMp4}`);
} catch {
  console.log('ffmpeg mp4 conversion failed — the .webm is still the real output.');
}

rmSync(workDir, { recursive: true, force: true });
console.log('Done.');
