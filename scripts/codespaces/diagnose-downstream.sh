#!/bin/bash
# diagnose-downstream.sh — Diagnose Codespaces downstream auth and reachability issues.
#
# Usage:
#   bash scripts/codespaces/diagnose-downstream.sh [--token <bearer-token>]
#
# Optional environment variables:
#   PRISM_BEARER_TOKEN              Bearer token to test authenticated downstream calls
#   PRISM_BUSINESSAPP_INTERNAL_URL  Override default internal BusinessApp URL (default: http://localhost:5163)
#   PRISM_LOCAL_BUSINESSAPP_URL     Override default local HTTPS BusinessApp URL (default: https://localhost:7245)
#
# This script distinguishes:
#   - Internal BusinessApp reachability vs public Codespaces tunnel/auth pages
#   - App availability vs bearer token / Keycloak validation failures
#   - Healthy vs stale Keycloak backchannel wiring in the running stack
#   - TestSite same-origin endpoint shape without needing a browser session
#
# Run from the repo root or anywhere inside the repo.

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$REPO_ROOT"

python3 - "$@" <<'PY'
import argparse
import json
import os
import ssl
import subprocess
import sys
import urllib.error
import urllib.request
from pathlib import Path

REPO_ROOT = Path.cwd()
PROGRAM_CS = REPO_ROOT / "src/UmbracoPrism.AppHost/Program.cs"
APPSETTINGS_JSON = REPO_ROOT / "src/UmbracoPrism.MockBusinessApp/appsettings.json"

DEFAULT_INTERNAL_BUSINESS_URL = os.environ.get("PRISM_BUSINESSAPP_INTERNAL_URL", "http://localhost:5163").rstrip("/")
DEFAULT_LOCAL_BUSINESS_URL = os.environ.get("PRISM_LOCAL_BUSINESSAPP_URL", "https://localhost:7245").rstrip("/")


class NoRedirectHandler(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


def build_opener(insecure: bool):
    context = ssl._create_unverified_context() if insecure else ssl.create_default_context()
    return urllib.request.build_opener(NoRedirectHandler(), urllib.request.HTTPSHandler(context=context))


def probe(url: str, *, insecure: bool = False, token: str | None = None, timeout: int = 6) -> dict:
    headers = {"User-Agent": "UmbracoPrism-Diagnostics/1.0", "Accept": "application/json, text/html;q=0.9, */*;q=0.1"}
    if token:
        headers["Authorization"] = f"Bearer {token}"

    req = urllib.request.Request(url, headers=headers)
    opener = build_opener(insecure)

    try:
        with opener.open(req, timeout=timeout) as response:
            body = response.read().decode("utf-8", errors="replace")
            headers_map = dict(response.headers.items())
            return normalize_response(url, response.status, response.reason, headers_map, body)
    except urllib.error.HTTPError as ex:
        body = ex.read().decode("utf-8", errors="replace")
        headers_map = dict(ex.headers.items())
        return normalize_response(url, ex.code, ex.reason, headers_map, body)
    except urllib.error.URLError as ex:
        return {
            "url": url,
            "ok": False,
            "network_error": True,
            "error": str(ex.reason),
            "status": None,
            "reason": None,
            "content_type": None,
            "location": None,
            "body": "",
            "body_preview": "",
            "json": None,
            "tunnel_html": False,
            "redirect": False,
        }
    except Exception as ex:  # pragma: no cover - defensive diagnostics path
        return {
            "url": url,
            "ok": False,
            "network_error": True,
            "error": str(ex),
            "status": None,
            "reason": None,
            "content_type": None,
            "location": None,
            "body": "",
            "body_preview": "",
            "json": None,
            "tunnel_html": False,
            "redirect": False,
        }


def normalize_response(url: str, status: int, reason: str | None, headers_map: dict, body: str) -> dict:
    content_type = headers_map.get("Content-Type", "").split(";", 1)[0].strip().lower() or None
    preview = body[:400]
    tunnel_html = "Connecting to the forwarded port" in body or "forwarded port" in body.lower()
    parsed_json = None
    if content_type == "application/json":
        try:
            parsed_json = json.loads(body)
        except json.JSONDecodeError:
            parsed_json = None

    return {
        "url": url,
        "ok": 200 <= status < 300,
        "network_error": False,
        "error": None,
        "status": status,
        "reason": reason,
        "content_type": content_type,
        "location": headers_map.get("Location"),
        "body": body,
        "body_preview": preview,
        "json": parsed_json,
        "tunnel_html": tunnel_html,
        "redirect": 300 <= status < 400,
    }


def discover_public_url(port: int) -> tuple[str, str]:
    codespace_name = os.environ.get("CODESPACE_NAME", "").strip()
    if not codespace_name:
        return (f"https://localhost:{port}", "local")

    try:
        result = subprocess.run(
            ["gh", "codespace", "ports", "--codespace", codespace_name, "--json", "sourcePort,browseUrl"],
            capture_output=True,
            text=True,
            check=True,
            timeout=10,
        )
        data = json.loads(result.stdout or "[]")
        for entry in data:
            if int(entry.get("sourcePort", -1)) == port and entry.get("browseUrl"):
                return (entry["browseUrl"].rstrip("/"), "gh")
    except Exception:
        pass

    domain = os.environ.get("GITHUB_CODESPACES_PORT_FORWARDING_DOMAIN", "app.github.dev").strip()
    return (f"https://{codespace_name}-{port}.{domain}", "fallback")


def read_expected_authority() -> str | None:
    try:
        data = json.loads(APPSETTINGS_JSON.read_text())
        tenants = data.get("PrismBusinessApp", {}).get("Tenants", [])
        for tenant in tenants:
            authority = tenant.get("OidcAuthority")
            if authority:
                return authority.rstrip("/")
    except Exception:
        return None
    return None


def read_apphost_expectations() -> tuple[bool, bool]:
    text = PROGRAM_CS.read_text()
    expects_business_dynamic = 'testsite.WithEnvironment("BUSINESSAPP_BACKCHANNEL_URL", businessApp.GetEndpoint("http"))' in text
    expects_keycloak_dynamic = 'businessApp.WithEnvironment("KEYCLOAK_BACKCHANNEL_URL", keycloak.GetEndpoint("http"))' in text
    return expects_business_dynamic, expects_keycloak_dynamic


def shell_quote(value: str) -> str:
    return "'" + value.replace("'", "'\"'\"'") + "'"


def safe_env(name: str) -> str:
    value = os.environ.get(name, "").strip()
    return value or "(not set in current shell)"


def status_line(result: dict) -> str:
    if result.get("network_error"):
        return f"network error ({result.get('error')})"
    return f"HTTP {result.get('status')} {result.get('reason') or ''}".strip()


def print_section(title: str):
    print(f"\n== {title} ==")


def report(label: str, verdict: str, message: str, next_steps: list[str] | None = None):
    icon = {
        "PASS": "✅",
        "WARN": "⚠️",
        "FAIL": "❌",
        "SKIP": "⏭️",
    }[verdict]
    print(f"{icon} {label}: {message}")
    if next_steps:
        for step in next_steps:
            print(f"   next: {step}")


parser = argparse.ArgumentParser(add_help=True)
parser.add_argument("--token", dest="token", default=os.environ.get("PRISM_BEARER_TOKEN", ""), help="Bearer token for authenticated downstream probes")
args = parser.parse_args()
token = args.token.strip() or None

public_business_url, public_business_source = discover_public_url(7245)
public_keycloak_url, public_keycloak_source = discover_public_url(8443)
public_testsite_url, public_testsite_source = discover_public_url(44345)
expected_authority = read_expected_authority()
expects_business_dynamic, expects_keycloak_dynamic = read_apphost_expectations()

internal_debug = probe(f"{DEFAULT_INTERNAL_BUSINESS_URL}/debug/auth")
local_https_debug = probe(f"{DEFAULT_LOCAL_BUSINESS_URL}/debug/auth", insecure=True)
public_api_no_auth = probe(f"{public_business_url}/api/backoffice/me")
public_keycloak_discovery = probe(f"{public_keycloak_url}/realms/prism-dev/.well-known/openid-configuration")
testsite_seed_ready = probe("https://localhost:44345/api/prism/downstream-demo/seed-contract-ready", insecure=True)
testsite_session_contract = probe("https://localhost:44345/api/prism/downstream-demo/session-contract", insecure=True)
testsite_downstream_no_cookie = probe("https://localhost:44345/api/prism/downstream-demo", insecure=True)
public_testsite_seed_ready = probe(f"{public_testsite_url}/api/prism/downstream-demo/seed-contract-ready")

runtime_debug_json = internal_debug.get("json") or local_https_debug.get("json") or {}
runtime_backchannel_url = runtime_debug_json.get("backchannelUrl") if isinstance(runtime_debug_json, dict) else None
runtime_backchannel_probe = runtime_debug_json.get("backchannelProbe") if isinstance(runtime_debug_json, dict) else None
runtime_tenants = runtime_debug_json.get("tenants") if isinstance(runtime_debug_json, dict) else []
runtime_authority = None
if isinstance(runtime_tenants, list):
    for tenant in runtime_tenants:
        if isinstance(tenant, dict) and tenant.get("OidcAuthority"):
            runtime_authority = str(tenant["OidcAuthority"]).rstrip("/")
            break

realm_path = "/realms/prism-dev"
if runtime_authority and "/realms/" in runtime_authority:
    realm_path = runtime_authority[runtime_authority.index("/realms/"):]
elif expected_authority and "/realms/" in expected_authority:
    realm_path = expected_authority[expected_authority.index("/realms/"):]

runtime_backchannel_discovery = None
runtime_backchannel_certs = None
if runtime_backchannel_url and runtime_backchannel_url != "(not set)":
    base = runtime_backchannel_url.rstrip("/")
    runtime_backchannel_discovery = probe(f"{base}{realm_path}/.well-known/openid-configuration")
    runtime_backchannel_certs = probe(f"{base}{realm_path}/protocol/openid-connect/certs")

internal_api_auth = None
public_api_auth = None
if token:
    internal_api_auth = probe(f"{DEFAULT_INTERNAL_BUSINESS_URL}/api/backoffice/me", token=token)
    public_api_auth = probe(f"{public_business_url}/api/backoffice/me", token=token)

print("Umbraco Prism — Codespaces downstream diagnostics")
print(f"Repo root: {REPO_ROOT}")
print(f"BusinessApp public URL: {public_business_url} ({public_business_source})")
print(f"Keycloak public URL:   {public_keycloak_url} ({public_keycloak_source})")
print(f"TestSite public URL:   {public_testsite_url} ({public_testsite_source})")
if os.environ.get("CODESPACE_NAME"):
    print(f"Codespace name:        {os.environ.get('CODESPACE_NAME')}")
else:
    print("⚠️ Codespaces shell variables were not detected. Public forwarded-port checks may be limited.")
if public_business_source == "fallback" or public_keycloak_source == "fallback":
    print("⚠️ gh codespace ports did not return browse URLs, so fallback hostnames were used.")
    print("   next: gh codespace ports --codespace \"$CODESPACE_NAME\"")

print_section("Safe env snapshot (current shell only)")
print(f"CODESPACE_NAME={safe_env('CODESPACE_NAME')}")
print(f"GITHUB_CODESPACES_PORT_FORWARDING_DOMAIN={safe_env('GITHUB_CODESPACES_PORT_FORWARDING_DOMAIN')}")
print(f"ASPNETCORE_ENVIRONMENT={safe_env('ASPNETCORE_ENVIRONMENT')}")
print(f"TESTSITE_PUBLIC_URL={safe_env('TESTSITE_PUBLIC_URL')}")
print(f"KEYCLOAK_URL={safe_env('KEYCLOAK_URL')}")
print(f"KEYCLOAK_BACKCHANNEL_URL={safe_env('KEYCLOAK_BACKCHANNEL_URL')}")
print(f"BUSINESSAPP_BACKCHANNEL_URL={safe_env('BUSINESSAPP_BACKCHANNEL_URL')}")
print(f"PrismBusinessApp__WorkflowApiBaseUrl={safe_env('PrismBusinessApp__WorkflowApiBaseUrl')}")
print("Note: app runtime env may differ from the terminal; /debug/auth and /session-contract are the runtime truth sources below.")

print_section("Internal service vs public tunnel")
if internal_debug.get("status") == 200:
    report(
        "Internal BusinessApp backchannel",
        "PASS",
        f"{DEFAULT_INTERNAL_BUSINESS_URL}/debug/auth returned JSON ({status_line(internal_debug)}). The internal service is up.",
    )
elif local_https_debug.get("status") == 200:
    report(
        "Internal BusinessApp backchannel",
        "WARN",
        f"{DEFAULT_INTERNAL_BUSINESS_URL}/debug/auth failed with {status_line(internal_debug)}, but {DEFAULT_LOCAL_BUSINESS_URL}/debug/auth succeeded. BusinessApp is running, but the default internal HTTP hop looks stale or wrong.",
        [
            "bash scripts/codespaces/refresh.sh",
            "tail -f artifacts/startup-status/prism-apphost.log",
            "gh codespace ports --codespace \"$CODESPACE_NAME\"",
        ],
    )
else:
    report(
        "Internal BusinessApp backchannel",
        "FAIL",
        f"Neither {DEFAULT_INTERNAL_BUSINESS_URL}/debug/auth nor {DEFAULT_LOCAL_BUSINESS_URL}/debug/auth responded successfully. BusinessApp itself may not be running.",
        [
            "bash scripts/codespaces/health-check.sh",
            "bash scripts/codespaces/refresh.sh",
            "tail -f artifacts/startup-status/prism-apphost.log",
        ],
    )

if public_api_no_auth.get("tunnel_html"):
    report(
        "Public forwarded BusinessApp URL",
        "WARN",
        f"{public_business_url}/api/backoffice/me returned the Codespaces tunnel/auth HTML page instead of API JSON. This is a public forwarding/auth problem, not an internal app outage.",
        [
            "gh codespace ports --codespace \"$CODESPACE_NAME\"",
            f"curl -i {shell_quote(public_business_url + '/api/backoffice/me')}",
        ],
    )
elif public_api_no_auth.get("status") in {400, 401}:
    report(
        "Public forwarded BusinessApp URL",
        "PASS",
        f"{public_business_url}/api/backoffice/me reached BusinessApp ({status_line(public_api_no_auth)}). Public forwarding works; auth is the only thing missing on this probe.",
    )
elif public_api_no_auth.get("network_error"):
    report(
        "Public forwarded BusinessApp URL",
        "FAIL",
        f"{public_business_url}/api/backoffice/me could not be reached ({status_line(public_api_no_auth)}). This points to forwarding or stack readiness, not token validation.",
        [
            "gh codespace ports --codespace \"$CODESPACE_NAME\"",
            "bash scripts/codespaces/health-check.sh",
            "bash scripts/codespaces/refresh.sh",
        ],
    )
else:
    report(
        "Public forwarded BusinessApp URL",
        "WARN",
        f"{public_business_url}/api/backoffice/me returned {status_line(public_api_no_auth)}. That is neither the expected auth response nor the tunnel page, so inspect the raw endpoint directly.",
        [
            f"curl -i {shell_quote(public_business_url + '/api/backoffice/me')}",
            f"curl -sk {shell_quote(DEFAULT_LOCAL_BUSINESS_URL + '/debug/auth')}",
        ],
    )

print_section("TestSite same-origin probes")
if testsite_seed_ready.get("status") == 200:
    report(
        "TestSite seed-contract-ready",
        "PASS",
        "https://localhost:44345/api/prism/downstream-demo/seed-contract-ready returned JSON 200. TestSite is up and its seed contract is ready.",
    )
elif testsite_seed_ready.get("status") == 503:
    report(
        "TestSite seed-contract-ready",
        "WARN",
        "https://localhost:44345/api/prism/downstream-demo/seed-contract-ready reached TestSite but returned 503. The app is up, but the seeded route contract is not ready yet.",
        [
            "bash scripts/codespaces/health-check.sh",
            "bash scripts/codespaces/refresh.sh",
        ],
    )
else:
    report(
        "TestSite seed-contract-ready",
        "FAIL",
        f"https://localhost:44345/api/prism/downstream-demo/seed-contract-ready failed ({status_line(testsite_seed_ready)}).",
        [
            "bash scripts/codespaces/health-check.sh",
            "tail -f artifacts/startup-status/prism-apphost.log",
        ],
    )

session_contract_json = testsite_session_contract.get("json") if isinstance(testsite_session_contract.get("json"), dict) else {}
tenant = session_contract_json.get("tenant") if isinstance(session_contract_json, dict) else {}
cookie = session_contract_json.get("cookie") if isinstance(session_contract_json, dict) else {}
downstream = session_contract_json.get("downstream") if isinstance(session_contract_json, dict) else {}
seed = session_contract_json.get("seed") if isinstance(session_contract_json, dict) else {}

if testsite_session_contract.get("status") == 200 and session_contract_json:
    report(
        "TestSite session-contract",
        "PASS",
        "https://localhost:44345/api/prism/downstream-demo/session-contract returned JSON, so the non-browser probe is available.",
    )
    print(f"   tenantResolved={tenant.get('resolved', '(missing)')}")
    print(f"   tenantHostname={tenant.get('hostname', '(missing)')}")
    print(f"   cookieAuthenticated={cookie.get('isAuthenticated', '(missing)')}")
    print(f"   hasAccessToken={cookie.get('hasAccessToken', '(missing)')}")
    print(f"   accessTokenExpired={cookie.get('accessTokenExpired', '(missing)')}")
    print(f"   authorizationHeaderReady={downstream.get('authorizationHeaderReady', '(missing)')}")
    print(f"   seedReady={seed.get('ready', seed.get('Ready', '(missing)'))}")
    print("   note: terminal requests do not carry your browser cookie, so auth-related flags will usually be false here.")
else:
    report(
        "TestSite session-contract",
        "WARN",
        f"https://localhost:44345/api/prism/downstream-demo/session-contract returned {status_line(testsite_session_contract)}.",
    )

if testsite_downstream_no_cookie.get("status") == 401:
    report(
        "TestSite downstream-demo without browser cookie",
        "PASS",
        "https://localhost:44345/api/prism/downstream-demo returned 401, which proves the same-origin endpoint is present and auth-protected.",
    )
elif testsite_downstream_no_cookie.get("status") == 200:
    report(
        "TestSite downstream-demo without browser cookie",
        "WARN",
        "https://localhost:44345/api/prism/downstream-demo returned 200 without a browser cookie. That is unusual for the current auth contract.",
    )
else:
    report(
        "TestSite downstream-demo without browser cookie",
        "WARN",
        f"https://localhost:44345/api/prism/downstream-demo returned {status_line(testsite_downstream_no_cookie)}.",
    )

if public_testsite_seed_ready.get("tunnel_html"):
    report(
        "Public TestSite seed-contract-ready",
        "WARN",
        f"{public_testsite_url}/api/prism/downstream-demo/seed-contract-ready returned the Codespaces tunnel/auth HTML page instead of app JSON.",
        [
            "gh codespace ports --codespace \"$CODESPACE_NAME\"",
            f"curl -i {shell_quote(public_testsite_url + '/api/prism/downstream-demo/seed-contract-ready')}",
        ],
    )
elif public_testsite_seed_ready.get("status") == 200:
    report(
        "Public TestSite seed-contract-ready",
        "PASS",
        f"{public_testsite_url}/api/prism/downstream-demo/seed-contract-ready returned JSON 200, so the forwarded TestSite URL is serving the app.",
    )
elif public_testsite_seed_ready.get("status") == 503:
    report(
        "Public TestSite seed-contract-ready",
        "WARN",
        f"{public_testsite_url}/api/prism/downstream-demo/seed-contract-ready reached TestSite but reported not-ready (503).",
    )
elif public_testsite_seed_ready.get("redirect"):
    report(
        "Public TestSite seed-contract-ready",
        "WARN",
        f"{public_testsite_url}/api/prism/downstream-demo/seed-contract-ready redirected ({status_line(public_testsite_seed_ready)}). Do not treat that as app success.",
        [
            f"curl -i {shell_quote(public_testsite_url + '/api/prism/downstream-demo/seed-contract-ready')}",
        ],
    )
else:
    report(
        "Public TestSite seed-contract-ready",
        "WARN",
        f"{public_testsite_url}/api/prism/downstream-demo/seed-contract-ready returned {status_line(public_testsite_seed_ready)}.",
    )

print_section("BusinessApp availability vs token validation")
if not token:
    report(
        "Authenticated downstream check",
        "SKIP",
        "No bearer token supplied, so the script can only prove transport and forwarding. Token-validation checks were skipped.",
        [
            "In the browser console, run: (async () => { const r = await fetch('/api/prism/downstream-demo/session-contract'); console.log(await r.json()); })()",
            "Then rerun with: PRISM_BEARER_TOKEN='<access-token>' bash scripts/codespaces/diagnose-downstream.sh",
        ],
    )
else:
    if internal_api_auth and internal_api_auth.get("status") == 200:
        report(
            "Authenticated internal backchannel",
            "PASS",
            f"{DEFAULT_INTERNAL_BUSINESS_URL}/api/backoffice/me accepted the bearer token ({status_line(internal_api_auth)}). BusinessApp and Keycloak validation are both healthy on the internal hop.",
        )
    elif internal_api_auth and internal_api_auth.get("status") == 401:
        report(
            "Authenticated internal backchannel",
            "WARN",
            f"{DEFAULT_INTERNAL_BUSINESS_URL}/api/backoffice/me is reachable but rejected the bearer token ({status_line(internal_api_auth)}). This is an auth/session/Keycloak-validation problem, not a BusinessApp outage.",
            [
                f"curl -sk {shell_quote(DEFAULT_LOCAL_BUSINESS_URL + '/debug/auth')}",
                "bash scripts/codespaces/refresh.sh",
            ],
        )
    elif internal_api_auth and internal_api_auth.get("network_error"):
        report(
            "Authenticated internal backchannel",
            "FAIL",
            f"Authenticated call to {DEFAULT_INTERNAL_BUSINESS_URL}/api/backoffice/me never reached BusinessApp ({status_line(internal_api_auth)}). Fix internal service reachability first.",
            [
                "bash scripts/codespaces/refresh.sh",
                "tail -f artifacts/startup-status/prism-apphost.log",
            ],
        )
    else:
        report(
            "Authenticated internal backchannel",
            "WARN",
            f"Authenticated call returned {status_line(internal_api_auth or {})}. Inspect the app response body and Keycloak diagnostics next.",
            [
                f"curl -sk {shell_quote(DEFAULT_LOCAL_BUSINESS_URL + '/debug/auth')}",
            ],
        )

    if public_api_auth and public_api_auth.get("tunnel_html"):
        report(
            "Authenticated public BusinessApp URL",
            "WARN",
            f"Even with a bearer token, {public_business_url}/api/backoffice/me returned the Codespaces tunnel/auth page. The public forwarding layer is still intercepting the request.",
            [
                "gh codespace ports --codespace \"$CODESPACE_NAME\"",
                f"curl -i {shell_quote(public_business_url + '/api/backoffice/me')}",
            ],
        )
    elif public_api_auth and public_api_auth.get("status") == 200:
        report(
            "Authenticated public BusinessApp URL",
            "PASS",
            f"{public_business_url}/api/backoffice/me also accepted the token ({status_line(public_api_auth)}). Browser-facing auth path looks healthy too.",
        )
    elif public_api_auth and public_api_auth.get("status") == 401:
        report(
            "Authenticated public BusinessApp URL",
            "WARN",
            f"Public BusinessApp URL is reachable but rejected the token ({status_line(public_api_auth)}). That aligns with an auth/token issue rather than a forwarding outage.",
            [
                "bash scripts/codespaces/refresh.sh",
                f"curl -sk {shell_quote(DEFAULT_LOCAL_BUSINESS_URL + '/debug/auth')}",
            ],
        )

print_section("Keycloak backchannel health")
if public_keycloak_discovery.get("status") == 200:
    issuer = None
    if isinstance(public_keycloak_discovery.get("json"), dict):
        issuer = public_keycloak_discovery["json"].get("issuer")
    report(
        "Public Keycloak discovery",
        "PASS",
        f"{public_keycloak_url}/realms/prism-dev/.well-known/openid-configuration responded ({status_line(public_keycloak_discovery)}). Issuer: {issuer or '(missing from payload)'}.",
    )
else:
    report(
        "Public Keycloak discovery",
        "FAIL",
        f"Public Keycloak discovery failed at {public_keycloak_url}/realms/prism-dev/.well-known/openid-configuration ({status_line(public_keycloak_discovery)}). Fix stack readiness before chasing token bugs.",
        [
            "bash scripts/codespaces/health-check.sh",
            "bash scripts/codespaces/refresh.sh",
        ],
    )

if runtime_backchannel_url and runtime_backchannel_url != "(not set)":
    runtime_message = f"BusinessApp reports KEYCLOAK_BACKCHANNEL_URL={runtime_backchannel_url}."
    if runtime_backchannel_probe:
        runtime_message += f" Probe: {runtime_backchannel_probe}."

    stale_reasons = []
    if expects_keycloak_dynamic and runtime_backchannel_discovery and runtime_backchannel_discovery.get("status") != 200:
        stale_reasons.append("repo expects a dynamic Keycloak backchannel, but the running BusinessApp cannot fetch discovery over that backchannel")
    if expects_keycloak_dynamic and runtime_authority and public_keycloak_source != "local" and public_keycloak_url not in runtime_authority:
        stale_reasons.append("running BusinessApp still trusts a different public OIDC authority than the current Codespaces forwarded URL")
    if runtime_backchannel_certs and runtime_backchannel_certs.get("status") != 200:
        stale_reasons.append("the backchannel JWKS endpoint is not returning 200")

    if stale_reasons:
        report(
            "Runtime Keycloak backchannel",
            "WARN",
            runtime_message + " This looks stale or broken because " + "; ".join(stale_reasons) + ".",
            [
                "bash scripts/codespaces/refresh.sh",
                f"curl -sk {shell_quote(DEFAULT_LOCAL_BUSINESS_URL + '/debug/auth')}",
                "tail -f artifacts/startup-status/prism-apphost.log",
            ],
        )
    else:
        report(
            "Runtime Keycloak backchannel",
            "PASS",
            runtime_message + " Discovery/JWKS checks line up with the current repo wiring, so a remaining 401 is more likely a stale token/session than a bad endpoint.",
        )
else:
    if expects_keycloak_dynamic:
        report(
            "Runtime Keycloak backchannel",
            "WARN",
            "BusinessApp /debug/auth did not report a KEYCLOAK_BACKCHANNEL_URL even though the repo expects one in Codespaces. That usually means the running stack is stale or needs a restart.",
            [
                "bash scripts/codespaces/refresh.sh",
                f"curl -sk {shell_quote(DEFAULT_LOCAL_BUSINESS_URL + '/debug/auth')}",
            ],
        )
    else:
        report(
            "Runtime Keycloak backchannel",
            "SKIP",
            "BusinessApp /debug/auth did not expose a KEYCLOAK_BACKCHANNEL_URL, and the repo does not currently insist on one.",
        )

if expected_authority:
    print(f"\nRepo expected fallback OIDC authority: {expected_authority}")
if runtime_authority:
    print(f"Runtime BusinessApp OIDC authority:      {runtime_authority}")
if expects_business_dynamic:
    print("Repo expectation: BUSINESSAPP_BACKCHANNEL_URL should be discovered dynamically in Codespaces.")
if expects_keycloak_dynamic:
    print("Repo expectation: KEYCLOAK_BACKCHANNEL_URL should be discovered dynamically in Codespaces.")
PY
