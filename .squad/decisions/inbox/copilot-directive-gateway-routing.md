### 2026-06-06T15:37:41Z: Gateway routing rules (architectural constraint)

**By:** jonnymuir (via Copilot)

**What:** Enforce strict gateway routing validation:
- Routes FROM states must ALWAYS go to a gateway (never direct state→state)
- Routes FROM gateways can go to gateways OR states
- This is the ONLY path: state → gateway → (gateway|state) → ...

**Why:** Architectural requirement for workflow logic correctness. All three non-payment workflows currently violate this (missing gateways). Validation must prevent invalid configurations.
