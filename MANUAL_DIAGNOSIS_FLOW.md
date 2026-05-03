# Downstream API Timeout Diagnosis Flow

**Target endpoint:** `https://{codespace}-7245.app.github.dev/api/backoffice/me`  
**Component:** MockBusinessApp API (requires Bearer token)  
**Timeout:** 10 seconds (configured in DownstreamDemoController line 289)

> **Prefer the scripted pass first in Codespaces:** run `bash scripts/codespaces/diagnose-downstream.sh` before stepping through the manual flow below. The shell-only helper separates internal reachability, public tunnel/auth HTML, bearer-token failures, and Keycloak backchannel problems in one pass.

---

## Quick Start: Verify API Reachability

### Step 1a: Is MockBusinessApp listening on HTTP internally?

```bash
# In your Codespace terminal, run:
curl -v http://localhost:5163/api/backoffice/me
```

**Expected outcomes:**

- ✅ **401 Unauthorized** → API is reachable and demanding authentication (good sign, move to Step 2)
- ✅ **400 Bad Request** → API is up; bearer token missing/invalid (expected, move to Step 2)
- ❌ **Connection refused** → MockBusinessApp not running or not on port 5163
- ❌ **Connection timeout** → Firewall issue or wrong port

**If failed:** Check Aspire AppHost logs for MockBusinessApp port assignment (see "Find the Actual Port" section below).

---

### Step 1b: Is MockBusinessApp listening on HTTPS publicly?

```bash
# In your Codespace terminal, run:
curl -v https://{codespace}-7245.app.github.dev/api/backoffice/me
```

Replace `{codespace}` with your actual Codespace name (e.g., `abc-123-def`).

**Expected outcomes:**

- ✅ **401 Unauthorized** → Public HTTPS endpoint is reachable (good)
- ✅ **403 Forbidden** → API is up; may indicate GitHub Codespaces port forwarding misconfiguration
- ✅ **HTML response** (contains "Connecting to the forwarded port" text) → GitHub port forwarding tunnel not authenticated
- ❌ **Connection refused** → Port forwarding not active
- ❌ **Connection timeout** → Port not forwarded to the AppHost

**If you get HTML instead of JSON:** See "GitHub Codespaces Tunnel Authentication" section.

---

## Step 2: Isolate Bearer Token Validation

### Step 2a: Get a fresh Bearer token

Sign in to TestSite via browser and capture your access token:

```bash
# In Codespaces browser console (F12 → Console tab):
(async () => {
  const resp = await fetch('/api/prism/downstream-demo/session-contract');
  const data = await resp.json();
  console.log('Access Token Ready:', data.cookie.hasAccessToken);
  console.log('Token Expiry:', data.cookie.expiresAt);
  console.log('Tenant:', data.tenant.hostname);
})()
```

You should see `"hasAccessToken": true` and a valid expiry timestamp.

### Step 2b: Call the API with that token (server-side via TestSite)

Click the **API Demo button** in the TestSite dashboard and observe the response.

- ✅ **200 OK** with JSON → Full flow works end-to-end
- ❌ **Timeout (10s)** → TestSite → MockBusinessApp hop is timing out (Step 3)
- ✅ **401 Unauthorized** → Bearer token rejected by MockBusinessApp (Step 4)

### Step 2c: Call the API directly with curl (manual bearer token test)

If sign-in works but the API demo button times out, extract your session cookie:

```bash
# In your browser, open browser DevTools (F12)
# → Application / Storage → Cookies → {codespace}-44345.app.github.dev
# Copy the value of ".AspNetCore.Cookies" (or similar)
# Then in terminal:

COOKIE="{paste-cookie-value-here}"
curl -v \
  -H "Cookie: .AspNetCore.Cookies=${COOKIE}" \
  https://{codespace}-7245.app.github.dev/api/backoffice/me
```

**Expected outcomes:**

- ✅ **200 OK** → Browser can reach the public endpoint and token is valid
- ✅ **401 Unauthorized** → API is reachable but token rejected (likely Keycloak backchannel issue)
- ❌ **Timeout** → GitHub port forwarding tunnel issue (Step 5)
- ❌ **HTML response** → Port forwarding tunnel authentication required

---

## Step 3: Test Server-to-Server Transport (TestSite → MockBusinessApp)

The TestSite → MockBusinessApp hop happens server-side. Simulate it locally:

```bash
# Get your current Bearer token from the session-contract endpoint
# (same as Step 2a, but get the actual token string)

TOKEN="{your-access-token-from-session-contract}"

# Test the INTERNAL backchannel (what TestSite uses in Codespaces)
curl -v \
  -H "Authorization: Bearer ${TOKEN}" \
  http://localhost:5163/api/backoffice/me
```

**Expected outcomes:**

- ✅ **200 OK** → Backchannel works; token is valid
- ✅ **401 Unauthorized** → Backchannel reachable but token rejected (Keycloak backchannel issue; see Step 4)
- ❌ **Connection refused** → Port 5163 not listening (Aspire reassigned the port; see "Find the Actual Port")
- ❌ **Timeout** → Port is correct but something hung inside MockBusinessApp

If this succeeds but the UI demo button times out, check DownstreamDemoController logs.

---

## Step 4: Bearer Token Validation — Check Keycloak Backchannel

If `curl` tests return **401 Unauthorized**, the token is either:
1. Expired
2. Not trusted by MockBusinessApp (issuer/audience mismatch)
3. Keycloak's backchannel is unreachable (can't fetch signing keys)

### 4a: Check token expiry

```bash
# From the session-contract response (Step 2a):
# Check data.cookie.expiresAt — is it in the future (unix timestamp)?
# If in the past, sign in again and get a fresh token.
```

### 4b: Check Keycloak's OIDC discovery endpoint

```bash
# Keycloak internal backchannel (what MockBusinessApp uses):
curl -v http://localhost:8080/realms/prism-dev/.well-known/openid-configuration
```

**Expected outcome:** 200 OK with JSON containing `issuer`, `jwks_uri`, etc.

If this fails:
- Keycloak container not running
- Port 8080 is wrong (see "Find the Actual Port")

### 4c: Check Keycloak signing keys are reachable

```bash
# Get the JWKS endpoint from step 4b (usually ends with /certs)
curl -v http://localhost:8080/realms/prism-dev/protocol/openid-connect/certs
```

**Expected outcome:** 200 OK with JSON containing `keys: [...]`

If this fails → MockBusinessApp can't validate bearer tokens (all requests will 401).

### 4d: Confirm MockBusinessApp trusts the right Keycloak issuer

Check `src/UmbracoPrism.MockBusinessApp/appsettings.json`:

```json
{
  "PrismBusinessApp": {
    "Tenants": {
      "2": {
        "OidcAuthority": "https://localhost:8443/realms/prism-dev"
      }
    }
  }
}
```

- In **local dev**: Should be `https://localhost:8443/realms/prism-dev` (HTTPS proxy)
- In **Codespaces**: Should be `https://{codespace}-8443.app.github.dev/realms/prism-dev` (public URL)

Mismatch between configured issuer and actual token issuer = 401.

---

## Step 5: GitHub Codespaces Port Forwarding Tunnel

If `curl` against the public endpoint returns **HTML** instead of JSON, you've hit the GitHub tunnel auth page.

### 5a: Check port forwarding status

```bash
# List all forwarded ports in your Codespace:
gh codespace ports --codespace {codespace-name}
```

**Expected output:**

```
sourcePort  protocol  visibility  forwardedPort  browseUrl
7245        https     public      https://...     https://{codespace}-7245.app.github.dev/
```

- If port 7245 is missing → Add it in .devcontainer.json or forward manually
- If `browseUrl` doesn't match your URL → Codespace may be in regional scheme; use `browseUrl` instead

### 5b: Check if the forwarded endpoint requires authentication

```bash
# Codespaces public endpoint without auth:
curl -i https://{codespace}-7245.app.github.dev/api/backoffice/me
```

If you see:

```html
<h1>Connecting to the forwarded port...</h1>
```

This is GitHub's tunnel auth page (normal). The actual API behind it requires a Bearer token.

**Next:** Pass the Bearer token:

```bash
TOKEN="{your-access-token}"
curl -v \
  -H "Authorization: Bearer ${TOKEN}" \
  https://{codespace}-7245.app.github.dev/api/backoffice/me
```

---

## Find the Actual Port

If MockBusinessApp is not on the expected port, query Aspire:

### Option A: Check Aspire AppHost logs

```bash
# Recent AppHost output (find port assignments):
gh codespace logs --codespace {codespace-name} | grep -i "businessapp\|7245\|port\|endpoint"
```

### Option B: List all bound ports

```bash
# On the Codespace machine itself:
netstat -tlnp 2>/dev/null | grep dotnet
# or
lsof -i -P -n 2>/dev/null | grep LISTEN
```

Look for the `dotnet` process running UmbracoPrism.MockBusinessApp and note its bound port.

### Option C: Check Aspire dashboard

Open the Aspire dashboard (port 17214 in Codespaces):

```
https://{codespace}-17214.app.github.dev/
```

- Find "businessapp" resource
- Click "Endpoints" section
- Note the actual HTTP endpoint URL (e.g., `http://localhost:5999`)

---

## Diagnosis Flowchart

```
┌─ Start: "API button times out"
│
├─ Step 1a: curl http://localhost:5163/api/backoffice/me
│  ├─ Connection refused? → MockBusinessApp not on port 5163 (Find Actual Port)
│  ├─ Timeout? → Network issue or hung process (check AppHost logs)
│  └─ 401 Unauthorized? ✓ → Proceed to Step 2
│
├─ Step 2c: curl -H "Authorization: Bearer ${TOKEN}" https://{codespace}-7245.app.github.dev/api/backoffice/me
│  ├─ 200 OK? ✓ → Full flow works; check TestSite logs for why demo button fails
│  ├─ 401 Unauthorized? → Proceed to Step 4 (Bearer token validation)
│  ├─ HTML response? → Proceed to Step 5 (Port forwarding tunnel)
│  └─ Timeout? → Step 5 (port forwarding) or network issue
│
├─ Step 3: curl -H "Authorization: Bearer ${TOKEN}" http://localhost:5163/api/backoffice/me
│  ├─ 200 OK? ✓ → Backchannel works; check TestSite logs
│  ├─ 401 Unauthorized? → Proceed to Step 4 (Keycloak backchannel)
│  └─ Connection refused? → Aspire reassigned port (Find Actual Port)
│
├─ Step 4: Check Keycloak backchannel
│  ├─ 4a: curl http://localhost:8080/realms/prism-dev/.well-known/openid-configuration
│  ├─ 4b: curl http://localhost:8080/realms/prism-dev/protocol/openid-connect/certs
│  └─ 4c: Verify appsettings.json issuer matches token issuer
│
└─ Step 5: GitHub Codespaces port forwarding
   ├─ Check gh codespace ports output
   ├─ Verify browseUrl is correct
   └─ Test with Bearer token
```

---

## Expected Behavior Summary

### ✅ Healthy Flow

1. **Browser sign-in** → Session cookie + access token stored
2. **Click API button** → Browser calls `GET /api/prism/downstream-demo` with session cookie
3. **TestSite server-side** → Extracts Bearer token from cookie, calls `http://localhost:5163/api/backoffice/me` (backchannel)
4. **MockBusinessApp** → Validates Bearer token against Keycloak, returns 200 OK + user info
5. **Browser** → Displays JSON response in dashboard (< 1 second)

### ❌ Common Failure Points

| Symptom | Root Cause | Fix |
|---------|-----------|-----|
| **10s timeout** | MockBusinessApp unreachable on backchannel port | Check Aspire port assignment; verify BUSINESSAPP_BACKCHANNEL_URL |
| **401 Unauthorized** | Bearer token invalid or Keycloak unreachable | Check token expiry, Keycloak backchannel, issuer mismatch |
| **HTML response** | GitHub Codespaces tunnel requires authentication | Use public URL with Bearer token header |
| **Connection refused** | Aspire assigned different ephemeral port | Check Aspire dashboard or AppHost logs |
| **Network Error** | TestSite can't reach MockBusinessApp DNS/firewall | Verify localhost/internal port accessibility |

---

## Operator Checklist

- [ ] MockBusinessApp is running (check `dotnet processes` or Aspire dashboard)
- [ ] Keycloak is running (check Aspire dashboard)
- [ ] Session cookie exists (browser DevTools → Application → Cookies)
- [ ] Access token is not expired (check session-contract endpoint)
- [ ] `BUSINESSAPP_BACKCHANNEL_URL` is set correctly in TestSite environment
- [ ] Port 5163 (or actual port) is listening on localhost
- [ ] Bearer token can be extracted and passed to curl
- [ ] Keycloak OIDC discovery is reachable from localhost:8080
