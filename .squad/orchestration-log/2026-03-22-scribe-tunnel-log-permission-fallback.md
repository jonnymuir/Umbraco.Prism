# Orchestration Log — Scribe / Tunnel Log Permission Fallback

**Date:** 2026-03-22
**Agent:** Scribe
**Scope:** Ensure temporary trycloudflared logs always use a writable location with documented fallback behavior
**Outcome:** Completed and commit-ready

---

## Summary

Implemented a reliability-focused maintenance update for tunnel startup logging by introducing writable-directory fallback logic and documenting runtime behavior for stop/cleanup workflows.

## Shipped Work

- Added writable-directory selection to `start-trycloudflare.sh`:
  - preferred: `artifacts/logs/trycloudflared`
  - fallback: `${TMPDIR:-/tmp}/prism-trycloudflared-logs`
- Added summary output line indicating which tunnel log directory was selected.
- Updated README stop/cleanup section to describe fallback behavior.
- Confirmed shell syntax validity with `bash -n scripts/dev/start-trycloudflare.sh`.

## Coordination Notes

- Change is defensive and operational; no product-feature behavior changed.
- Objective is to prevent permission-related tunnel startup failures caused by unwritable artifact paths.
- Documentation now reflects both preferred and fallback log-path behavior for local development.
