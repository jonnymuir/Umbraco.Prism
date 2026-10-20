# Generated app checks

Maestro flows and a timing harness for a generated app running on the iOS simulator or an
Android emulator against the local Aspire stack (demo sign-in `demo@prism.local` / `password`).

Needs [Maestro](https://maestro.mobile.dev), `ffmpeg`, and `python3` with `numpy`.

Generate and install an app pointed at the local stack:

```bash
dotnet run --project src/UmbracoPrism.MobileBundleCli -- \
  --start-url https://localhost:44345/ --app-name "Prism Local" --app-id com.prism.local \
  --biometric-auth true --output prism-local.zip
unzip prism-local.zip -d prism-local && cd prism-local
npm install && npm run bootstrap:ios      # or bootstrap:android
```

The Android emulator reaches the local stack through `adb reverse`, and needs the development
certificate installed as a user CA, which debug builds of the generated app trust. On a
`google_apis` emulator image (one that allows `adb root`):

```bash
dotnet dev-certs https --export-path devcert.pem --format PEM --no-password
HASH=$(openssl x509 -inform PEM -subject_hash_old -in devcert.pem | head -1)
adb root && adb push devcert.pem /data/misc/user/0/cacerts-added/$HASH.0
adb shell "chown system:system /data/misc/user/0/cacerts-added/$HASH.0 && chmod 644 /data/misc/user/0/cacerts-added/$HASH.0"
adb reverse tcp:44345 tcp:44345 && adb reverse tcp:8443 tcp:8443
```

Build the Android project with JDK 21 (Android Studio's bundled one works:
`JAVA_HOME="/Applications/Android Studio.app/Contents/jbr/Contents/Home"`).

The flows sign in inside the app, so they run against a tenant without **App Sign-In** set up
(the local tenant's default). With it set up, the app signs in through the system sheet, whose
callback the simulator cannot receive and the emulator receives only when handed over with
`adb shell am start -a android.intent.action.VIEW -d "<callback URL>" com.prism.local`.

Run a flow (add `--device <id>` when more than one simulator or emulator is running):

```bash
maestro test -e APP_ID=com.prism.local tests/mobile-app/flows/sign-out-refreshes-other-tabs.yaml
```

Time tab switches on the iOS simulator (twelve switches, recorded and measured frame by frame):

```bash
tests/mobile-app/measure-tab-switches.sh com.prism.local
```

| Flow | Checks |
|---|---|
| `sign-in-and-visit-tabs.yaml` | Signs in from the Dashboard tab and opens every tab once |
| `switch-tabs.yaml` | Taps a checkbox, then the tabs in turn; used by the timing harness |
| `sign-out-refreshes-other-tabs.yaml` | Signing out in one tab leaves no other tab showing the member's page |
| `sign-in-from-home-switches-tabs.yaml` | A link or redirect to another tab's page opens in that tab, after sign-in and sign-out too |

Maestro's own timings include waiting for the screen to settle after every tap, so they are not a
measure of the app. `measure-tab-switches.sh` records the screen instead: the checkbox tap aligns
the video with Maestro's log, and each tab switch is timed from the tap to the last frame in which
the page area changes.
