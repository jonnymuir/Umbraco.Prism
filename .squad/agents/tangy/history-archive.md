# Tangy — History Archive

## 2026-05-03 Detail Sessions (Archived)

### Downstream Timeout URL-Choice vs BusinessApp Diagnosis (2026-05-03T22:27:45)
- Diagnosed downstream API timeout: backchannel port hardcoding vs runtime discovery
- Reduced operator diagnostic flow to three checks
- DevTools "Copy as cURL" proven fastest URL-path comparison tool

### Transport Diagnostics Validation Spawn (2026-05-03)
- Defined behavioral contract for transport path masking
- Test contracts: backchannel/public tunnel classification, timeout metadata, masking
- All 680 Core tests passing

### Business API Arrival Instrumentation (2026-05-04T00:01:43)
- Test contract validated for trace ID capture and forwarding
- Safety model: read-only diagnostic headers, no auth/PII exposure

### Workflow 401 Regression Investigation (2026-05-04)
- Two layered failure modes produce same "Business App error (HTTP 401)" surface:
  1. Null auth header silently dropped in `BusinessAppWorkflowClient`
  2. Application-level `Results.Unauthorized()` in workflow handlers vs `Results.Problem()` in backoffice
- Added 3 regression tests to `BusinessAppWorkflowClientTests.cs`
- Root causes: JWT middleware 401 (no valid token) vs application-level 401 (tenant/email resolution failed)

### Key Learnings
- `[PRISM AUTH FAILED]` console log from `OnAuthenticationFailed` distinguishes JWT vs app-level 401
- Null auth header in `CreateClientAsync` is silent danger—omits header without logging
- JWKS fix (0904810) necessary but insufficient if `PrismTenantMiddleware` fails to resolve tenant
