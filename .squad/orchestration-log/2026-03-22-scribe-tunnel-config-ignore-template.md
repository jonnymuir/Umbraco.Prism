# Orchestration Log — Scribe / Tunnel Config Ignore + Template

**Date:** 2026-03-22
**Agent:** Scribe
**Scope:** Coordinator-authored maintenance update for local tunnel config handling
**Outcome:** Completed and committed-ready for repository hygiene and onboarding clarity

---

## Summary

Implemented a small repository-maintenance change to prevent local tunnel config leakage while preserving a documented, committed template for team onboarding.

## Shipped Work

- Added `.prism_tunnel.conf` to `.gitignore`.
- Added explicit allow-list for `.prism_tunnel.conf.example` in `.gitignore`.
- Added committed `.prism_tunnel.conf.example` template file.
- Updated README config storage section to reference the committed template.

## Coordination Notes

- Change is maintenance-only and does not alter runtime behavior.
- Purpose is to reduce accidental local metadata commits and standardize setup guidance.
