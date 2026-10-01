#!/usr/bin/env bash
# Talk helper for docs/demos/service-design-meetup-talk.md.
#
#   scripts/demo.sh check         are both stacks up, on their fixed URLs, with a trusted certificate?
#   scripts/demo.sh mcp-connect   register the Wayfinder MCP server with Claude Code and log in
#
# Every URL here is fixed by a launch profile, so nothing depends on which port Aspire picks.
# The MCP leg uses plain http://localhost:5299 on purpose: the Claude CLI does not trust the
# .NET dev certificate (it ignores the macOS keychain and NODE_EXTRA_CA_CERTS), and the browser
# OAuth login works the same over loopback http. No TLS setting, nothing to forget.

set -u

MCP_NAME="wayfinder-umbraco"
MCP_URL="${DEMO_MCP_URL:-http://localhost:5299/wayfinder/service-blueprint-authoring/mcp}"
MCP_CLIENT_ID="umbraco-back-office-wayfinder-mcp"
MCP_CALLBACK_PORT=33418
SCRATCH_DIR="$HOME/demo-scratch"

ok()   { printf '  \033[32m✔\033[0m %s\n' "$1"; }
fail() { printf '  \033[31m✘\033[0m %s\n' "$1"; failures=$((failures + 1)); }

probe() {
  local label="$1" url="$2" hint="$3" code
  code=$(curl -s -o /dev/null -m 8 -w '%{http_code}' "$url" 2>/dev/null)
  if [[ "$code" =~ ^[234] ]]; then
    ok "$label  $url"
  elif [[ "$code" == "000" ]]; then
    fail "$label  $url  (not reachable or certificate not trusted: $hint)"
  else
    fail "$label  $url  (HTTP $code)"
  fi
}

cmd_check() {
  failures=0
  echo "Tools"
  command -v dotnet >/dev/null && ok "dotnet $(dotnet --version)" || fail "dotnet missing"
  command -v claude >/dev/null && ok "claude $(claude --version 2>/dev/null)" || fail "claude missing"
  docker info >/dev/null 2>&1 && ok "docker running" || fail "docker not running, start Docker Desktop"
  dotnet dev-certs https --check --trust >/dev/null 2>&1 \
    && ok "dev certificate trusted" || fail "dev certificate not trusted, run: dotnet dev-certs https --trust"

  echo "Stack 1: Wayfinder.Umbraco ReferenceApp"
  probe "front end + backoffice" "https://localhost:44399/umbraco" "start Stack 1"
  probe "Mailpit inbox         " "https://localhost:8025" "start Stack 1"
  probe "MCP OAuth metadata    " "http://localhost:5299/.well-known/oauth-authorization-server" "start Stack 1"

  echo "Stack 2: Umbraco.Prism"
  probe "Aspire dashboard      " "https://localhost:17214" "start Stack 2"
  probe "TestSite              " "https://localhost:44345" "start Stack 2"
  probe "Keycloak              " "https://localhost:8443/realms/prism-dev/.well-known/openid-configuration" "start Stack 2"
  probe "MockBusinessApp       " "https://localhost:7245" "start Stack 2"

  echo
  if [[ $failures -eq 0 ]]; then
    printf '\033[32mAll good. Ready for the talk.\033[0m\n'
  else
    printf '\033[31m%d problem(s) above.\033[0m\n' "$failures"
    return 1
  fi
}

cmd_mcp_connect() {
  if ! curl -s -o /dev/null -m 8 "http://localhost:5299/.well-known/oauth-authorization-server"; then
    echo "Stack 1 is not answering on http://localhost:5299. Start it first (Terminal window 1)." >&2
    return 1
  fi
  if lsof -iTCP:"$MCP_CALLBACK_PORT" -sTCP:LISTEN >/dev/null 2>&1; then
    echo "Port $MCP_CALLBACK_PORT is already in use, so the login callback cannot start." >&2
    echo "Find the culprit with: lsof -iTCP:$MCP_CALLBACK_PORT -sTCP:LISTEN" >&2
    return 1
  fi

  mkdir -p "$SCRATCH_DIR" && cd "$SCRATCH_DIR" || return 1
  claude mcp remove "$MCP_NAME" >/dev/null 2>&1
  claude mcp add --transport http "$MCP_NAME" "$MCP_URL" \
    --client-id "$MCP_CLIENT_ID" --callback-port "$MCP_CALLBACK_PORT" || return 1

  echo
  echo "A browser tab opens now. Sign in as admin@example.test / Wayfinder123!"
  claude mcp login "$MCP_NAME" || return 1

  echo
  claude mcp list
}

case "${1:-}" in
  check)        cmd_check ;;
  mcp-connect)  cmd_mcp_connect ;;
  *) sed -n '2,8p' "$0" | sed 's/^# \{0,1\}//'; exit 1 ;;
esac
