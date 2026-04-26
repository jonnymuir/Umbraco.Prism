# Session Log: Biometric Tenant Toggle

**Date:** 2026-06-18

## Summary

Merged decision from `.squad/decisions/inbox/brewster-biometric-toggle.md` into `.squad/decisions.md`. Implemented per-tenant `AllowBiometricLogin` toggle as designed.

## Changes

- ✅ Database migration: `AddAllowBiometricLoginColumn` (idempotent)
- ✅ Domain models: PrismTenant, PrismTenantSchema, PrismTenantRequest
- ✅ API enforcement: BiometricController (Register + Exchange) → HTTP 403 when disabled
- ✅ Backoffice UI: Toggle switch in prism-create-tenant-modal (General tab)
- ✅ TenantManagementController: Field mapping in create + update
- ✅ TenantService: Schema→model projection

## Build Status

- ✅ Dotnet build: passing
- ✅ npm build: passing

## Decision Integration

Merged `.squad/decisions/inbox/brewster-biometric-toggle.md` → `.squad/decisions.md`. Deleted inbox file.
