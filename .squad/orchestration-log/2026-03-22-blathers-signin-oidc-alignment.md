# Orchestration Log — Blathers / Signin OIDC Callback Alignment

**Date:** 2026-03-22
**Agent:** Blathers
**Scope:** Align local tunnel redirect callback path with Prism OIDC runtime callback path
**Outcome:** Completed and handed off for scribe logging and commit

---

## Summary

Blathers aligned tunnel helper output and docs to Prism's OIDC callback endpoint so local dev Entra redirect entries match runtime auth expectations.

## Shipped Work

- Set tunnel helper callback path to `/signin-oidc` in `scripts/dev/start-trycloudflare.sh`.
- Updated README tunnel redirect URI references/examples from `/umbraco/oauth_complete` to `/signin-oidc`.
- Recorded handoff details in `.squad/agents/blathers/history.md`.

## Validation

- `bash -n scripts/dev/start-trycloudflare.sh` passed.

## Handoff Notes

- This was a path-alignment/correctness batch focused on reducing redirect mismatch risk during local tunnel auth testing.
