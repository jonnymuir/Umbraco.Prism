# Warning-Free Build Policy

## Decision

Umbraco.Prism should aim for warning-free restore, build, and test runs. Warnings such as `NU1603` are not acceptable background noise; they should be fixed in the smallest safe way or explicitly documented with the exact blocker and owner.

## Applied Here

- Added squad guidance to `.squad/team.md`.
- Added reusable convention guidance to `.squad/skills/project-conventions/SKILL.md`.
- Eliminated the current `NU1603` warnings by pinning `OpenTelemetry.Instrumentation.AspNetCore` and `OpenTelemetry.Instrumentation.Http` to `1.12.0` in `src/UmbracoPrism.ServiceDefaults/UmbracoPrism.ServiceDefaults.csproj`, matching the version NuGet was already resolving.

## Follow-up

The repo still has non-`NU1603` build warnings (including `NU1902`, `CS0618`, and `MVC1000`) that should be treated as future cleanup work to reach the broader warning-free target.
