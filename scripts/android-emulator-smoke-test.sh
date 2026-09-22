#!/usr/bin/env bash
# Android emulator smoke test for deploy-play-internal.yml: install the freshly built debug
# APK, launch it, then poll screenshots until the content area shows real rendered content
# (not a blank/broken screen), same two-consecutive-passes approach as deploy-testflight.yml's
# own iOS simulator smoke test.
#
# This lives in its own file, invoked as a single command
# (`script: bash scripts/android-emulator-smoke-test.sh`), rather than inline in the workflow's
# `script:` block. ReactiveCircus/android-emulator-runner runs each LINE of that field as a
# separate `sh -c` call rather than the block as one script — found live when a `set -eu` fix
# still crashed with "Syntax error: end of file unexpected (expecting fi")": the `if`/`fi` pair
# had been split across two independent shell invocations with no shared state between them.
# Wrapping the whole thing in one real script file, run as a single command, sidesteps that
# entirely and lets it use full bash syntax freely.
set -euo pipefail

APK_PATH=$(find mobile-app/android -maxdepth 8 -iname "app-debug.apk" | head -n 1)
if [ -z "$APK_PATH" ]; then
  echo "::error::Could not find built app-debug.apk under mobile-app/android."
  exit 1
fi

BUNDLE_ID="${PRISM_APP_ID:-com.jonnymuir.prismreference}"
adb install "$APK_PATH"
adb shell monkey -p "$BUNDLE_ID" -c android.intent.category.LAUNCHER 1

sudo apt-get update -y && sudo apt-get install -y imagemagick

mkdir -p build

PASSED=false
CONSECUTIVE_PASSES=0
for attempt in $(seq 1 15); do
  adb exec-out screencap -p > build/emulator-smoke-test.png
  # Crop away the top ~8% (Android status bar, generally shorter than iOS's notch/Dynamic
  # Island area) so its own icon/text contrast can't mask a blank body.
  # `convert`/`identify`, not the unified `magick` binary — Ubuntu 24.04's `apt-get install
  # imagemagick` gives ImageMagick 6, which never shipped `magick` (that's IM7-only); found
  # live once the smoke test script finally ran far enough to reach this line at all.
  convert build/emulator-smoke-test.png -gravity South -crop 100%x92%+0+0 +repage build/emulator-smoke-test-content.png
  SPREAD=$(identify -format "%[fx:standard_deviation]" build/emulator-smoke-test-content.png)
  echo "Attempt $attempt/15 — content-area colour standard deviation: $SPREAD (consecutive passes: $CONSECUTIVE_PASSES)"
  if (( $(echo "$SPREAD >= 0.03" | bc -l) )); then
    CONSECUTIVE_PASSES=$((CONSECUTIVE_PASSES + 1))
    if [ "$CONSECUTIVE_PASSES" -ge 2 ]; then
      PASSED=true
      break
    fi
  else
    CONSECUTIVE_PASSES=0
  fi
  sleep 4
done

if [ "$PASSED" != "true" ]; then
  echo "::error::The app's content area still looks near-solid-colour (stddev=$SPREAD) after $((15 * 4))s, or never passed twice in a row — it likely rendered a blank/broken screen instead of real content. See the uploaded screenshot artifact."
  exit 1
fi

echo "✓ Emulator smoke test passed — app launched and rendered real content (2 consecutive passes)."
