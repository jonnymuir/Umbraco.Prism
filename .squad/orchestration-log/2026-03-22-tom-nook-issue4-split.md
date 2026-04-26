# Orchestration Log — Tom Nook / Issue #4 Split

**Date:** 2026-03-22  
**Agent:** Tom Nook  
**Issue:** #4 — Standardize authorization model (Entra vs. Umbraco groups)  
**Outcome:** ✅ Issue split into three child issues (#8, #9, #10); decision written to inbox

---

## Summary

Tom Nook performed a deep-read of the authorization layer and confirmed that Entra token claims must become the single source of truth for all Prism authorization decisions. Issue #4 was decomposed into three sequenced child issues with hard delivery gates.

## Child Issues Created

| GH Issue | Title | Owner | Status |
|----------|-------|-------|--------|
| #8 | Auth compatibility mode — Entra claim evaluation + Umbraco fallback | squad:tom nook | Open |
| #9 | Auth policy test suite — PrismAdminHandler + PrismTenantHandler | squad:blathers | Open |
| #10 | Auth fallback removal — breaking change after adoption window | squad:tom nook | Open — blocked on #8, #9, one release cycle |

## Decision Written

**File:** `.squad/decisions/inbox/tom-nook-auth-split.md` (merged to decisions.md by Scribe)

## Key Findings

- `PrismTenantHandler` already uses Entra `tid` claim — **no changes needed**.
- `PrismAdminHandler` checks Umbraco local backoffice groups via `IBackOfficeSecurityAccessor` — **this is the migration target**.
- Split trust root creates a permission-drift vector: Entra group removal does not automatically update Umbraco local group state.

## Delivery Sequence

1. **#8 (compatibility)** → Introduce Entra claim path in `PrismAdminHandler` with optional Umbraco fallback (on by default). Fallback emits warning log on every fire. Startup validation for strict mode. Deprecation warning in `PrismComposer` when old `GroupAliases` config exists without new Entra claim config.
2. **#9 (tests)** → Full XUnit coverage of both handlers, including claim combinations and fallback toggle permutations. Can begin authoring once #8 options shape is finalized.
3. **#10 (removal)** → Remove `IBackOfficeSecurityAccessor` dependency, `GroupAliases`, `EnableUmbracoGroupFallback`. Breaking change. **Blocked until:** #8 deployed, #9 CI-green, one release cycle with zero fallback log fires.

## `PrismAdminOptions` Shape (target, for #8)

```csharp
public class PrismAdminOptions
{
    // Existing (deprecated in #8, removed in #10)
    public string[] GroupAliases { get; set; } = ["admin"];

    // New in #8
    public string EntraAdminClaimType { get; set; } = "roles";
    public string[] EntraAdminClaimValues { get; set; } = [];
    public bool StrictEntraMode { get; set; } = false;
    public bool EnableUmbracoGroupFallback { get; set; } = true;
}
```

## Migration Safety Guardrails

1. `Prism:AdminGroups:GroupAliases` continues to work in #8 — no breaking change.
2. Warning log on every fallback activation provides visibility.
3. `StrictEntraMode: true` without `EntraAdminClaimValues` configured → `InvalidOperationException` on startup.
4. #10 shipping gate written directly into issue body — not rely on process memory.
