# Session Log — Signin OIDC Callback Alignment

**Date:** 2026-03-22
**Session:** Align tunnel redirect URI with Prism OIDC callback path
**Requested by:** Jonny Muir
**Agent:** Scribe

---

## Summary

This batch aligned local tunnel redirect behavior and documentation with Prism's actual OIDC callback endpoint.

Blathers updated local tunnel automation to emit redirect URIs using `/signin-oidc` and updated README tunnel guidance/examples to match runtime callback behavior. This removes callback-path drift between helper tooling and Prism authentication configuration.

## What Shipped

- Updated `scripts/dev/start-trycloudflare.sh` callback constant to `/signin-oidc`.
- Ensured generated Entra redirect URI from tunnel flow uses `<tunnel-url>/signin-oidc`.
- Updated README tunnel automation documentation and redirect examples to `/signin-oidc`.

## Files Touched In This Batch

- `scripts/dev/start-trycloudflare.sh`
- `README.md`
- `.squad/agents/blathers/history.md`
- `.squad/log/2026-03-22-signin-oidc-alignment.md`
- `.squad/orchestration-log/2026-03-22-blathers-signin-oidc-alignment.md`

## Validation

- Passed shell syntax validation:
  - `bash -n scripts/dev/start-trycloudflare.sh`

## Notes

- Objective was consistency and correctness of redirect callback path, not behavioral expansion of tunnel tooling.
