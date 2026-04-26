# Session: Aspire Workload Permissions on macOS

**Timestamp:** 2026-04-12T01:29:29Z

## Overview

Blathers completed investigation and fix for `dotnet workload install aspire` failing with inadequate permissions on macOS with protected SDK installation.

## Outcome

- Root cause identified: .NET SDK at `/usr/local/share/dotnet` owned by `root:wheel`
- Solution: Document elevated command path (`sudo dotnet workload install aspire`)
- Validation: Updated preflight messages guide users correctly
- Docs: README, ASPIRE_DEV.md, and prereq validator updated

## Key Decision

Aspire workload installation requires `sudo` on macOS when the SDK is in a protected directory—this is now documented and checked proactively.
