# Blathers — Archived History (Pre-2026-05-16)

This archive contains detailed work logs prior to the V1 design cycle and extraction slice.

## Transport Diagnostics & Downstream Demo Fixes (2026-05-03 → 2026-05-04)

- ✅ Implemented response-visible transport diagnostics for downstream API calls
- ✅ Fixed workflow API backchannel URL resolution in Codespaces
- ✅ Diagnosed JWKS backchannel escape as root cause of auth timeouts
- ✅ Added logging for null auth headers in workflow clients
- ✅ Aligned workflow handlers to `Results.Problem()` for consistency
- ✅ Fixed `PrismContextTests` race condition via `EnvVarSensitiveTestCollection`

**Key Learnings:**
- Named HttpClients must be registered via AddHttpClient() even when timeout is managed via CancellationToken
- Any test class reading `KEYCLOAK_BACKCHANNEL_URL` or `ASPNETCORE_ENVIRONMENT` must use `EnvVarSensitiveTestCollection`
- Response-visible diagnostics beat verbose logs for operator troubleshooting
- Safe transport diagnostics must mask internal ports but show public URLs

## Authored Workflow V1 & Deterministic Projection (2026-05-15 → 2026-05-16)

**Commit:** `24374f2` — feat(core): introduce authored workflow model and deterministic V1 projection slice

Created authored workflow types (`StageKind`, `FieldType`, `AuthoredWorkflow`, `WorkflowProjector`) with deterministic projection guarantees.

**Store:** `FilesystemAuthoredWorkflowStore(basePath)` — reads `*.workflow.json` from any directory. Not wired to live host in V1. Pass fixture path in tests.

**HTTP API & Services:**

- **Patch service** (`WorkflowPatchService`): Generates minimal RFC6902 patches between two `AuthoredWorkflow` instances
- **Preview service** (`WorkflowPreviewService`): Projects authored workflow to `WorkflowDefinitionFile` and returns component tree + metadata
- **HTTP endpoints** (5 routes): validate, project, preview, apply — all return `WorkflowProjectionResult`

**WAF integration tests:** Two Program classes conflict (MockBusinessApp + TestSite). Resolved with `Aliases="global,MockBusinessApp"` on ProjectReference + `extern alias` + type alias in test file. `ConfigureWebHost` sets `UseEnvironment("Development")`, injects minimal tenant config via `AddInMemoryCollection`, overrides `IAuthoredWorkflowStore` to point at fixture directory.

**FluentAssertions v6 compatibility:**
- `ContainSingle(e => e is T derived && derived.Prop == X)` fails (CS8122). Use `.OfType<T>().Should().ContainSingle(t => t.Prop == X)`.
- `BeOneOf` with `because:` as named positional arg fails. Use `BeOneOf(IEnumerable, string because)` overload.

All tests passing. Projection determinism locked by design.
