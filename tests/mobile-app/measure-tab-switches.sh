#!/usr/bin/env bash
# Times twelve tab switches in a generated app running on a booted iOS simulator.
# Usage: tests/mobile-app/measure-tab-switches.sh <app id>
# Needs maestro, ffmpeg and python3 with numpy, and the local Aspire stack (demo sign-in).
set -euo pipefail
APP_ID=${1:?usage: measure-tab-switches.sh <app id>}
DIR="$(cd "$(dirname "$0")" && pwd)"
DEVICE=$(xcrun simctl list devices booted -j | python3 -c "import json,sys;print([d['udid'] for r in json.load(sys.stdin)['devices'].values() for d in r][0])")
OUT=$(mktemp -d)

maestro --device "$DEVICE" test -e APP_ID="$APP_ID" "$DIR/flows/sign-in-and-visit-tabs.yaml" > "$OUT/setup.log" 2>&1 \
  || { tail -20 "$OUT/setup.log"; exit 1; }

xcrun simctl io "$DEVICE" recordVideo --codec h264 --force "$OUT/switches.mp4" > /dev/null 2>&1 &
RECORDER=$!
sleep 2
maestro --device "$DEVICE" test -e APP_ID="$APP_ID" "$DIR/flows/switch-tabs.yaml" > "$OUT/switches.log" 2>&1 \
  || tail -20 "$OUT/switches.log"
MAESTRO_LOG="$HOME/.maestro/tests/$(ls -t "$HOME/.maestro/tests" | head -1)/maestro.log"
sleep 1
kill -INT $RECORDER
wait $RECORDER 2>/dev/null || true

python3 "$DIR/tab-switch-timing.py" "$OUT/switches.mp4" "$MAESTRO_LOG"
echo "Recording: $OUT/switches.mp4"
