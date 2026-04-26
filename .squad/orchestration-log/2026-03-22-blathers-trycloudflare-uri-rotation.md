# Orchestration Log — Blathers / trycloudflare URI Rotation Safety

**Date:** 2026-03-22
**Agent:** Blathers
**Scope:** Prevent stale trycloudflare callback URI accumulation while preserving non-trycloudflare redirect URIs
**Outcome:** Completed and handed off for decision merge, session logging, and commit

---

## Summary

Blathers updated local tunnel redirect URI handling to safely rotate ephemeral trycloudflare callback entries and avoid destructive mutation of stable redirect URIs.

## Shipped Work

- Added trycloudflare callback detection for Prism callback path entries.
- Pruned stale `*.trycloudflare.com/signin-oidc` redirect URIs before final update.
- Preserved all non-trycloudflare redirect URIs unchanged.
- Ensured the current tunnel callback URI exists exactly once.
- Added concise script output summarizing stale callback prune count.

## Documentation

- Updated README local tunnel documentation with redirect URI rotation behavior.
- Updated README local auth guidance to recommend `az login --allow-no-subscriptions` for dev tenant auth context selection.

## Decision Record

- Decision proposal merged from `.squad/decisions/inbox/blathers-trycloudflare-uri-rotation.md` into `.squad/decisions.md`.
