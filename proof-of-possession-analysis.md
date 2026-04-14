# Proof-of-Possession Hardware Binding — Feasibility Analysis for Umbraco.Prism

**Author:** Kicks (Mobile Native Specialist)  
**Date:** 2026-03-28  
**Context:** Response to Jonny's question about "proof-of-possession hardware binding" mentioned in mobile security review

---

## What is Proof-of-Possession (PoP) Hardware Binding?

### Plain English Explanation

**Current approach (bearer token):**
Think of the current biometric flow like a hotel key card. When you register biometric login:
1. The server gives your phone a JWT token (the "key card")
2. Your phone stores it in the iOS Keychain or Android Keystore (a "safe")
3. When you want to sign in, you unlock your phone with Face ID/fingerprint
4. Your phone pulls the JWT from the safe and shows it to the server
5. The server checks if the JWT is valid and logs you in

**Problem:** If malware copies the JWT from your phone's memory (after biometric unlock), it can be used on ANY device. The JWT is like a photocopied hotel key card — anyone who has a copy can use it.

**Proof-of-Possession approach:**
Instead of a copyable JWT, think of a physical hotel room door that requires BOTH your key card AND your fingerprint reader on the door itself. With PoP:
1. When you register, the phone generates a cryptographic key pair (public/private) INSIDE the Secure Enclave (iOS) or StrongBox (Android)
2. The private key NEVER leaves the hardware chip — it physically cannot be copied
3. The phone sends the public key to the server
4. When you sign in, the server sends a challenge (random string)
5. The phone uses Face ID/fingerprint to unlock the private key IN THE HARDWARE
6. The private key signs the challenge (still inside the chip)
7. The phone sends the signed challenge to the server
8. The server verifies the signature using the public key

**Key difference:** With PoP, even if malware compromises your phone's memory, it cannot steal the private key because it never exists in software — only in the tamper-resistant hardware chip.

---

## Current Prism Implementation vs. Proof-of-Possession

### Current Flow (Bearer Token with Biometric Unlock)

**Registration:**
```
User → Entra OIDC → PrismMemberCookie → BiometricController.Register()
    ↓
Server issues BiometricToken JWT (signed with server's HMAC key)
    ↓
JWT stored in iOS Keychain / Android Keystore
    ↓
Entra refresh_token encrypted and stored in database
```

**Exchange (Sign-in):**
```
User taps "Sign in with Face ID"
    ↓
Face ID prompt → unlock iOS Keychain
    ↓
App retrieves BiometricToken JWT from Keychain
    ↓
POST /exchange with JWT + deviceId
    ↓
Server validates JWT signature, looks up credential, decrypts refresh_token
    ↓
Server calls Entra /token with refresh_token
    ↓
Server issues PrismMemberCookie session
```

**Security model:**
- JWT is signed by **server** (symmetric HMAC-SHA256)
- Device only proves it can retrieve the JWT from secure storage
- DeviceId is self-asserted (client-generated UUID)
- No cryptographic proof that the request came from the enrolled device

**Threat:** If an attacker:
1. Compromises the device and extracts the JWT after biometric unlock, OR
2. Intercepts the JWT during transmission (though HTTPS prevents this), OR
3. Exploits a vulnerability in the Keychain/Keystore access layer

...they can replay the JWT from a different device (as long as the JWT hasn't expired).

### Proof-of-Possession Flow (Hardware-Bound Crypto)

**Registration:**
```
User → Entra OIDC → PrismMemberCookie → BiometricController.Register()
    ↓
App generates key pair in Secure Enclave/StrongBox (biometric-protected)
    ↓
App sends PUBLIC KEY to server (not a JWT)
    ↓
Server stores: deviceId, userId, tenantId, publicKey, encrypted refresh_token
    ↓
Private key NEVER leaves hardware chip
```

**Exchange (Sign-in):**
```
User taps "Sign in with Face ID"
    ↓
App requests server challenge: GET /challenge?deviceId=...
    ↓
Server returns: { challenge: "random-nonce-xyz", expiresAt: "..." }
    ↓
Face ID prompt → unlock private key in Secure Enclave
    ↓
App signs challenge with private key (inside hardware)
    ↓
POST /exchange with deviceId + challenge + signature
    ↓
Server retrieves publicKey for deviceId
    ↓
Server verifies signature(challenge) == valid with publicKey
    ↓
If valid: decrypt refresh_token, call Entra /token, issue PrismMemberCookie
```

**Security model:**
- Private key is bound to **device hardware** (cannot be exported)
- Signature proves cryptographically that the request came from the enrolled device
- Even if the signature is intercepted, it's only valid for that specific challenge (single-use)
- DeviceId is still self-asserted, but now tied to a cryptographic proof

**Threat mitigation:**
- ✅ JWT extraction/replay → **Eliminated** (no JWT to steal)
- ✅ Cross-device replay → **Eliminated** (signature tied to device's private key)
- ✅ Malware reading app memory → **Mitigated** (private key never in memory)
- ⚠️ Device theft + coerced biometric → **Same risk** (physical possession + biometric access)

---

## Technical Feasibility with Current Stack

### Platform Support

#### iOS (Secure Enclave)
- **Hardware:** iPhone 5s+ (A7+ chip), iPad Air 2+ (A8X+ chip)
- **API:** `SecKeyCreateRandomKey` with `kSecAttrTokenIDSecureEnclave` attribute
- **Biometric binding:** `kSecAccessControlBiometryCurrentSet` flag (invalidates key if biometrics change)
- **Capacitor plugin:** ❌ No official plugin wraps Secure Enclave key generation
- **Custom native code:** ✅ Required (Objective-C/Swift code in iOS plugin)

#### Android (StrongBox / Keystore)
- **Hardware:** Android 9+ (API 28+) with StrongBox support (flagship devices), API 23+ for standard Keystore
- **API:** `KeyStore` with `setIsStrongBoxBacked(true)` (API 28+) or `AndroidKeyStore` provider
- **Biometric binding:** `setUserAuthenticationRequired(true)` + `setInvalidatedByBiometricEnrollment(true)`
- **Capacitor plugin:** ❌ No official plugin wraps Android KeyStore key generation + signing
- **Custom native code:** ✅ Required (Java/Kotlin code in Android plugin)

### Current Plugins Used

```json
{
  "@aparajita/capacitor-biometric-auth": "^10.0.0",
  "@aparajita/capacitor-secure-storage": "^8.0.0"
}
```

**What these provide:**
- `biometric-auth`: Biometric availability check + prompt
- `secure-storage`: Encrypted data storage (uses Keychain/Keystore for encryption keys, but stores DATA as encrypted files)

**What they DON'T provide:**
- Key pair generation in Secure Enclave/StrongBox
- Cryptographic signing operations with hardware-bound keys
- Challenge-response authentication flows

### Implementation Effort

#### Option 1: Custom Capacitor Plugin (Full PoP)

**New plugin: `@prism/capacitor-device-attestation`**

**iOS Native (Swift):**
```swift
// Generate key pair in Secure Enclave
let access = SecAccessControlCreateWithFlags(
    nil,
    kSecAttrAccessibleWhenUnlockedThisDeviceOnly,
    [.privateKeyUsage, .biometryCurrentSet],
    nil
)

let attributes: [String: Any] = [
    kSecAttrKeyType as String: kSecAttrKeyTypeECDSASeCP256r1,
    kSecAttrTokenID as String: kSecAttrTokenIDSecureEnclave,
    kSecAttrKeySizeInBits as String: 256,
    kSecPrivateKeyAttrs as String: [
        kSecAttrIsPermanent: true,
        kSecAttrApplicationTag: "com.prism.biometric.\(deviceId)",
        kSecAttrAccessControl: access
    ]
]

var error: Unmanaged<CFError>?
guard let privateKey = SecKeyCreateRandomKey(attributes as CFDictionary, &error) else {
    // Handle error
}

// Export public key
let publicKey = SecKeyCopyPublicKey(privateKey)
let publicKeyData = SecKeyCopyExternalRepresentation(publicKey, &error) as Data?

// Sign challenge
let signature = SecKeyCreateSignature(
    privateKey,
    .ecdsaSignatureMessageX962SHA256,
    challengeData as CFData,
    &error
)
```

**Android Native (Kotlin):**
```kotlin
// Generate key pair in StrongBox
val keyGenParameterSpec = KeyGenParameterSpec.Builder(
    "prism_biometric_$deviceId",
    KeyProperties.PURPOSE_SIGN
)
    .setDigests(KeyProperties.DIGEST_SHA256)
    .setAlgorithmParameterSpec(ECGenParameterSpec("secp256r1"))
    .setIsStrongBoxBacked(true) // API 28+, fallback to hardware-backed if unavailable
    .setUserAuthenticationRequired(true)
    .setInvalidatedByBiometricEnrollment(true)
    .build()

val keyPairGenerator = KeyPairGenerator.getInstance(
    KeyProperties.KEY_ALGORITHM_EC,
    "AndroidKeyStore"
)
keyPairGenerator.initialize(keyGenParameterSpec)
val keyPair = keyPairGenerator.generateKeyPair()

// Export public key
val publicKey = keyPair.public.encoded // DER format

// Sign challenge (requires biometric prompt)
val keyStore = KeyStore.getInstance("AndroidKeyStore")
keyStore.load(null)
val privateKey = keyStore.getKey("prism_biometric_$deviceId", null) as PrivateKey

val signature = Signature.getInstance("SHA256withECDSA")
signature.initSign(privateKey)
signature.update(challengeData)
val signatureBytes = signature.sign()
```

**TypeScript API:**
```typescript
interface DeviceAttestationPlugin {
  generateKeyPair(options: {
    deviceId: string;
    reason: string;
  }): Promise<{ publicKey: string }>;

  signChallenge(options: {
    deviceId: string;
    challenge: string;
    reason: string;
  }): Promise<{ signature: string }>;

  deleteKeyPair(options: {
    deviceId: string;
  }): Promise<void>;
}
```

**Estimated effort:**
- Plugin development: 3-5 days (iOS + Android native code + TypeScript wrapper)
- Testing on physical devices: 2 days (Secure Enclave/StrongBox behavior varies by device)
- Integration into biometric-bridge.ts: 1 day
- Server-side challenge/verify endpoints: 2 days (BiometricController changes + crypto lib)
- End-to-end testing: 2 days

**Total: ~10-12 days** (assuming team has iOS/Swift + Android/Kotlin experience)

#### Option 2: WebAuthn / Passkeys (Platform Standard)

**Alternative approach:** Use the W3C WebAuthn standard, which provides proof-of-possession by design.

**How it works:**
1. Registration: `navigator.credentials.create()` generates key pair in platform authenticator
2. Authentication: `navigator.credentials.get()` signs challenge with private key
3. Server verifies using WebAuthn verification libraries

**Capacitor support:**
- iOS 16+ supports WebAuthn API in WKWebView
- Android supports WebAuthn via Chrome Custom Tabs (not in WebView)
- Capacitor app would need to use iOS native WKWebView APIs or Android Custom Tabs

**Pros:**
- ✅ Industry standard (FIDO2 certified)
- ✅ No custom plugin needed (platform APIs)
- ✅ Attestation support (cryptographic proof of device type)
- ✅ Passkeys sync across devices (if enabled)

**Cons:**
- ❌ Requires iOS 16+ (excludes older devices)
- ❌ Android WebView doesn't support WebAuthn natively (needs Custom Tab navigation)
- ❌ Breaks the "stay-in-WebView" mobile shell design (Auth must happen in system browser)
- ❌ More complex UX (user sees browser-level UI, not native biometric prompt)

**Feasibility for Prism:** **Low** — breaks the WebView shell model and excludes pre-iOS 16 devices.

#### Option 3: Third-Party SDK (e.g., FIDO UAF)

**Commercial options:**
- Nok Nok Labs (FIDO UAF)
- Transmit Security (Platform Authenticator SDKs)

**Pros:**
- ✅ Battle-tested implementations
- ✅ Compliance certifications (FIDO, Common Criteria)

**Cons:**
- ❌ Licensing costs (typically per-user or per-transaction)
- ❌ Vendor lock-in
- ❌ Integration complexity (native SDKs + server-side SDK)

**Feasibility for Prism:** **Medium** — viable for enterprise deployments, but adds cost/complexity.

---

## Server-Side Changes Required

### New Endpoints

```csharp
[HttpGet("challenge")]
[AllowAnonymous]
public IActionResult GetChallenge([FromQuery] string deviceId)
{
    var challenge = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    var expiresAt = DateTime.UtcNow.AddMinutes(2);
    
    // Store challenge in Redis/cache with 2-minute TTL
    _challengeCache.Set($"biometric_challenge_{deviceId}", challenge, expiresAt);
    
    return Ok(new { challenge, expiresAt });
}

[HttpPost("exchange")]
[AllowAnonymous]
public async Task<IActionResult> ExchangeWithSignature([FromBody] BiometricExchangeWithSignatureRequest request)
{
    // 1. Retrieve stored challenge
    var storedChallenge = _challengeCache.Get($"biometric_challenge_{request.DeviceId}");
    if (storedChallenge == null || storedChallenge != request.Challenge)
        return Unauthorized(new { error = "invalid_challenge" });
    
    // 2. Look up device credential by deviceId (not JWT hash)
    var credential = db.FirstOrDefault<PrismDeviceCredentialSchema>(
        "WHERE DeviceId = @0 AND TenantId = @1 AND RevokedAt IS NULL",
        request.DeviceId, tenantId);
    
    if (credential == null)
        return Unauthorized(new { error = "device_not_registered" });
    
    // 3. Parse stored public key (DER format)
    var publicKey = ImportPublicKey(credential.PublicKeyDer);
    
    // 4. Verify signature
    using var ecdsa = ECDsa.Create(publicKey);
    var challengeBytes = Encoding.UTF8.GetBytes(request.Challenge);
    var signatureBytes = Convert.FromBase64String(request.Signature);
    
    if (!ecdsa.VerifyData(challengeBytes, signatureBytes, HashAlgorithmName.SHA256))
    {
        _exchangeRateLimitService.RecordTokenFailure(request.DeviceId);
        return Unauthorized(new { error = "invalid_signature" });
    }
    
    // 5. Challenge verified — consume it (prevent replay)
    _challengeCache.Remove($"biometric_challenge_{request.DeviceId}");
    
    // 6. Decrypt refresh_token and proceed as current flow...
    // (steps 8-12 from current Exchange() method)
}
```

### Database Schema Changes

```sql
ALTER TABLE PrismDeviceCredentials 
  ADD PublicKeyDer NVARCHAR(MAX) NULL;  -- DER-encoded EC public key (P-256)

-- TokenHash column becomes optional (only used for JWT-based flow)
-- New PoP flow uses DeviceId as primary lookup key
```

### Migration Strategy

**Dual-mode support (gradual rollout):**
1. Keep existing JWT-based flow as default
2. Add new PoP endpoints (`/challenge`, `/exchange-with-signature`)
3. Client detects PoP plugin availability → uses PoP if available, falls back to JWT
4. Server supports both flows during transition period
5. After 6-12 months, deprecate JWT flow

---

## User Convenience Impact

### What Stays the Same ✅
- Biometric enrollment UX (Face ID/fingerprint prompt during registration)
- Sign-in UX (single biometric prompt at app startup)
- Automatic re-authentication after app backgrounding
- Device revocation from backoffice settings

### What Changes ⚠️
- **Registration time:** +500ms (key generation is slower than JWT issuance)
- **Sign-in time:** +1 round trip (need to fetch challenge before signing)
  - Current: 1 request (`POST /exchange` with JWT)
  - PoP: 2 requests (`GET /challenge` → `POST /exchange` with signature)
  - **Mitigation:** Prefetch challenge during app launch (before user taps Face ID button)

### Degraded Experience Scenarios
- **Biometric enrollment change:** Current flow already handles this (invalidates stored JWT)
  - PoP: iOS/Android flags automatically invalidate keys (no code change needed)
- **Device transfer:** Current flow loses credentials (user must re-register)
  - PoP: Same behavior (private key cannot be exported)
- **Offline sign-in:** Current flow fails (requires server to validate JWT)
  - PoP: Also fails (requires server challenge)

**Verdict:** **Nearly identical UX** with proper prefetching. Sign-in adds <200ms latency if challenge is prefetched.

---

## Security Benefits vs. Risk

### Benefits of PoP
1. **Eliminates JWT theft risk:** Private key cannot be extracted from hardware
2. **Prevents cross-device replay:** Signature is device-bound
3. **Stronger compliance posture:** Aligns with FIDO2 / PSD2 SCA requirements
4. **Auditability:** Cryptographic proof of device origin in logs

### Risks/Limitations
1. **Does NOT prevent:**
   - Device theft + coerced biometric unlock (physical threat model)
   - Server-side compromise (attacker can still decrypt refresh_tokens)
   - Man-in-the-middle (HTTPS certificate pinning still required)

2. **Operational considerations:**
   - Key generation failures (rare, but Secure Enclave can fill up on old devices)
   - Harder to debug (crypto operations are black-box at native layer)
   - Platform fragmentation (Secure Enclave vs. StrongBox vs. fallback Keystore)

3. **User friction:**
   - Cannot "export" credentials to new device (explicit re-registration required)
   - Biometric changes force re-registration (more secure, but more friction)

---

## Recommendation

### Immediate Answer to Jonny's Question

**"Is proof-of-possession hardware binding possible now?"**

**Yes, technically feasible** with current iOS/Android capabilities. Requires:
- Custom Capacitor plugin (~10 days dev effort)
- Server-side challenge/verify logic (~2 days)
- Database schema addition for public keys

**But should we do it now?**

### Recommended Approach: **Staged Behind "Strict Security Mode"**

#### Phase 1 (Now): Document + Design
- Create design doc: `Design/proof-of-possession.md`
- Define API contracts (challenge/exchange endpoints)
- Spec custom plugin interface
- **Decision point:** Is this needed for launch? (Probably not)

#### Phase 2 (Post-Launch): Implement as Optional Feature
- Build `@prism/capacitor-device-attestation` plugin
- Add server-side dual-mode support
- Expose in tenant settings: `AllowBiometricHardwareBinding` (default: off)
- UI: "Enhanced Mobile Security (Hardware-Bound)" toggle in backoffice

#### Phase 3 (Mature): Mandate for High-Security Tenants
- Flag tenants handling regulated data (GDPR special categories, healthcare, finance)
- Auto-enable hardware binding for these tenants
- Provide migration tool for existing JWT-based registrations

### Why Stage It?

1. **Current JWT flow is "good enough" for most use cases:**
   - Already encrypts refresh_tokens at rest
   - Already binds deviceId + userId + tenantId
   - Already has rate-limiting and lockout
   - Keychain/Keystore protection is strong (iOS Keychain has never been publicly compromised)

2. **PoP adds complexity without eliminating all threats:**
   - Physical device theft + coercion is still a risk
   - Server-side compromise is a bigger threat (refresh_tokens stored encrypted, but decryption key is on server)
   - Most mobile malware targets credential phishing (which PoP doesn't prevent)

3. **Development ROI is better elsewhere:**
   - Push notifications, offline mode, multi-tenant UX polish
   - Backend scale testing, monitoring, alerting
   - Finish Entra CIAM integration, test tenant lifecycle

4. **Compliance trigger:** If a customer says "we need FIDO2 certification" or "we need hardware-backed authentication", THEN build it.

### Alternative: Frame as "Convenience-Grade" in Documentation

Update `Design/biometric-auth.md` and tenant settings UI:

> **Biometric Login Security Model**
> 
> Prism's biometric authentication provides **convenience-grade security** suitable for typical business applications. The device stores an encrypted credential that is unlocked via Face ID/Touch ID. This protects against:
> - ✅ Accidental device loss (credential is locked behind biometric)
> - ✅ Casual snooping (requires biometric unlock)
> - ✅ Phishing attacks (user never enters password)
> 
> However, it does not provide hardware-attested proof-of-possession. For regulated environments (finance, healthcare, defense) requiring hardware-bound cryptographic authentication, consider:
> - Requiring Entra OIDC sign-in on every session (no biometric convenience)
> - Enabling certificate-based authentication (requires MDM)
> - Contacting Prism support for enterprise-grade device attestation features
> 
> **Recommendation:** Biometric login is appropriate for internal business apps where user convenience and security balance is valued over regulatory compliance.

---

## Conclusion

**Proof-of-possession hardware binding is technically possible** with ~2 weeks of development effort, but it should be **deferred until there's a clear customer or compliance driver**.

The current JWT-based biometric flow is a **pragmatic, secure-enough solution** for a v1.0 mobile release. It's significantly better than password-only auth, and the incremental security benefit of PoP doesn't justify the complexity right now.

If/when PoP is built, it should be:
- **Opt-in** (tenant flag + user choice)
- **Transparent** (same UX, just better crypto under the hood)
- **Backward-compatible** (support both JWT and PoP flows during migration)

**Decision logged:** `.squad/decisions/inbox/kicks-proof-of-possession-feasibility.md`

---

## Appendix: Key Technical Terms Explained

- **Secure Enclave (iOS):** A hardware chip separate from the main CPU that stores cryptographic keys. Even if iOS is jailbroken, keys cannot be extracted.
- **StrongBox (Android):** Similar to Secure Enclave, a tamper-resistant hardware security module (HSM) on high-end Android devices (Pixel 3+, Samsung S9+).
- **ECDSA/P-256:** Elliptic curve cryptography algorithm (like RSA but faster). P-256 is a standard curve (secp256r1).
- **Challenge-response:** Server sends random string → client signs it with private key → server verifies with public key. Proves client owns the private key without revealing it.
- **DER encoding:** Binary format for storing public keys (used by iOS SecKey and Android KeyStore).
- **FIDO2/WebAuthn:** W3C web standard for passwordless authentication using hardware security keys or platform authenticators (Face ID, Touch ID).
- **Attestation:** Cryptographic proof that a key was generated in genuine hardware (not a software emulator). iOS/Android provide attestation APIs, but require Apple/Google server verification.

---

**Next steps:**
1. Jonny reviews and decides: defer, stage, or prioritize?
2. If staged: create tracking issue "Support hardware-bound biometric authentication (PoP)"
3. Update `Design/biometric-auth.md` with "convenience-grade" framing
4. Log decision in squad decisions
