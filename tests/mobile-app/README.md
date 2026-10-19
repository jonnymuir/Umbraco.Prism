# Generated app checks

Maestro flows and a timing harness for a generated app running on the iOS simulator against the
local Aspire stack (demo sign-in `demo@prism.local` / `password`).

Needs [Maestro](https://maestro.mobile.dev), `ffmpeg`, and `python3` with `numpy`.

Generate and install an app pointed at the local stack:

```bash
dotnet run --project src/UmbracoPrism.MobileBundleCli -- \
  --start-url https://localhost:44345/ --app-name "Prism Local" --app-id com.prism.local \
  --biometric-auth true --output prism-local.zip
unzip prism-local.zip -d prism-local && cd prism-local
npm install && npm run bootstrap:ios
```

Run a flow:

```bash
maestro test -e APP_ID=com.prism.local tests/mobile-app/flows/sign-out-refreshes-other-tabs.yaml
```

Time tab switches (twelve switches, recorded and measured frame by frame):

```bash
tests/mobile-app/measure-tab-switches.sh com.prism.local
```

| Flow | Checks |
|---|---|
| `sign-in-and-visit-tabs.yaml` | Signs in from the Dashboard tab and opens every tab once |
| `switch-tabs.yaml` | Taps a checkbox, then the tabs in turn; used by the timing harness |
| `sign-out-refreshes-other-tabs.yaml` | Signing out in one tab leaves no other tab showing the member's page |

Maestro's own timings include waiting for the screen to settle after every tap, so they are not a
measure of the app. `measure-tab-switches.sh` records the screen instead: the checkbox tap aligns
the video with Maestro's log, and each tab switch is timed from the tap to the last frame in which
the page area changes.
