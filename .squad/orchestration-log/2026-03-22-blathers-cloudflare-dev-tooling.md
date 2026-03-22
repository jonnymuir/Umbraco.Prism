# Orchestration Log — Blathers / Cloudflared Dev Tooling

**Date:** 2026-03-22
**Agent:** Blathers
**Scope:** Local development automation for temporary public callback setup
**Outcome:** Completed and handed off for security hardening pass

---

## Summary

Blathers implemented a local helper script to automate temporary Cloudflare tunnel setup and synchronize callback/routing settings required for local Prism tenant auth testing.

## Shipped Work

- Added `scripts/dev/start-trycloudflare.sh` as the local automation entrypoint.
- Added script flow for:
  - Starting `cloudflared` quick tunnel.
  - Reading tunnel URL.
  - Deriving callback URL with `/umbraco/oauth_complete`.
  - Updating Entra redirect URI.
  - Updating selected tenant hostname in SQLite.
- Added support for repo-local `.prism_tunnel.conf` configuration.
- Added lifecycle trap handling and temporary artifact cleanup.

## Documentation

- Added README instructions for usage and operator flow.

## Decision Record

- Decision proposal merged from `.squad/decisions/inbox/blathers-cloudflare-dev-tooling.md` into `.squad/decisions.md`.
