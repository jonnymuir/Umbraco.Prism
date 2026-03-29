# Decision: Deterministic GUID for Prism Custom Data Types

**Date:** 2026-06-17
**Author:** Brewster (Umbraco Platform Specialist)

## Decisions

### 1. Use deterministic fixed GUIDs for all Prism-owned data types

When creating custom data types in Prism seeders, assign a project-specific fixed GUID by setting `Key = <fixed Guid>` on the `DataType` instance before calling `dataTypeService.CreateAsync(...)`.

This allows reliable idempotent lookup via `dataTypeService.GetAsync(key)` across installs and upgrades, without depending on name-based search which is fragile.

**Fixed GUID for Prism Mobile Nav Links:** `3b4c5d6e-7f80-9a1b-c2d3-e4f567890abc`

### 2. Use remove + re-add for property type migration, not in-place DataTypeKey mutation

When a content type property is found using the wrong data type, do NOT mutate `existingProperty.DataTypeKey` in-place. Instead:
1. `contentType.RemovePropertyType(alias)` and save
2. Re-fetch the content type from DB: `contentTypeService.Get(alias)`
3. Fall through to create the property fresh with the correct `PropertyType(shortStringHelper, newDataType, alias)` constructor

This ensures Umbraco's internal integer `DataTypeId` is set correctly, not just the GUID key.

## Lessons

### `dataTypeService.DeleteAsync` silently fails when a data type is in use

Umbraco blocks deletion of data types that are referenced by content types at the DB level. The `Attempt<>` result carries the failure but if the caller ignores it, code silently continues with the old data type still in place. Always check and log the `Attempt` result.

### In-place `DataTypeKey` mutation on `PropertyType` is unreliable

`PropertyType` stores both `DataTypeKey` (GUID) and `DataTypeId` (int). Setting only the GUID via the setter does not update the integer ID used internally by Umbraco for validation lookup. The property validation still uses the old data type, causing JSON deserialization errors at publish time (as seen with MultiNodeTreePicker vs MultiUrlPicker).

### Re-fetch content type from DB after structural changes

After removing a property type and saving, re-fetch the content type from the database (`contentTypeService.Get(alias)`) to get a clean, cache-free object before adding properties. Operating on a stale in-memory object after structural changes can cause inconsistent state.

### Guard pattern prevents startup crash

A GUID comparison guard in `EnsureSettingsDefaults` (`mobileNavProperty.DataTypeKey != expectedDataTypeKey`) allows the seeder to safely save/publish an empty Settings node rather than crashing with a JSON deserialization exception. The user can fill in nav links manually via the backoffice.
