# Skill: GitHub Codespaces URL Forms (and how to derive the *real* public URL)

## When to use

Any time code (or scripts) need to compute a public URL for something running
on a forwarded port inside a GitHub Codespace — Aspire URL plumbing, OIDC
redirect URIs, tenant seeding, devcontainer status pages, README output, etc.

## The three URL families you will encounter

1. **VS Code web editor (NOT a forwarded port)**
   `https://{CODESPACE_NAME}.github.dev`
   - One per Codespace. Serves the in-browser IDE.
   - Has NO port number. Domain is bare `github.dev`, not `app.github.dev`.
   - Never target this from app code. It is not your app.

2. **Legacy port-forwarding URL**
   `https://{CODESPACE_NAME}-{port}.app.github.dev`
   - Older scheme. Still served in some regions.
   - This is what most existing tutorials/codebases assume.

3. **New port-forwarding URL (rolling out)**
   `https://{token}-{port}.{region}.app.github.dev`
   (e.g. `v7ldkc4c-3000.uks1.app.github.dev`)
   - `{token}` is an opaque per-Codespace forwarding identifier.
   - `{token}` is **NOT** derived from `CODESPACE_NAME` and is **NOT** exposed
     as an env var.
   - `{region}` is captured by `GITHUB_CODESPACES_PORT_FORWARDING_DOMAIN`
     (e.g. `uks1.app.github.dev`).

   ⚠️ Glueing `{CODESPACE_NAME}-{port}` in front of
   `GITHUB_CODESPACES_PORT_FORWARDING_DOMAIN` produces a host Codespaces does
   NOT serve in this scheme.

## ✅ Robust derivation strategies

### Strategy A — query `gh codespace ports`

Inside the Codespace (works in `on-start.sh`, in dotnet AppHost via
`System.Diagnostics.Process`, etc.):

```bash
gh codespace ports --codespace "$CODESPACE_NAME" \
  --json sourcePort,browseUrl
```

Returns the actual `browseUrl` for every forwarded port — authoritative.
Use this **once** at startup to populate Aspire env vars
(`KEYCLOAK_URL`, `TESTSITE_PUBLIC_URL`, `KC_HOSTNAME`, …).

**Important:** `browseUrl` values have a trailing slash — call `.TrimEnd('/')`.

**C# AppHost pattern (using `System.Diagnostics.Process`):**

```csharp
var psi = new ProcessStartInfo("gh",
    $"codespace ports --codespace {codespaceName} --json sourcePort,browseUrl")
{
    RedirectStandardOutput = true,
    UseShellExecute = false,
};
using var proc = Process.Start(psi);
if (proc is null) throw new InvalidOperationException("gh not found");
proc.WaitForExit(10_000);
var json = await proc.StandardOutput.ReadToEndAsync();
// parse with System.Text.Json ...
```

**Shell (on-start.sh) pattern:**

```bash
CODESPACE_PORTS_JSON=$(gh codespace ports \
  --codespace "$CODESPACE_NAME" --json sourcePort,browseUrl 2>/dev/null || echo "[]")

get_codespace_url() {
  local port=$1
  if command -v jq &>/dev/null; then
    echo "$CODESPACE_PORTS_JSON" | jq -r \
      --argjson p "$port" '.[] | select(.sourcePort==$p) | .browseUrl' \
      | sed 's|/$||'
  else
    python3 -c "
import json, sys
data = json.loads('''$CODESPACE_PORTS_JSON''')
url = next((e['browseUrl'].rstrip('/') for e in data if e['sourcePort']==$port), '')
print(url)"
  fi
}
```

**Always fall back to the legacy pattern with a warning if gh fails.** This keeps
local dev working when `CODESPACE_NAME` is unset.

### Strategy B — derive from the inbound request

Inside the running app, the inbound request hostname IS the real public host
for that port. Sibling-port URLs share the same `{token}` and `{region}`
prefix — derive them by string-swapping the port segment.

```text
request host : v7ldkc4c-44345.uks1.app.github.dev   (TestSite)
sibling host : v7ldkc4c-8443.uks1.app.github.dev    (Keycloak proxy)
```

Use a regex anchored on `-{port}.` not on `{name}-{port}`:

```csharp
// Replace -{currentPort}. with -{siblingPort}.
var siblingHost = System.Text.RegularExpressions.Regex.Replace(
    requestHost, @"-\d+\.", $"-{siblingPort}.");
```

## ❌ Anti-patterns

- `$"https://{CODESPACE_NAME}-{port}.app.github.dev"` — assumes legacy scheme.
- `$"https://{CODESPACE_NAME}-{port}.{GITHUB_CODESPACES_PORT_FORWARDING_DOMAIN}"`
  — looks robust because it uses the env var, but still fails on the new
  scheme because `CODESPACE_NAME` ≠ `{token}`.
- Seeding tenants by one exact Codespaces hostname.

## ✅ Safe pattern checks

- Hostname-membership tests should use `EndsWith(".app.github.dev",
  StringComparison.OrdinalIgnoreCase)` — that matches both the legacy and the
  new (regional) form.

## Security bedrock (do NOT compromise)

The request hostname may be used to derive *configuration* (which OIDC
authority to trust, which tenant row to look up). It must NEVER be used as
identity or to bypass `ValidateIssuer` / `ValidateAudience` /
`ValidateIssuerSigningKey` / `RequireHttpsMetadata`. The configured
`OidcAuthority` remains the trust anchor for token validation regardless of
what URL the request arrived on.

## Reference

- Confirmed working in production: `fix/codespaces-url-derivation` — 647 tests pass with
  regional URL form (`v7ldkc4c-8443.uks1.app.github.dev`) covered in Group D of
  `BackchannelRewriteTests`.
- See `.squad/decisions/blathers-codespaces-url-forms.md` (after Scribe merges
  the inbox file).
- Originating investigation: `.squad/agents/blathers/history.md` entry
  `2026-05-02 — Codespaces URL Forms`.
