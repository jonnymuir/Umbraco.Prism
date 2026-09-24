/**
 * Shared helpers for authoring deterministic level data — no randomness, just
 * arithmetic, so every level file stays hand-reasoned-about and replayable.
 */

// Even spacing for obstacles that loop across a lane of width `w`, wrapping
// at +/-`margin`. Fully deterministic: same inputs, same layout, every time.
export function evenlySpaced(count, width, margin, phaseOffset = 0) {
  const cycle = width + margin * 2;
  const spacing = cycle / count;
  const out = [];
  for (let k = 0; k < count; k++) {
    let x = k * spacing - margin + phaseOffset;
    if (x > width + margin) x -= cycle;
    out.push(x);
  }
  return out;
}
