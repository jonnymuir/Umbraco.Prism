# Orchestration Log — Blathers / Tunnel Input Clarity

**Date:** 2026-03-22
**Agent:** Blathers
**Scope:** Clarify local tunnel helper inputs and tenant selection workflow
**Outcome:** Completed and handed off for decision merge and documentation traceability

---

## Summary

Blathers refined `scripts/dev/start-trycloudflare.sh` to make Entra app input naming explicit and tenant targeting safer for operators.

## Shipped Work

- Replaced canonical script input key naming from `ENTRA_APP_OBJECT_ID` to `ENTRA_APP_CLIENT_ID`.
- Added backward compatibility path to read legacy key when needed and persist canonical key on save.
- Expanded tenant input UX to accept tenant name or numeric id.
- Added tenant selector resolution with explicit duplicate/no-match failure handling.
- Updated summary output to include resolved tenant id and tenant name.

## Documentation

- Updated README local tunnel section to explain:
  - Entra Application (Client) ID requirement.
  - Tenant selector behavior (name or numeric id).
  - Legacy config key compatibility.

## Decision Record

- Decision proposal merged from `.squad/decisions/inbox/blathers-tunnel-input-clarity.md` into `.squad/decisions.md`.
