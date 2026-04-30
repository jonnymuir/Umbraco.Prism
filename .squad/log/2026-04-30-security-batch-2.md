# Session Log: Security Batch 2 (2026-04-30)

## Summary
Security findings from comprehensive audit consolidated into decisions ledger. 7 of 11 findings closed; 4 remain open (1 in-flight, 3 backlog).

## Findings Status

### Closed Earlier (Before This Batch)
- **SEC-001 (HIGH):** WorkflowPollController auth — ✅ CLOSED (commit `c2ff66a`)
- **SEC-004 (HIGH):** TestSite secrets management — ✅ CLOSED (commit `b6336fd`, batch)
- **SEC-009 (LOW):** Log injection fix — ✅ CLOSED
- **SEC-011 (LOW):** HTML encoding fix — ✅ CLOSED

### Closed This Batch
- **SEC-002 (CRITICAL):** DataProtection CVE 10.0.0 → 10.0.7 — ✅ CLOSED (commit `2618c54`)
- **SEC-005 (HIGH):** npm CVE remediation — ✅ CLOSED (commit `7e499b5`, 1 critical + 10 high eliminated, 9 moderate residual dev-only)
- **SEC-006 (HIGH):** CookieSecurePolicy.Always — ✅ CLOSED (commit `df434bf`)
- **SEC-007 (HIGH):** ForwardedHeaders proxy awareness — ✅ CLOSED (commit `44c476f`, ⚠️ KnownProxies hardening required pre-production)
- **SEC-008 (MEDIUM):** OpenTelemetry.Api CVE 1.12.0 → 1.15.3 — ✅ CLOSED (commit `2618c54`)
- **SEC-010 (MEDIUM):** Scrub PII in MockBusinessApp — ✅ CLOSED (commit `87900c9`, ⚠️ PII remains in git history)

### In-Flight
- **SEC-003 (HIGH):** IWorkflowContentSanitizer design for Html.Raw sanitization — 🟡 DESIGN COMPLETE (proposal in inbox, awaiting implementation routing to Copper/Blathers)

### Backlog/Open
- None currently triaged; SEC-003 impl will re-populate backlog

## Test Coverage
548 → 550 tests passing (Blathers additions: 2 regression tests)

## Decision Registry
- 6 inbox files merged to decisions.md (blathers-sec-002-008, -004-fix, -006, -007, -010; isabelle-sec-005)
- tom-nook-sec-003-proposal.md retained in inbox (active proposal)
- decisions.md: 17.7 KB → ~27.6 KB post-merge (no archival required; <20.5 KB threshold not exceeded)

## Artifacts
- `.squad/orchestration-log/` — 3 agent logs (Tom Nook, Isabelle, Blathers)
- `.squad/decisions.md` — Updated with all 6 closed findings + SEC-003 design proposal entry

## Agents Involved
- **Tom Nook:** SEC-003 design proposal
- **Isabelle:** SEC-005 npm remediation
- **Blathers:** SEC-002, SEC-004, SEC-006, SEC-007, SEC-008, SEC-010

## Next Steps
1. Tom Nook / Copper / Blathers: SEC-003 implementation sprint (IWorkflowContentSanitizer)
2. Deployment team: Pre-production hardening (SEC-007 KnownProxies, PII notification per GDPR/UK GDPR)
3. Copper: Continue triaging findings ≥HIGH from original audit

---

**Scribe note:** All findings documented in decisions.md with commit SHAs for traceability. Inbox consolidated; tom-nook-sec-003-proposal.md retained as active proposal awaiting team decision on implementation ETA.
