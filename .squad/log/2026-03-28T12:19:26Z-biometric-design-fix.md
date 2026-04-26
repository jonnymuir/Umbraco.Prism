# Session Log 2026-03-28T12:19:26Z

## Biometric Auth Design — JWT/UUID Consistency Fix

**Participant:** Tom Nook (Lead Architect)

**Context:** Follow-up to the Biometric Auth Architecture Design sprint (2026-03-28T11:55:34Z). An internal consistency review identified a conflict: early sections of `Design/biometric-auth.md` described `BiometricToken` as a UUID v4, while the agreed model (captured in decisions) uses a signed JWT. This session resolves that inconsistency end-to-end.

**Outcomes:**

1. **Design Document Updated** (`/Design/biometric-auth.md`):
   - `BiometricToken` consistently described as a Prism-signed JWT throughout
   - JWT payload: `deviceId` (client UUID, first-launch), `tenantId`, `userOid`, `iat`, `exp`
   - `DeviceId` claim used for device binding: stored in `prismBiometricTokens` table, validated on every `/exchange` call → closes bearer theft vector

2. **Token Lifetime Corrected**:
   - Old: 90 days, non-configurable
   - New: 30 days default, tenant-configurable (range: 7–90 days)

3. **Audit Logging Promoted to v1**:
   - Minimum logging (attempt + outcome + token ID + IP) is ~5 lines of code; deferring to v2 was unjustified
   - Now required in v1

4. **Rate Limiting Hardened**:
   - Old policy: "5 requests/minute per device ID" (unenforceable without server-side device identity)
   - New policy: 3 failed `/exchange` attempts within 10 minutes per token → token locked, requires re-registration; IP-based rate limiting as secondary layer

5. **Decision Filed**:
   - `.squad/decisions/inbox/tom-nook-biometric-jwt-committed.md`

**Next Phase:** Implementation (Blathers: backend C# changes; TypeScript: WebView bridge + flows)
