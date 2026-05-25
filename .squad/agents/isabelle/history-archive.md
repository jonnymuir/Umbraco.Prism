# Isabelle History Archive

## 2026-05-19 to 2026-05-24 — Prior Sessions

Previous sessions (2026-05-19 to 2026-05-24) detailed work on:
- Lane header clearance and viewport width regression fixes (2026-05-23)
- Graph layout overflow and scroll container implementation (2026-05-23)
- Visual regression platform baseline fixes (2026-05-24)
- CI test drift resolution for walkthrough and visual regression (2026-05-24)
- Behavioral test conversion from pixel-perfect to user-action verification (2026-05-24)

Full session records archived but not repeated here. Scope: shell cohesion, graph viewport sizing, visual baseline management, walkthrough CI alignment, and behavioral test methodology.

### Key Learnings from Prior Sessions
- Platform-specific baselines add maintenance burden; deterministic font setup enables single baseline across platforms
- Visual baselines regenerated locally must be committed immediately; drift accumulates when baselines lag layout changes
- Behavioral assertions (what users can DO) are more robust than pixel-perfect snapshots for cross-platform testing
- Test hooks (`data-prism-*` attributes) improve test robustness over magic CSS selectors
