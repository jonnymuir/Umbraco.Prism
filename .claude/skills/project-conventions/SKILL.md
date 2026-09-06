---
name: "project-conventions"
description: "Core conventions and patterns for this codebase"
domain: "project-conventions"
confidence: "medium"
source: "template"
---

## Context

Core conventions that repeatedly affect delivery quality in this repo.

## Patterns

### Warning-Free Builds

- Treat restore, build, and test warnings as actionable work.
- Do not normalize NuGet restore warnings such as `NU1603`; fix the package reference/version mismatch or document the exact blocker in squad decision artifacts.
- Before handoff, run the existing solution build/test commands and record whether warnings were removed or deliberately deferred.

### Error Handling

<!-- Example: How does your project handle errors? -->
<!-- - Use try/catch with specific error types? -->
<!-- - Log to a specific service? -->
<!-- - Return error objects vs throwing? -->

### Testing

- .NET validation runs through `dotnet build UmbracoPrism.slnx` and `dotnet test UmbracoPrism.slnx`.
- Core automated tests currently live in `src/UmbracoPrism.Core.Tests`.

### Code Style

<!-- Example: Linting, formatting, naming conventions -->
<!-- - Linter: ESLint config? -->
<!-- - Formatter: Prettier? -->
<!-- - Naming: camelCase, snake_case, etc.? -->
- Prefer small, surgical fixes that align with existing package and framework choices.
- For dependency cleanup, update only the references required to remove the warning unless compatibility forces a broader upgrade.

### File Structure

- `src/` contains the .NET projects, Aspire AppHost, service defaults, test site, and test suite.
- `.squad/` contains team guidance, decisions, agent history, and reusable skills.

## Examples

```xml
<PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.12.0" />
```

## Anti-Patterns

- **Ignoring restore/build warnings** — Warning debt hides real regressions and makes future upgrades harder to reason about.
