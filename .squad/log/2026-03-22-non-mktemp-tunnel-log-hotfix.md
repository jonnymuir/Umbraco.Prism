# Session Log — Non-mktemp Tunnel Log Hotfix

**Date:** 2026-03-22
**Session:** Replace `mktemp` tunnel log creation with writable-probe + unique filename fallback chain
**Requested by:** Jonny Muir
**Agent:** Scribe

---

## Summary

This batch replaces `mktemp`-based tunnel log creation with direct writable-probe logic and deterministic unique filename creation across a robust directory fallback chain.

## What Shipped

- Replaced `mktemp` tunnel log creation with a direct write probe plus unique filename creation flow.
- Added directory candidate chain for tunnel logs:
  - `artifacts/logs/trycloudflared`
  - `${TMPDIR}` path
  - `/tmp` path
  - `${HOME}` cache path
- Improved failure diagnostics when no candidate directory is writable/usable.
- Updated `README.md` to document the expanded fallback chain.
- Validation passed: `bash -n scripts/dev/start-trycloudflare.sh`.

## Files Touched In This Batch

- `scripts/dev/start-trycloudflare.sh`
- `README.md`
- `.squad/agents/blathers/history.md`
- `.squad/log/2026-03-22-non-mktemp-tunnel-log-hotfix.md`
- `.squad/orchestration-log/2026-03-22-blathers-non-mktemp-tunnel-log-hotfix.md`

## Validation Expectations

- Tunnel log file creation succeeds without relying on `mktemp`.
- Candidate directories are evaluated in documented order.
- Startup emits actionable diagnostics when all candidate directories fail.
- README behavior notes match runtime behavior.
