# Followups & Hygiene Items

Umbraco.Prism squad hygiene backlog. Low-priority items that don't block feature work.

---

## 📋 Tracked Build Outputs (Low Priority)

**Date:** 2026-04-30  
**Category:** Source Control Hygiene  
**Severity:** Low (cosmetic, no security risk)

### Issue

Build output files are tracked in git:
- `src/UmbracoPrism.TestSite/bin/Debug/net10.0/appsettings.json`
- `src/UmbracoPrism.TestSite/bin/Release/net10.0/appsettings.json`

These `.gitignore` entries should block `bin/` and `obj/` from all projects, but they're in the repository.

### Context

Identified during SEC-004 remediation investigation (2026-04-30). SEC-004 itself is unrelated — it's a separate hygiene observation that the `bin/` directory is being tracked at all.

### Recommended Fix

1. Add (or verify) `.gitignore` entries to exclude `bin/` and `obj/` from source control
2. Run `git rm --cached src/UmbracoPrism.TestSite/bin/ src/UmbracoPrism.TestSite/obj/` (or equivalent for all projects)
3. Verify `.gitignore` covers the pattern

### Note

Deferred from SEC-004 closure because it's a separate hygiene matter (not part of the secrets remediation). Does not impact security posture.

---
