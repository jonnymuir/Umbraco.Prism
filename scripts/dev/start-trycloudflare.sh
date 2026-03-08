#!/usr/bin/env bash
set -euo pipefail

LOCAL_URL="${1:-https://localhost:44345}"
CALLBACK_PATH="${2:-/umbraco/oauth_complete}"

if ! command -v cloudflared >/dev/null 2>&1; then
  echo "cloudflared is not installed. Install with: brew install cloudflared" >&2
  exit 1
fi

echo "Starting Cloudflare quick tunnel to $LOCAL_URL"
echo "Press Ctrl+C to stop the tunnel."
echo

cloudflared tunnel --url "$LOCAL_URL" 2>&1 | while IFS= read -r line; do
  echo "$line"

  if [[ "${PUBLIC_URL:-}" == "" && "$line" =~ https://[a-zA-Z0-9._-]+\.trycloudflare\.com ]]; then
    PUBLIC_URL="${BASH_REMATCH[0]}"

    echo
    echo "================ Tunnel Ready ================"
    echo "Public URL:        $PUBLIC_URL"
    echo "Entra Redirect URI: ${PUBLIC_URL}${CALLBACK_PATH}"
    echo "Mobile Start URL:  $PUBLIC_URL"
    echo "============================================="
    echo
  fi
done
