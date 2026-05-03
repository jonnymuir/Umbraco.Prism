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

PROGRAM_CS="$REPO_ROOT/src/UmbracoPrism.AppHost/Program.cs"
APPSETTINGS_JSON="$REPO_ROOT/src/UmbracoPrism.MockBusinessApp/appsettings.json"

DEFAULT_INTERNAL_BUSINESS_URL="${PRISM_BUSINESSAPP_INTERNAL_URL:-http://localhost:5163}"
DEFAULT_INTERNAL_BUSINESS_URL="${DEFAULT_INTERNAL_BUSINESS_URL%/}"
DEFAULT_LOCAL_BUSINESS_URL="${PRISM_LOCAL_BUSINESSAPP_URL:-https://localhost:7245}"
DEFAULT_LOCAL_BUSINESS_URL="${DEFAULT_LOCAL_BUSINESS_URL%/}"

usage() {
    cat <<'EOF'
Usage:
  bash scripts/codespaces/diagnose-downstream.sh [--token <bearer-token>]

Optional environment variables:
  PRISM_BEARER_TOKEN              Bearer token to test authenticated downstream calls
  PRISM_BUSINESSAPP_INTERNAL_URL  Override default internal BusinessApp URL
  PRISM_LOCAL_BUSINESSAPP_URL     Override default local HTTPS BusinessApp URL
EOF
}

trim() {
    local value="${1-}"
    value="${value#"${value%%[![:space:]]*}"}"
    value="${value%"${value##*[![:space:]]}"}"
    printf '%s' "$value"
}

shell_quote() {
    printf "'%s'" "$(printf '%s' "$1" | sed "s/'/'\"'\"'/g")"
}

safe_env() {
    local name="$1"
    local value
    value="$(trim "${!name-}")"
    if [ -n "$value" ]; then
        printf '%s' "$value"
    else
        printf '%s' '(not set in current shell)'
    fi
}

print_section() {
    printf '\n== %s ==\n' "$1"
}

report() {
    local label="$1"
    local verdict="$2"
    local message="$3"
    shift 3

    local icon='⚠️'
    case "$verdict" in
        PASS) icon='✅' ;;
        WARN) icon='⚠️' ;;
        FAIL) icon='❌' ;;
        SKIP) icon='⏭️' ;;
    esac

    printf '%s %s: %s\n' "$icon" "$label" "$message"
    local step
    for step in "$@"; do
        printf '   next: %s\n' "$step"
    done
}

probe_get() {
    local var_name="${1}_${2}"
    printf '%s' "${!var_name-}"
}

status_line() {
    local prefix="$1"
    if [ "$(probe_get "$prefix" network_error)" = 'true' ]; then
        printf 'network error (%s)' "$(probe_get "$prefix" error)"
    else
        printf 'HTTP %s' "$(probe_get "$prefix" status)"
    fi
}

json_compact() {
    printf '%s' "$1" | tr '\r\n' '  '
}

json_get_string() {
    local compact key escaped_key
    compact="$(json_compact "$1")"
    key="$2"
    escaped_key="$(printf '%s' "$key" | sed 's/[][\\/.^$*]/\\&/g')"
    printf '%s' "$compact" | sed -nE "s/.*\"${escaped_key}\"[[:space:]]*:[[:space:]]*\"([^\"]*)\".*/\\1/p" | head -n 1
}

json_get_bool() {
    local compact key escaped_key
    compact="$(json_compact "$1")"
    key="$2"
    escaped_key="$(printf '%s' "$key" | sed 's/[][\\/.^$*]/\\&/g')"
    printf '%s' "$compact" | sed -nE "s/.*\"${escaped_key}\"[[:space:]]*:[[:space:]]*(true|false).*/\\1/p" | head -n 1
}

extract_realm_path() {
    if [ -z "${1-}" ]; then
        return 0
    fi

    printf '%s' "$1" | sed -nE 's#^[a-zA-Z]+://[^/]+(/realms/.*)$#\1#p' | head -n 1
}

read_expected_authority() {
    sed -nE 's/.*"OidcAuthority"[[:space:]]*:[[:space:]]*"([^"]+)".*/\1/p' "$APPSETTINGS_JSON" | head -n 1
}

read_apphost_expectations() {
    if grep -Fq '.WithEnvironment("BUSINESSAPP_BACKCHANNEL_URL", businessApp.GetEndpoint("http"))' "$PROGRAM_CS"; then
        EXPECTS_BUSINESS_DYNAMIC='true'
    else
        EXPECTS_BUSINESS_DYNAMIC='false'
    fi

    if grep -Fq 'businessApp.WithEnvironment("KEYCLOAK_BACKCHANNEL_URL", keycloak.GetEndpoint("http"))' "$PROGRAM_CS"; then
        EXPECTS_KEYCLOAK_DYNAMIC='true'
    else
        EXPECTS_KEYCLOAK_DYNAMIC='false'
    fi
}

discover_public_url() {
    local port="$1"
    local url_var="$2"
    local source_var="$3"
    local codespace_name browse_url domain

    codespace_name="$(trim "${CODESPACE_NAME:-}")"
    if [ -z "$codespace_name" ]; then
        printf -v "$url_var" 'https://localhost:%s' "$port"
        printf -v "$source_var" 'local'
        return 0
    fi

    browse_url=''
    if command -v gh >/dev/null 2>&1; then
        browse_url="$(gh codespace ports --codespace "$codespace_name" --json sourcePort,browseUrl --jq ".[] | select(.sourcePort==$port) | .browseUrl" 2>/dev/null | head -n 1 | tr -d '\r')"
        browse_url="${browse_url%/}"
        if [ -n "$browse_url" ] && [ "$browse_url" != 'null' ]; then
            printf -v "$url_var" '%s' "$browse_url"
            printf -v "$source_var" 'gh'
            return 0
        fi
    fi

    domain="$(trim "${GITHUB_CODESPACES_PORT_FORWARDING_DOMAIN:-app.github.dev}")"
    if [ -z "$domain" ]; then
        domain='app.github.dev'
    fi

    printf -v "$url_var" 'https://%s-%s.%s' "$codespace_name" "$port" "$domain"
    printf -v "$source_var" 'fallback'
}

probe_into() {
    local prefix="$1"
    local url="$2"
    local insecure="$3"
    local token_value="${4-}"
    local timeout="${5-6}"
    local response body meta status content_type redirect_url error curl_exit ok redirect tunnel_html
    local -a curl_args

    curl_args=(
        -sS
        --max-time "$timeout"
        --connect-timeout "$timeout"
        -H 'User-Agent: UmbracoPrism-Diagnostics/1.0'
        -H 'Accept: application/json, text/html;q=0.9, */*;q=0.1'
        -w $'\n__PRISM_META__\nhttp_code=%{http_code}\ncontent_type=%{content_type}\nredirect_url=%{redirect_url}\n'
    )

    if [ "$insecure" = 'true' ]; then
        curl_args+=(-k)
    fi

    if [ -n "$token_value" ]; then
        curl_args+=(-H "Authorization: Bearer $token_value")
    fi

    curl_exit=0
    response="$(curl "${curl_args[@]}" "$url" 2>&1)" || curl_exit=$?

    if [ "$curl_exit" -ne 0 ]; then
        error="$(printf '%s' "$response" | tr '\n' ' ' | sed 's/[[:space:]]\+/ /g; s/^ //; s/ $//')"
        if [ -z "$error" ]; then
            error="curl exited with status $curl_exit"
        fi

        printf -v "${prefix}_network_error" '%s' 'true'
        printf -v "${prefix}_error" '%s' "$error"
        printf -v "${prefix}_status" '%s' ''
        printf -v "${prefix}_content_type" '%s' ''
        printf -v "${prefix}_location" '%s' ''
        printf -v "${prefix}_body" '%s' ''
        printf -v "${prefix}_tunnel_html" '%s' 'false'
        printf -v "${prefix}_redirect" '%s' 'false'
        return 0
    fi

    if [[ "$response" == *$'\n__PRISM_META__\n'* ]]; then
        body="${response%%$'\n__PRISM_META__\n'*}"
        meta="${response#*$'\n__PRISM_META__\n'}"
    else
        body="$response"
        meta=''
    fi

    status="$(printf '%s' "$meta" | sed -n 's/^http_code=//p' | head -n 1)"
    content_type="$(printf '%s' "$meta" | sed -n 's/^content_type=//p' | head -n 1 | tr '[:upper:]' '[:lower:]')"
    content_type="${content_type%%;*}"
    redirect_url="$(printf '%s' "$meta" | sed -n 's/^redirect_url=//p' | head -n 1)"

    ok='false'
    redirect='false'
    if [ -n "$status" ] && [ "$status" -ge 200 ] && [ "$status" -lt 300 ]; then
        ok='true'
    fi
    if [ -n "$status" ] && [ "$status" -ge 300 ] && [ "$status" -lt 400 ]; then
        redirect='true'
    fi

    tunnel_html='false'
    if printf '%s' "$body" | grep -qi 'Connecting to the forwarded port\|forwarded port'; then
        tunnel_html='true'
    fi

    printf -v "${prefix}_network_error" '%s' 'false'
    printf -v "${prefix}_error" '%s' ''
    printf -v "${prefix}_status" '%s' "$status"
    printf -v "${prefix}_content_type" '%s' "$content_type"
    printf -v "${prefix}_location" '%s' "$redirect_url"
    printf -v "${prefix}_body" '%s' "$body"
    printf -v "${prefix}_tunnel_html" '%s' "$tunnel_html"
    printf -v "${prefix}_redirect" '%s' "$redirect"
}

join_by() {
    local separator="$1"
    shift
    local first="${1-}"
    if [ "$#" -eq 0 ]; then
        return 0
    fi

    shift
    printf '%s' "$first"
    local value
    for value in "$@"; do
        printf '%s%s' "$separator" "$value"
    done
}

TOKEN="$(trim "${PRISM_BEARER_TOKEN:-}")"
while [ "$#" -gt 0 ]; do
    case "$1" in
        --token)
            shift
            if [ "$#" -eq 0 ]; then
                echo 'diagnose-downstream.sh: --token requires a value.' >&2
                usage >&2
                exit 1
            fi
            TOKEN="$(trim "$1")"
            ;;
        -h|--help)
            usage
            exit 0
            ;;
        *)
            echo "diagnose-downstream.sh: unknown argument: $1" >&2
            usage >&2
            exit 1
            ;;
    esac
    shift
done

EXPECTED_AUTHORITY="$(read_expected_authority || true)"
read_apphost_expectations

discover_public_url 7245 PUBLIC_BUSINESS_URL PUBLIC_BUSINESS_SOURCE
discover_public_url 8443 PUBLIC_KEYCLOAK_URL PUBLIC_KEYCLOAK_SOURCE
discover_public_url 44345 PUBLIC_TESTSITE_URL PUBLIC_TESTSITE_SOURCE

probe_into INTERNAL_DEBUG "${DEFAULT_INTERNAL_BUSINESS_URL}/debug/auth" false ''
probe_into LOCAL_HTTPS_DEBUG "${DEFAULT_LOCAL_BUSINESS_URL}/debug/auth" true ''
probe_into PUBLIC_API_NO_AUTH "${PUBLIC_BUSINESS_URL}/api/backoffice/me" false ''
probe_into PUBLIC_KEYCLOAK_DISCOVERY "${PUBLIC_KEYCLOAK_URL}/realms/prism-dev/.well-known/openid-configuration" false ''
probe_into TESTSITE_SEED_READY 'https://localhost:44345/api/prism/downstream-demo/seed-contract-ready' true ''
probe_into TESTSITE_SESSION_CONTRACT 'https://localhost:44345/api/prism/downstream-demo/session-contract' true ''
probe_into TESTSITE_DOWNSTREAM_NO_COOKIE 'https://localhost:44345/api/prism/downstream-demo' true ''
probe_into PUBLIC_TESTSITE_SEED_READY "${PUBLIC_TESTSITE_URL}/api/prism/downstream-demo/seed-contract-ready" false ''

RUNTIME_DEBUG_BODY=''
if [ "$(probe_get INTERNAL_DEBUG content_type)" = 'application/json' ] && [ -n "$(probe_get INTERNAL_DEBUG body)" ]; then
    RUNTIME_DEBUG_BODY="$(probe_get INTERNAL_DEBUG body)"
elif [ "$(probe_get LOCAL_HTTPS_DEBUG content_type)" = 'application/json' ] && [ -n "$(probe_get LOCAL_HTTPS_DEBUG body)" ]; then
    RUNTIME_DEBUG_BODY="$(probe_get LOCAL_HTTPS_DEBUG body)"
fi

RUNTIME_BACKCHANNEL_URL="$(json_get_string "$RUNTIME_DEBUG_BODY" 'backchannelUrl')"
RUNTIME_BACKCHANNEL_PROBE="$(json_get_string "$RUNTIME_DEBUG_BODY" 'backchannelProbe')"
RUNTIME_AUTHORITY="$(json_get_string "$RUNTIME_DEBUG_BODY" 'OidcAuthority')"
REALM_PATH="$(extract_realm_path "$RUNTIME_AUTHORITY")"
if [ -z "$REALM_PATH" ]; then
    REALM_PATH="$(extract_realm_path "$EXPECTED_AUTHORITY")"
fi
if [ -z "$REALM_PATH" ]; then
    REALM_PATH='/realms/prism-dev'
fi

if [ -n "$RUNTIME_BACKCHANNEL_URL" ] && [ "$RUNTIME_BACKCHANNEL_URL" != '(not set)' ]; then
    probe_into RUNTIME_BACKCHANNEL_DISCOVERY "${RUNTIME_BACKCHANNEL_URL%/}${REALM_PATH}/.well-known/openid-configuration" false ''
    probe_into RUNTIME_BACKCHANNEL_CERTS "${RUNTIME_BACKCHANNEL_URL%/}${REALM_PATH}/protocol/openid-connect/certs" false ''
fi

if [ -n "$TOKEN" ]; then
    probe_into INTERNAL_API_AUTH "${DEFAULT_INTERNAL_BUSINESS_URL}/api/backoffice/me" false "$TOKEN"
    probe_into PUBLIC_API_AUTH "${PUBLIC_BUSINESS_URL}/api/backoffice/me" false "$TOKEN"
fi

printf 'Umbraco Prism — Codespaces downstream diagnostics\n'
printf 'Repo root: %s\n' "$REPO_ROOT"
printf 'BusinessApp public URL: %s (%s)\n' "$PUBLIC_BUSINESS_URL" "$PUBLIC_BUSINESS_SOURCE"
printf 'Keycloak public URL:   %s (%s)\n' "$PUBLIC_KEYCLOAK_URL" "$PUBLIC_KEYCLOAK_SOURCE"
printf 'TestSite public URL:   %s (%s)\n' "$PUBLIC_TESTSITE_URL" "$PUBLIC_TESTSITE_SOURCE"
if [ -n "$(trim "${CODESPACE_NAME:-}")" ]; then
    printf 'Codespace name:        %s\n' "$(trim "${CODESPACE_NAME}")"
else
    printf '⚠️ Codespaces shell variables were not detected. Public forwarded-port checks may be limited.\n'
fi
if [ "$PUBLIC_BUSINESS_SOURCE" = 'fallback' ] || [ "$PUBLIC_KEYCLOAK_SOURCE" = 'fallback' ]; then
    printf '⚠️ gh codespace ports did not return browse URLs, so fallback hostnames were used.\n'
    printf '   next: gh codespace ports --codespace "$CODESPACE_NAME"\n'
fi

print_section 'Safe env snapshot (current shell only)'
printf 'CODESPACE_NAME=%s\n' "$(safe_env 'CODESPACE_NAME')"
printf 'GITHUB_CODESPACES_PORT_FORWARDING_DOMAIN=%s\n' "$(safe_env 'GITHUB_CODESPACES_PORT_FORWARDING_DOMAIN')"
printf 'ASPNETCORE_ENVIRONMENT=%s\n' "$(safe_env 'ASPNETCORE_ENVIRONMENT')"
printf 'TESTSITE_PUBLIC_URL=%s\n' "$(safe_env 'TESTSITE_PUBLIC_URL')"
printf 'KEYCLOAK_URL=%s\n' "$(safe_env 'KEYCLOAK_URL')"
printf 'KEYCLOAK_BACKCHANNEL_URL=%s\n' "$(safe_env 'KEYCLOAK_BACKCHANNEL_URL')"
printf 'BUSINESSAPP_BACKCHANNEL_URL=%s\n' "$(safe_env 'BUSINESSAPP_BACKCHANNEL_URL')"
printf 'PrismBusinessApp__WorkflowApiBaseUrl=%s\n' "$(safe_env 'PrismBusinessApp__WorkflowApiBaseUrl')"
printf 'Note: app runtime env may differ from the terminal; /debug/auth and /session-contract are the runtime truth sources below.\n'

print_section 'Internal service vs public tunnel'
if [ "$(probe_get INTERNAL_DEBUG status)" = '200' ]; then
    report \
        'Internal BusinessApp backchannel' \
        'PASS' \
        "${DEFAULT_INTERNAL_BUSINESS_URL}/debug/auth returned JSON ($(status_line INTERNAL_DEBUG)). The internal service is up."
elif [ "$(probe_get LOCAL_HTTPS_DEBUG status)" = '200' ]; then
    report \
        'Internal BusinessApp backchannel' \
        'WARN' \
        "${DEFAULT_INTERNAL_BUSINESS_URL}/debug/auth failed with $(status_line INTERNAL_DEBUG), but ${DEFAULT_LOCAL_BUSINESS_URL}/debug/auth succeeded. BusinessApp is running, but the default internal HTTP hop looks stale or wrong." \
        'bash scripts/codespaces/refresh.sh' \
        'tail -f artifacts/startup-status/prism-apphost.log' \
        'gh codespace ports --codespace "$CODESPACE_NAME"'
else
    report \
        'Internal BusinessApp backchannel' \
        'FAIL' \
        "Neither ${DEFAULT_INTERNAL_BUSINESS_URL}/debug/auth nor ${DEFAULT_LOCAL_BUSINESS_URL}/debug/auth responded successfully. BusinessApp itself may not be running." \
        'bash scripts/codespaces/health-check.sh' \
        'bash scripts/codespaces/refresh.sh' \
        'tail -f artifacts/startup-status/prism-apphost.log'
fi

if [ "$(probe_get PUBLIC_API_NO_AUTH tunnel_html)" = 'true' ]; then
    report \
        'Public forwarded BusinessApp URL' \
        'WARN' \
        "${PUBLIC_BUSINESS_URL}/api/backoffice/me returned the Codespaces tunnel/auth HTML page instead of API JSON. This is a public forwarding/auth problem, not an internal app outage." \
        'gh codespace ports --codespace "$CODESPACE_NAME"' \
        "curl -i $(shell_quote "${PUBLIC_BUSINESS_URL}/api/backoffice/me")"
elif [ "$(probe_get PUBLIC_API_NO_AUTH status)" = '400' ] || [ "$(probe_get PUBLIC_API_NO_AUTH status)" = '401' ]; then
    report \
        'Public forwarded BusinessApp URL' \
        'PASS' \
        "${PUBLIC_BUSINESS_URL}/api/backoffice/me reached BusinessApp ($(status_line PUBLIC_API_NO_AUTH)). Public forwarding works; auth is the only thing missing on this probe."
elif [ "$(probe_get PUBLIC_API_NO_AUTH network_error)" = 'true' ]; then
    report \
        'Public forwarded BusinessApp URL' \
        'FAIL' \
        "${PUBLIC_BUSINESS_URL}/api/backoffice/me could not be reached ($(status_line PUBLIC_API_NO_AUTH)). This points to forwarding or stack readiness, not token validation." \
        'gh codespace ports --codespace "$CODESPACE_NAME"' \
        'bash scripts/codespaces/health-check.sh' \
        'bash scripts/codespaces/refresh.sh'
else
    report \
        'Public forwarded BusinessApp URL' \
        'WARN' \
        "${PUBLIC_BUSINESS_URL}/api/backoffice/me returned $(status_line PUBLIC_API_NO_AUTH). That is neither the expected auth response nor the tunnel page, so inspect the raw endpoint directly." \
        "curl -i $(shell_quote "${PUBLIC_BUSINESS_URL}/api/backoffice/me")" \
        "curl -sk $(shell_quote "${DEFAULT_LOCAL_BUSINESS_URL}/debug/auth")"
fi

print_section 'TestSite same-origin probes'
if [ "$(probe_get TESTSITE_SEED_READY status)" = '200' ]; then
    report \
        'TestSite seed-contract-ready' \
        'PASS' \
        'https://localhost:44345/api/prism/downstream-demo/seed-contract-ready returned JSON 200. TestSite is up and its seed contract is ready.'
elif [ "$(probe_get TESTSITE_SEED_READY status)" = '503' ]; then
    report \
        'TestSite seed-contract-ready' \
        'WARN' \
        'https://localhost:44345/api/prism/downstream-demo/seed-contract-ready reached TestSite but returned 503. The app is up, but the seeded route contract is not ready yet.' \
        'bash scripts/codespaces/health-check.sh' \
        'bash scripts/codespaces/refresh.sh'
else
    report \
        'TestSite seed-contract-ready' \
        'FAIL' \
        "https://localhost:44345/api/prism/downstream-demo/seed-contract-ready failed ($(status_line TESTSITE_SEED_READY))." \
        'bash scripts/codespaces/health-check.sh' \
        'tail -f artifacts/startup-status/prism-apphost.log'
fi

SESSION_CONTRACT_BODY=''
if [ "$(probe_get TESTSITE_SESSION_CONTRACT content_type)" = 'application/json' ] && [ -n "$(probe_get TESTSITE_SESSION_CONTRACT body)" ]; then
    SESSION_CONTRACT_BODY="$(probe_get TESTSITE_SESSION_CONTRACT body)"
fi

TENANT_RESOLVED="$(json_get_bool "$SESSION_CONTRACT_BODY" 'resolved')"
TENANT_HOSTNAME="$(json_get_string "$SESSION_CONTRACT_BODY" 'hostname')"
COOKIE_AUTHENTICATED="$(json_get_bool "$SESSION_CONTRACT_BODY" 'isAuthenticated')"
HAS_ACCESS_TOKEN="$(json_get_bool "$SESSION_CONTRACT_BODY" 'hasAccessToken')"
ACCESS_TOKEN_EXPIRED="$(json_get_bool "$SESSION_CONTRACT_BODY" 'accessTokenExpired')"
AUTHORIZATION_HEADER_READY="$(json_get_bool "$SESSION_CONTRACT_BODY" 'authorizationHeaderReady')"
SEED_READY_FLAG="$(json_get_bool "$SESSION_CONTRACT_BODY" 'ready')"
if [ -z "$SEED_READY_FLAG" ]; then
    SEED_READY_FLAG="$(json_get_bool "$SESSION_CONTRACT_BODY" 'Ready')"
fi

if [ "$(probe_get TESTSITE_SESSION_CONTRACT status)" = '200' ] && [ -n "$SESSION_CONTRACT_BODY" ]; then
    report \
        'TestSite session-contract' \
        'PASS' \
        'https://localhost:44345/api/prism/downstream-demo/session-contract returned JSON, so the non-browser probe is available.'
    printf '   tenantResolved=%s\n' "${TENANT_RESOLVED:-'(missing)'}"
    printf '   tenantHostname=%s\n' "${TENANT_HOSTNAME:-'(missing)'}"
    printf '   cookieAuthenticated=%s\n' "${COOKIE_AUTHENTICATED:-'(missing)'}"
    printf '   hasAccessToken=%s\n' "${HAS_ACCESS_TOKEN:-'(missing)'}"
    printf '   accessTokenExpired=%s\n' "${ACCESS_TOKEN_EXPIRED:-'(missing)'}"
    printf '   authorizationHeaderReady=%s\n' "${AUTHORIZATION_HEADER_READY:-'(missing)'}"
    printf '   seedReady=%s\n' "${SEED_READY_FLAG:-'(missing)'}"
    printf '   note: terminal requests do not carry your browser cookie, so auth-related flags will usually be false here.\n'
else
    report \
        'TestSite session-contract' \
        'WARN' \
        "https://localhost:44345/api/prism/downstream-demo/session-contract returned $(status_line TESTSITE_SESSION_CONTRACT)."
fi

if [ "$(probe_get TESTSITE_DOWNSTREAM_NO_COOKIE status)" = '401' ]; then
    report \
        'TestSite downstream-demo without browser cookie' \
        'PASS' \
        'https://localhost:44345/api/prism/downstream-demo returned 401, which proves the same-origin endpoint is present and auth-protected.'
elif [ "$(probe_get TESTSITE_DOWNSTREAM_NO_COOKIE status)" = '200' ]; then
    report \
        'TestSite downstream-demo without browser cookie' \
        'WARN' \
        'https://localhost:44345/api/prism/downstream-demo returned 200 without a browser cookie. That is unusual for the current auth contract.'
else
    report \
        'TestSite downstream-demo without browser cookie' \
        'WARN' \
        "https://localhost:44345/api/prism/downstream-demo returned $(status_line TESTSITE_DOWNSTREAM_NO_COOKIE)."
fi

if [ "$(probe_get PUBLIC_TESTSITE_SEED_READY tunnel_html)" = 'true' ]; then
    report \
        'Public TestSite seed-contract-ready' \
        'WARN' \
        "${PUBLIC_TESTSITE_URL}/api/prism/downstream-demo/seed-contract-ready returned the Codespaces tunnel/auth HTML page instead of app JSON." \
        'gh codespace ports --codespace "$CODESPACE_NAME"' \
        "curl -i $(shell_quote "${PUBLIC_TESTSITE_URL}/api/prism/downstream-demo/seed-contract-ready")"
elif [ "$(probe_get PUBLIC_TESTSITE_SEED_READY status)" = '200' ]; then
    report \
        'Public TestSite seed-contract-ready' \
        'PASS' \
        "${PUBLIC_TESTSITE_URL}/api/prism/downstream-demo/seed-contract-ready returned JSON 200, so the forwarded TestSite URL is serving the app."
elif [ "$(probe_get PUBLIC_TESTSITE_SEED_READY status)" = '503' ]; then
    report \
        'Public TestSite seed-contract-ready' \
        'WARN' \
        "${PUBLIC_TESTSITE_URL}/api/prism/downstream-demo/seed-contract-ready reached TestSite but reported not-ready (503)."
elif [ "$(probe_get PUBLIC_TESTSITE_SEED_READY redirect)" = 'true' ]; then
    report \
        'Public TestSite seed-contract-ready' \
        'WARN' \
        "${PUBLIC_TESTSITE_URL}/api/prism/downstream-demo/seed-contract-ready redirected ($(status_line PUBLIC_TESTSITE_SEED_READY)). Do not treat that as app success." \
        "curl -i $(shell_quote "${PUBLIC_TESTSITE_URL}/api/prism/downstream-demo/seed-contract-ready")"
else
    report \
        'Public TestSite seed-contract-ready' \
        'WARN' \
        "${PUBLIC_TESTSITE_URL}/api/prism/downstream-demo/seed-contract-ready returned $(status_line PUBLIC_TESTSITE_SEED_READY)."
fi

print_section 'BusinessApp availability vs token validation'
if [ -z "$TOKEN" ]; then
    report \
        'Authenticated downstream check' \
        'SKIP' \
        'No bearer token supplied, so the script can only prove transport and forwarding. Token-validation checks were skipped.' \
        "In the browser console, run: (async () => { const r = await fetch('/api/prism/downstream-demo/session-contract'); console.log(await r.json()); })()" \
        "Then rerun with: PRISM_BEARER_TOKEN='<access-token>' bash scripts/codespaces/diagnose-downstream.sh"
else
    if [ "$(probe_get INTERNAL_API_AUTH status)" = '200' ]; then
        report \
            'Authenticated internal backchannel' \
            'PASS' \
            "${DEFAULT_INTERNAL_BUSINESS_URL}/api/backoffice/me accepted the bearer token ($(status_line INTERNAL_API_AUTH)). BusinessApp and Keycloak validation are both healthy on the internal hop."
    elif [ "$(probe_get INTERNAL_API_AUTH status)" = '401' ]; then
        report \
            'Authenticated internal backchannel' \
            'WARN' \
            "${DEFAULT_INTERNAL_BUSINESS_URL}/api/backoffice/me is reachable but rejected the bearer token ($(status_line INTERNAL_API_AUTH)). This is an auth/session/Keycloak-validation problem, not a BusinessApp outage." \
            "curl -sk $(shell_quote "${DEFAULT_LOCAL_BUSINESS_URL}/debug/auth")" \
            'bash scripts/codespaces/refresh.sh'
    elif [ "$(probe_get INTERNAL_API_AUTH network_error)" = 'true' ]; then
        report \
            'Authenticated internal backchannel' \
            'FAIL' \
            "Authenticated call to ${DEFAULT_INTERNAL_BUSINESS_URL}/api/backoffice/me never reached BusinessApp ($(status_line INTERNAL_API_AUTH)). Fix internal service reachability first." \
            'bash scripts/codespaces/refresh.sh' \
            'tail -f artifacts/startup-status/prism-apphost.log'
    else
        report \
            'Authenticated internal backchannel' \
            'WARN' \
            "Authenticated call returned $(status_line INTERNAL_API_AUTH). Inspect the app response body and Keycloak diagnostics next." \
            "curl -sk $(shell_quote "${DEFAULT_LOCAL_BUSINESS_URL}/debug/auth")"
    fi

    if [ "$(probe_get PUBLIC_API_AUTH tunnel_html)" = 'true' ]; then
        report \
            'Authenticated public BusinessApp URL' \
            'WARN' \
            "Even with a bearer token, ${PUBLIC_BUSINESS_URL}/api/backoffice/me returned the Codespaces tunnel/auth page. The public forwarding layer is still intercepting the request." \
            'gh codespace ports --codespace "$CODESPACE_NAME"' \
            "curl -i $(shell_quote "${PUBLIC_BUSINESS_URL}/api/backoffice/me")"
    elif [ "$(probe_get PUBLIC_API_AUTH status)" = '200' ]; then
        report \
            'Authenticated public BusinessApp URL' \
            'PASS' \
            "${PUBLIC_BUSINESS_URL}/api/backoffice/me also accepted the token ($(status_line PUBLIC_API_AUTH)). Browser-facing auth path looks healthy too."
    elif [ "$(probe_get PUBLIC_API_AUTH status)" = '401' ]; then
        report \
            'Authenticated public BusinessApp URL' \
            'WARN' \
            "Public BusinessApp URL is reachable but rejected the token ($(status_line PUBLIC_API_AUTH)). That aligns with an auth/token issue rather than a forwarding outage." \
            'bash scripts/codespaces/refresh.sh' \
            "curl -sk $(shell_quote "${DEFAULT_LOCAL_BUSINESS_URL}/debug/auth")"
    elif [ "$(probe_get PUBLIC_API_AUTH network_error)" = 'true' ]; then
        report \
            'Authenticated public BusinessApp URL' \
            'FAIL' \
            "Authenticated call to ${PUBLIC_BUSINESS_URL}/api/backoffice/me failed before it reached the app ($(status_line PUBLIC_API_AUTH)). Treat this as forwarding or stack readiness trouble first." \
            'gh codespace ports --codespace "$CODESPACE_NAME"' \
            'bash scripts/codespaces/health-check.sh'
    else
        report \
            'Authenticated public BusinessApp URL' \
            'WARN' \
            "Authenticated public call returned $(status_line PUBLIC_API_AUTH). Inspect the raw response body and forwarding state next." \
            "curl -i $(shell_quote "${PUBLIC_BUSINESS_URL}/api/backoffice/me")"
    fi
fi

print_section 'Keycloak backchannel health'
if [ "$(probe_get PUBLIC_KEYCLOAK_DISCOVERY status)" = '200' ]; then
    PUBLIC_ISSUER=''
    if [ "$(probe_get PUBLIC_KEYCLOAK_DISCOVERY content_type)" = 'application/json' ]; then
        PUBLIC_ISSUER="$(json_get_string "$(probe_get PUBLIC_KEYCLOAK_DISCOVERY body)" 'issuer')"
    fi
    report \
        'Public Keycloak discovery' \
        'PASS' \
        "${PUBLIC_KEYCLOAK_URL}/realms/prism-dev/.well-known/openid-configuration responded ($(status_line PUBLIC_KEYCLOAK_DISCOVERY)). Issuer: ${PUBLIC_ISSUER:-'(missing from payload)'}."
else
    report \
        'Public Keycloak discovery' \
        'FAIL' \
        "Public Keycloak discovery failed at ${PUBLIC_KEYCLOAK_URL}/realms/prism-dev/.well-known/openid-configuration ($(status_line PUBLIC_KEYCLOAK_DISCOVERY)). Fix stack readiness before chasing token bugs." \
        'bash scripts/codespaces/health-check.sh' \
        'bash scripts/codespaces/refresh.sh'
fi

if [ -n "$RUNTIME_BACKCHANNEL_URL" ] && [ "$RUNTIME_BACKCHANNEL_URL" != '(not set)' ]; then
    RUNTIME_MESSAGE="BusinessApp reports KEYCLOAK_BACKCHANNEL_URL=${RUNTIME_BACKCHANNEL_URL}."
    if [ -n "$RUNTIME_BACKCHANNEL_PROBE" ]; then
        RUNTIME_MESSAGE="${RUNTIME_MESSAGE} Probe: ${RUNTIME_BACKCHANNEL_PROBE}."
    fi

    STALE_REASONS=()
    if [ "$EXPECTS_KEYCLOAK_DYNAMIC" = 'true' ] && [ "$(probe_get RUNTIME_BACKCHANNEL_DISCOVERY status)" != '200' ]; then
        STALE_REASONS+=('repo expects a dynamic Keycloak backchannel, but the running BusinessApp cannot fetch discovery over that backchannel')
    fi
    if [ "$EXPECTS_KEYCLOAK_DYNAMIC" = 'true' ] && [ -n "$RUNTIME_AUTHORITY" ] && [ "$PUBLIC_KEYCLOAK_SOURCE" != 'local' ] && [[ "$RUNTIME_AUTHORITY" != *"${PUBLIC_KEYCLOAK_URL}"* ]]; then
        STALE_REASONS+=('running BusinessApp still trusts a different public OIDC authority than the current Codespaces forwarded URL')
    fi
    if [ "$(probe_get RUNTIME_BACKCHANNEL_CERTS status)" != '200' ]; then
        STALE_REASONS+=('the backchannel JWKS endpoint is not returning 200')
    fi

    if [ "${#STALE_REASONS[@]}" -gt 0 ]; then
        report \
            'Runtime Keycloak backchannel' \
            'WARN' \
            "${RUNTIME_MESSAGE} This looks stale or broken because $(join_by '; ' "${STALE_REASONS[@]}")." \
            'bash scripts/codespaces/refresh.sh' \
            "curl -sk $(shell_quote "${DEFAULT_LOCAL_BUSINESS_URL}/debug/auth")" \
            'tail -f artifacts/startup-status/prism-apphost.log'
    else
        report \
            'Runtime Keycloak backchannel' \
            'PASS' \
            "${RUNTIME_MESSAGE} Discovery/JWKS checks line up with the current repo wiring, so a remaining 401 is more likely a stale token/session than a bad endpoint."
    fi
else
    if [ "$EXPECTS_KEYCLOAK_DYNAMIC" = 'true' ]; then
        report \
            'Runtime Keycloak backchannel' \
            'WARN' \
            'BusinessApp /debug/auth did not report a KEYCLOAK_BACKCHANNEL_URL even though the repo expects one in Codespaces. That usually means the running stack is stale or needs a restart.' \
            'bash scripts/codespaces/refresh.sh' \
            "curl -sk $(shell_quote "${DEFAULT_LOCAL_BUSINESS_URL}/debug/auth")"
    else
        report \
            'Runtime Keycloak backchannel' \
            'SKIP' \
            'BusinessApp /debug/auth did not expose a KEYCLOAK_BACKCHANNEL_URL, and the repo does not currently insist on one.'
    fi
fi

if [ -n "$EXPECTED_AUTHORITY" ]; then
    printf '\nRepo expected fallback OIDC authority: %s\n' "$EXPECTED_AUTHORITY"
fi
if [ -n "$RUNTIME_AUTHORITY" ]; then
    printf 'Runtime BusinessApp OIDC authority:      %s\n' "$RUNTIME_AUTHORITY"
fi
if [ "$EXPECTS_BUSINESS_DYNAMIC" = 'true' ]; then
    printf 'Repo expectation: BUSINESSAPP_BACKCHANNEL_URL should be discovered dynamically in Codespaces.\n'
fi
if [ "$EXPECTS_KEYCLOAK_DYNAMIC" = 'true' ]; then
    printf 'Repo expectation: KEYCLOAK_BACKCHANNEL_URL should be discovered dynamically in Codespaces.\n'
fi
