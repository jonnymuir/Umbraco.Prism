#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
CONFIG_FILE="$REPO_ROOT/.prism_tunnel.conf"
TUNNEL_LOG_DIR="$REPO_ROOT/artifacts/logs/trycloudflared"

DEFAULT_LOCAL_PORT="44345"
DEFAULT_TENANT_ID="1"
DEFAULT_DB_PATH="src/UmbracoPrism.TestSite/umbraco/Data/Umbraco.sqlite.db"
CALLBACK_PATH="/signin-oidc"
TUNNEL_TIMEOUT_SECONDS="90"

LOCAL_PORT=""
ENTRA_APP_CLIENT_ID=""
TENANT_ID=""
TENANT_SELECTOR=""
TENANT_NAME=""
DB_PATH=""

CLOUDFLARED_PID=""
TUNNEL_LOG_FILE=""
ACTIVE_TUNNEL_LOG_DIR=""

error() {
  echo "Error: $1" >&2
}

require_command() {
  local cmd="$1"
  local install_hint="$2"
  if ! command -v "$cmd" >/dev/null 2>&1; then
    error "$cmd is required. $install_hint"
    exit 1
  fi
}

cleanup() {
  if [[ -n "$CLOUDFLARED_PID" ]] && kill -0 "$CLOUDFLARED_PID" >/dev/null 2>&1; then
    kill "$CLOUDFLARED_PID" >/dev/null 2>&1 || true
    wait "$CLOUDFLARED_PID" 2>/dev/null || true
  fi

  if [[ -n "$TUNNEL_LOG_FILE" && -f "$TUNNEL_LOG_FILE" ]]; then
    rm -f "$TUNNEL_LOG_FILE"
  fi
}

on_interrupt() {
  echo
  echo "Stopping trycloudflare tunnel and cleaning up..."
}

trap on_interrupt INT TERM
trap cleanup EXIT

trim_whitespace() {
  local value="$1"
  value="${value#${value%%[![:space:]]*}}"
  value="${value%${value##*[![:space:]]}}"
  printf '%s' "$value"
}

strip_wrapping_quotes() {
  local value="$1"
  if [[ "$value" =~ ^\".*\"$ ]]; then
    value="${value#\"}"
    value="${value%\"}"
  elif [[ "$value" =~ ^\'.*\'$ ]]; then
    value="${value#\'}"
    value="${value%\'}"
  fi
  printf '%s' "$value"
}

load_config() {
  if [[ ! -f "$CONFIG_FILE" ]]; then
    return
  fi

  local legacy_entra_app_object_id=""

  while IFS= read -r line || [[ -n "$line" ]]; do
    line="$(trim_whitespace "$line")"
    if [[ -z "$line" || "$line" == \#* ]]; then
      continue
    fi

    local key="${line%%=*}"
    local value="${line#*=}"
    key="$(trim_whitespace "$key")"
    value="$(trim_whitespace "$value")"
    value="$(strip_wrapping_quotes "$value")"

    case "$key" in
      LOCAL_PORT) LOCAL_PORT="$value" ;;
      ENTRA_APP_CLIENT_ID) ENTRA_APP_CLIENT_ID="$value" ;;
      ENTRA_APP_OBJECT_ID) legacy_entra_app_object_id="$value" ;;
      TENANT_ID) TENANT_ID="$value" ;;
      DB_PATH) DB_PATH="$value" ;;
    esac
  done < "$CONFIG_FILE"

  # Backward compatibility: migrate legacy key in-memory when new key is absent.
  if [[ -z "$ENTRA_APP_CLIENT_ID" && -n "$legacy_entra_app_object_id" ]]; then
    ENTRA_APP_CLIENT_ID="$legacy_entra_app_object_id"
  fi
}

prompt_with_default() {
  local label="$1"
  local current_value="$2"
  local fallback="$3"
  local prompt_default="$fallback"
  local input=""

  if [[ -n "$current_value" ]]; then
    prompt_default="$current_value"
  fi

  read -r -p "$label [$prompt_default]: " input
  if [[ -z "$input" ]]; then
    input="$prompt_default"
  fi

  printf '%s' "$input"
}

save_config() {
  umask 177
  local temp_file
  temp_file="$(mktemp "$REPO_ROOT/.prism_tunnel.conf.tmp.XXXXXX")"

  {
    echo "LOCAL_PORT=$LOCAL_PORT"
    echo "ENTRA_APP_CLIENT_ID=$ENTRA_APP_CLIENT_ID"
    echo "TENANT_ID=$TENANT_ID"
    echo "DB_PATH=$DB_PATH"
  } > "$temp_file"

  mv "$temp_file" "$CONFIG_FILE"
  chmod 600 "$CONFIG_FILE"
}

resolve_db_path() {
  if [[ "$DB_PATH" = /* ]]; then
    printf '%s' "$DB_PATH"
  else
    printf '%s' "$REPO_ROOT/$DB_PATH"
  fi
}

resolve_tunnel_log_dir() {
  local preferred_dir="$TUNNEL_LOG_DIR"
  if mkdir -p "$preferred_dir" >/dev/null 2>&1 && [[ -w "$preferred_dir" ]]; then
    printf '%s' "$preferred_dir"
    return
  fi

  local fallback_dir="${TMPDIR:-/tmp}/prism-trycloudflared-logs"
  if mkdir -p "$fallback_dir" >/dev/null 2>&1 && [[ -w "$fallback_dir" ]]; then
    printf '%s' "$fallback_dir"
    return
  fi

  error "Unable to create a writable tunnel log directory. Tried '$preferred_dir' and '$fallback_dir'."
  error "Check directory permissions and retry."
  exit 1
}

create_tunnel_log_file() {
  local preferred_dir="$TUNNEL_LOG_DIR"
  local fallback_dir="${TMPDIR:-/tmp}/prism-trycloudflared-logs"

  if mkdir -p "$preferred_dir" >/dev/null 2>&1; then
    if TUNNEL_LOG_FILE="$(mktemp "$preferred_dir/.trycloudflared.log.XXXXXX" 2>/dev/null)"; then
      ACTIVE_TUNNEL_LOG_DIR="$preferred_dir"
      return
    fi
  fi

  if mkdir -p "$fallback_dir" >/dev/null 2>&1; then
    if TUNNEL_LOG_FILE="$(mktemp "$fallback_dir/.trycloudflared.log.XXXXXX" 2>/dev/null)"; then
      ACTIVE_TUNNEL_LOG_DIR="$fallback_dir"
      return
    fi
  fi

  error "Unable to create temporary tunnel log file. Tried '$preferred_dir' and '$fallback_dir'."
  error "Check directory permissions and retry."
  exit 1
}

validate_inputs() {
  if [[ ! "$LOCAL_PORT" =~ ^[0-9]+$ ]]; then
    error "LOCAL_PORT must be numeric."
    exit 1
  fi

  if (( LOCAL_PORT < 1 || LOCAL_PORT > 65535 )); then
    error "LOCAL_PORT must be between 1 and 65535."
    exit 1
  fi

  if [[ -z "$TENANT_SELECTOR" ]]; then
    error "Tenant selector is required (tenant name or numeric id)."
    exit 1
  fi

  if [[ -z "$ENTRA_APP_CLIENT_ID" ]]; then
    error "ENTRA_APP_CLIENT_ID is required."
    exit 1
  fi

  if [[ ! "$ENTRA_APP_CLIENT_ID" =~ ^[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}$ ]]; then
    error "ENTRA_APP_CLIENT_ID must be a GUID (Application (Client) ID)."
    exit 1
  fi

  local resolved_db_path
  resolved_db_path="$(resolve_db_path)"
  if [[ ! -f "$resolved_db_path" ]]; then
    error "DB_PATH does not exist: $resolved_db_path"
    exit 1
  fi
}

sql_escape_literal() {
  local value="$1"
  printf '%s' "$value" | sed "s/'/''/g"
}

resolve_tenant_selector() {
  local database_file="$1"
  local selector="$2"
  local resolved_row=""
  local escaped_selector=""
  local match_count=""
  local matching_ids=""

  if [[ "$selector" =~ ^[0-9]+$ ]]; then
    resolved_row="$(sqlite3 -separator $'\t' "$database_file" "SELECT id, COALESCE(NULLIF(TRIM(name), ''), '(unnamed)') FROM prismTenants WHERE id = ${selector};")"
    if [[ -z "$resolved_row" ]]; then
      error "No tenant found with id ${selector} in prismTenants."
      exit 1
    fi
  else
    escaped_selector="$(sql_escape_literal "$selector")"
    match_count="$(sqlite3 "$database_file" "SELECT COUNT(*) FROM prismTenants WHERE name = '${escaped_selector}';")"

    if [[ "$match_count" == "0" ]]; then
      error "No tenant found with name '${selector}' in prismTenants."
      exit 1
    fi

    if [[ "$match_count" != "1" ]]; then
      matching_ids="$(sqlite3 "$database_file" "SELECT group_concat(id, ', ') FROM (SELECT id FROM prismTenants WHERE name = '${escaped_selector}' ORDER BY id);")"
      error "Multiple tenants found for name '${selector}'. Use numeric id instead. Matching ids: ${matching_ids}"
      exit 1
    fi

    resolved_row="$(sqlite3 -separator $'\t' "$database_file" "SELECT id, COALESCE(NULLIF(TRIM(name), ''), '(unnamed)') FROM prismTenants WHERE name = '${escaped_selector}' LIMIT 1;")"
  fi

  IFS=$'\t' read -r TENANT_ID TENANT_NAME <<< "$resolved_row"
}

extract_tunnel_url() {
  local source_file="$1"
  grep -Eo 'https://[A-Za-z0-9.-]+\.trycloudflare\.com' "$source_file" | tail -n 1 || true
}

validate_hostname() {
  local hostname="$1"

  if [[ ! "$hostname" =~ ^[A-Za-z0-9.-]+$ ]]; then
    return 1
  fi

  if [[ "$hostname" != *.trycloudflare.com ]]; then
    return 1
  fi

  if [[ "$hostname" == .* || "$hostname" == *. || "$hostname" == -* || "$hostname" == *- || "$hostname" == *..* ]]; then
    return 1
  fi

  if [[ "$hostname" != *.* ]]; then
    return 1
  fi

  return 0
}

is_trycloudflare_callback_uri() {
  local uri="$1"
  [[ "$uri" =~ ^https://[A-Za-z0-9.-]+\.trycloudflare\.com${CALLBACK_PATH}/?$ ]]
}

update_entra_redirect_uri() {
  local redirect_uri="$1"
  local -a existing_uris=()
  local -a final_uris=()
  local line
  local update_required=false
  local current_seen=false
  local stale_removed_count=0
  local current_duplicate_removed_count=0

  while IFS= read -r line; do
    if [[ -n "$line" ]]; then
      existing_uris+=("$line")
    fi
  done < <(az ad app show --id "$ENTRA_APP_CLIENT_ID" --query "web.redirectUris[]" -o tsv)

  for line in "${existing_uris[@]}"; do
    if is_trycloudflare_callback_uri "$line" && [[ "$line" != "$redirect_uri" ]]; then
      stale_removed_count=$((stale_removed_count + 1))
      update_required=true
      continue
    fi

    if [[ "$line" == "$redirect_uri" ]]; then
      if [[ "$current_seen" == true ]]; then
        current_duplicate_removed_count=$((current_duplicate_removed_count + 1))
        update_required=true
        continue
      fi
      current_seen=true
    fi

    final_uris+=("$line")
  done

  if [[ "$current_seen" == false ]]; then
    final_uris+=("$redirect_uri")
    update_required=true
  fi

  if [[ "$update_required" == true ]]; then
    az ad app update --id "$ENTRA_APP_CLIENT_ID" --web-redirect-uris "${final_uris[@]}" >/dev/null
  fi

  echo "Entra redirect URI prune: removed ${stale_removed_count} stale trycloudflare callbacks (${CALLBACK_PATH})."
}

update_tenant_hostname() {
  local database_file="$1"
  local hostname="$2"
  local tenant_id="$3"
  local existing_count
  local changed

  existing_count="$(sqlite3 "$database_file" "SELECT COUNT(*) FROM prismTenants WHERE id = ${tenant_id};")"
  if [[ "$existing_count" != "1" ]]; then
    error "No tenant found with id $tenant_id in prismTenants."
    exit 1
  fi

  changed="$(sqlite3 "$database_file" "BEGIN; UPDATE prismTenants SET hostname = '${hostname}' WHERE id = ${tenant_id}; SELECT changes(); COMMIT;")"
  if [[ "$changed" != "1" ]]; then
    error "Tenant hostname update failed for id $tenant_id."
    exit 1
  fi
}

mask_identifier() {
  local identifier="$1"
  local length="${#identifier}"
  if (( length <= 8 )); then
    printf '%s' "(hidden)"
    return
  fi

  local prefix="${identifier:0:4}"
  local suffix="${identifier:length-4:4}"
  printf '%s' "${prefix}...${suffix}"
}

wait_for_tunnel_url() {
  local start_time
  local now
  local elapsed
  local url=""

  start_time="$(date +%s)"
  while true; do
    if ! kill -0 "$CLOUDFLARED_PID" >/dev/null 2>&1; then
      error "cloudflared exited early. Inspect the last 50 lines below:"
      tail -n 50 "$TUNNEL_LOG_FILE" >&2 || true
      exit 1
    fi

    url="$(extract_tunnel_url "$TUNNEL_LOG_FILE")"
    if [[ -n "$url" ]]; then
      printf '%s' "$url"
      return
    fi

    now="$(date +%s)"
    elapsed=$((now - start_time))
    if (( elapsed >= TUNNEL_TIMEOUT_SECONDS )); then
      error "Timed out waiting ${TUNNEL_TIMEOUT_SECONDS}s for trycloudflare URL."
      error "Check local site availability on https://localhost:${LOCAL_PORT} and retry."
      error "cloudflared log excerpt:"
      tail -n 50 "$TUNNEL_LOG_FILE" >&2 || true
      exit 1
    fi

    sleep 1
  done
}

require_command cloudflared "Install with: brew install cloudflared"
require_command az "Install Azure CLI from https://learn.microsoft.com/cli/azure/install-azure-cli"
require_command sqlite3 "Install with: brew install sqlite"
require_command grep "grep is required and should be present on macOS by default."
require_command sed "sed is required and should be present on macOS by default."

load_config

LOCAL_PORT="$(prompt_with_default "Local HTTPS port" "$LOCAL_PORT" "$DEFAULT_LOCAL_PORT")"
ENTRA_APP_CLIENT_ID="$(prompt_with_default "Entra Application (Client) ID" "$ENTRA_APP_CLIENT_ID" "")"
TENANT_SELECTOR="$(prompt_with_default "Prism tenant selector (tenant name or numeric id; numeric id is internal DB id)" "$TENANT_ID" "$DEFAULT_TENANT_ID")"
DB_PATH="$(prompt_with_default "SQLite DB path (relative to repo or absolute)" "$DB_PATH" "$DEFAULT_DB_PATH")"

validate_inputs
DB_ABSOLUTE_PATH="$(resolve_db_path)"
resolve_tenant_selector "$DB_ABSOLUTE_PATH" "$TENANT_SELECTOR"
save_config
LOCAL_URL="https://localhost:${LOCAL_PORT}"

ACTIVE_TUNNEL_LOG_DIR="$(resolve_tunnel_log_dir)"
create_tunnel_log_file

echo "Starting Cloudflare quick tunnel to $LOCAL_URL"
echo "Waiting for a trycloudflare URL (timeout: ${TUNNEL_TIMEOUT_SECONDS}s)..."

cloudflared tunnel --url "$LOCAL_URL" > "$TUNNEL_LOG_FILE" 2>&1 &
CLOUDFLARED_PID="$!"

TUNNEL_URL="$(wait_for_tunnel_url)"
HOSTNAME="$(printf '%s' "$TUNNEL_URL" | sed -E 's#^https://([^/]+).*#\1#')"

if ! validate_hostname "$HOSTNAME"; then
  error "Derived hostname is invalid or not a trycloudflare domain: $HOSTNAME"
  exit 1
fi

REDIRECT_URI="${TUNNEL_URL}${CALLBACK_PATH}"

echo "Updating Entra redirect URI..."
update_entra_redirect_uri "$REDIRECT_URI"

echo "Updating Prism tenant hostname in SQLite..."
update_tenant_hostname "$DB_ABSOLUTE_PATH" "$HOSTNAME" "$TENANT_ID"

echo
echo "================ Local Dev Tunnel Ready ================"
echo "Tunnel URL:                $TUNNEL_URL"
echo "Hostname:                  $HOSTNAME"
echo "Redirect URI:              $REDIRECT_URI"
echo "Tenant selector provided:  $TENANT_SELECTOR"
echo "Tenant id updated:         $TENANT_ID"
echo "Tenant name resolved:      $TENANT_NAME"
echo "SQLite DB:                 $DB_ABSOLUTE_PATH"
echo "Entra app client id:       $(mask_identifier "$ENTRA_APP_CLIENT_ID")"
echo "Config file:               $CONFIG_FILE"
echo "Tunnel log directory:      $ACTIVE_TUNNEL_LOG_DIR"
echo "cloudflared PID:           $CLOUDFLARED_PID"
echo "========================================================"
echo
echo "Security note: this script is for local development only and modifies Entra redirect URIs plus local tenant hostname data."
echo "Tunnel is running. Press Ctrl+C to stop and clean up."

wait "$CLOUDFLARED_PID"
