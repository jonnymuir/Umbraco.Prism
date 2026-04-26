# Session Log: Mobile Nav Seeder Deterministic Fix

**Date:** 2026-06-17  
**Agent:** Brewster (Umbraco Platform Specialist)  
**Commit:** f305547

## Summary

Fixed mobile nav seeder crash with deterministic GUID pattern. Settings node now creates with correct MultiUrlPicker data type via fixed-GUID lookup, property remove+re-add pattern, ILogger instrumentation, and guard against publish failure.

## Key Decisions Merged

1. Deterministic fixed GUID for Prism Mobile Nav data type (`3b4c5d6e-7f80-9a1b-c2d3-e4f567890abc`)
2. Remove + re-add pattern for property migration (not in-place mutation)
3. Added ILogger to both seeders for diagnostics
4. Guard pattern prevents crash: publishes Settings empty if data type mismatch detected

## Learnings

- `dataTypeService.GetAsync(Guid key)` is reliable lookup when GUID is fixed
- In-place `DataTypeKey` mutation leaves integer `DataTypeId` stale — remove + re-add instead
- `dataTypeService.DeleteAsync` silently fails if data type is in use — check Attempt result
- Re-fetch content type from DB after structural changes to avoid cache stale state

See decisions.md for full rationale and technical details.
