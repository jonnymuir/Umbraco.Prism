# Mobile App CI/CD Pipeline (TestFlight & Play Store)

Automates the equivalent of the Umbraco backoffice's "Produce Mobile" action end-to-end: generate
the Capacitor starter bundle, bootstrap the native iOS/Android project, sign it, and upload it to
TestFlight and the Play Store's internal testing track — fully from GitHub Actions, with no human
running a script by hand.

> **Prerequisite this pipeline does NOT set up for you:** the app is a thin Capacitor shell that
> points at a live, reachable HTTPS URL (`server.url` in `capacitor.config.ts`) — it never bundles
> the web UI itself. You need a real hosted instance of the reference app before a TestFlight
> build will show anything but its own "can't connect" screen. That's a separate piece of work
> (this project's own setup uses a Fasthosts Linux VM behind Cloudflare); the pipeline only takes
> the hostname as a config input (`PRISM_REFERENCE_APP_HOSTNAME` below), it never provisions one.

## How it works

`.github/workflows/deploy-testflight.yml`, triggered manually (`workflow_dispatch`) on
`macos-latest`:

1. Checks out the repo, sets up .NET 10 and Node 20+.
2. Runs `src/UmbracoPrism.MobileBundleCli` — the headless equivalent of "Produce Mobile" — to
   generate the bundle `.zip` for the configured tenant hostname.
3. Unzips it, `npm install`s the bundle's own generated `package.json`, then runs its
   `npm run bootstrap:ios` script (`npx cap add ios` + `npx cap sync ios` + the biometric
   `Info.plist` patch, if enabled) — the exact same script a developer would run by hand.
4. Signs and archives the app via `xcodebuild -allowProvisioningUpdates`, using an App Store
   Connect API key rather than a manually-exported `.p12` certificate or provisioning profile —
   Xcode resolves/creates what it needs itself, scoped to the bundle ID.
5. Exports a `.ipa` and uploads it to TestFlight via `xcrun altool`.
6. Uploads the `.ipa` and dSYMs as a workflow artifact regardless of whether the TestFlight
   upload step is reached, so a build is inspectable even if upload fails.

No `.p12` certificate or `.mobileprovision` file is ever generated or stored — that's the whole
point of the API-key/automatic-signing approach.

## One-time Apple Developer Portal / App Store Connect setup

- [ ] Register the app's Bundle ID (e.g. `com.jonnymuir.prismreference`) under **Certificates,
      Identifiers & Profiles**.
- [ ] Create the app record in **App Store Connect** with that same Bundle ID.
- [ ] Generate an **App Store Connect API Key** under the **Team Keys** tab (not Individual Keys
      — Team Keys are the account-level, service-account-style keys meant for CI/automation;
      Individual Keys are tied to your own personal account access): Users and Access →
      Integrations → App Store Connect API → generate a key with the **Admin** role.
      **Admin, not App Manager** — found live: App Manager can create/manage a Development
      certificate (enough for `xcodebuild archive` to succeed) but cannot create or query an
      **iOS Distribution** certificate or provisioning profile via cloud-managed signing, so the
      later `-exportArchive` step fails with `Cloud signing permission error` / `No signing
      certificate "iOS Distribution" found` even though archiving worked. Note its **Key ID** and
      **Issuer ID**, and download the `.p8` file once — Apple only lets you download it once, so
      store it somewhere safe until it's in GitHub Secrets.
- [ ] Confirm the Apple Developer Program membership is a paid one covering Distribution.
- [ ] Note your **Team ID** (Apple Developer portal → **Account** → **Membership details**) —
      needed below. This isn't a secret (it ends up embedded in every `.ipa`'s own provisioning
      profile anyway), but `xcodebuild -allowProvisioningUpdates` still needs to be told which
      team to provision for explicitly — a freshly-generated Capacitor Xcode project has no team
      set at all, and archiving fails outright ("Signing for 'App' requires a development team")
      without it. Found live on this pipeline's first real dispatch run.

## GitHub repository configuration

**Secrets** (Settings → Secrets and variables → Actions → Secrets):

| Secret | Value |
|---|---|
| `APP_STORE_CONNECT_API_KEY_P8` | The full contents of the downloaded `.p8` file |
| `APP_STORE_CONNECT_KEY_ID` | The API key's Key ID |
| `APP_STORE_CONNECT_ISSUER_ID` | The Issuer ID shown alongside the key |

**Variables** (same page, Variables tab):

| Variable | Purpose | Required? |
|---|---|---|
| `PRISM_REFERENCE_APP_HOSTNAME` | The reference app's live hostname | Yes — the workflow fails fast with a clear error if unset |
| `PRISM_REFERENCE_APP_NAME` | Display name shown on the device | No — defaults to "Prism Reference" |
| `PRISM_REFERENCE_APP_ID` | Reverse-DNS bundle identifier | No — defaults to `com.jonnymuir.prismreference` |
| `PRISM_REFERENCE_APP_VERSION` | App version string | No — defaults to `1.0.0` |
| `PRISM_APPLE_TEAM_ID` | Your Apple Developer Team ID | Yes — the workflow fails fast with a clear error if unset |

Until the reference app has real hosting, set `PRISM_REFERENCE_APP_HOSTNAME` to **`example.com`**
(the IANA-reserved placeholder domain — no trademark/ToS concern, unlike pointing it at a real
site you don't control) to validate that the pipeline itself builds, signs, and uploads correctly.
The app will just show its themed "can't connect" screen in TestFlight until the real host
resolves — that's expected and fine for a first pipeline test. No workflow change is needed later:
flip the variable to the real value once hosting exists.

## Running it

Actions tab → **Deploy to TestFlight** → **Run workflow**. First run: inspect the uploaded
`.ipa` workflow artifact even before trusting the TestFlight upload step, then confirm the build
lands in App Store Connect → TestFlight and installs on a real device via the TestFlight app.

**Every run uploads a genuinely new build automatically** — no manual version bump needed. A
fresh `cap add ios` (run from scratch every CI run, no state persists between them) always
produces a fixed `MARKETING_VERSION=1.0`/`CURRENT_PROJECT_VERSION=1`; App Store Connect rejects a
second upload with an identical pair for the same bundle ID. The workflow overrides
`CURRENT_PROJECT_VERSION` at archive time to the GitHub Actions run number (`github.run_number`)
— simple, always-unique, no extra state to manage — while leaving `MARKETING_VERSION` at
Capacitor's default, since Apple only requires that to be unique per marketing-version *release*,
not per build.

**Known separate issue, not yet fixed**: `UmbracoPrism.MobileBundleCli`'s `--version` flag
(and `MobileBundleService`'s `request.Version`) doesn't actually do anything — it's accepted but
never applied to the generated `capacitor.config.ts` or the iOS project once scaffolded. Doesn't
block this pipeline (which sidesteps it entirely via the build-number override above), but worth
fixing so the flag isn't silently misleading for other consumers of the CLI.

## Android (Play Store — internal testing track)

The same shape as the iOS pipeline, as a second workflow
(`.github/workflows/deploy-play-internal.yml`, `ubuntu-latest` — no macOS needed for Android):

1. Same bundle generation step (the bundle is platform-agnostic — it ships both
   `bootstrap-ios.sh` and `bootstrap-android.sh`), then `npm run bootstrap:android`.
2. Builds a debug APK (`./gradlew assembleDebug`, no signing needed) and runs it through an
   emulator smoke test — the Android equivalent of the iOS pipeline's simulator smoke test, using
   [`ReactiveCircus/android-emulator-runner`](https://github.com/ReactiveCircus/android-emulator-runner)
   for hardware-accelerated (KVM) emulation on the Linux runner, with the same
   screenshot-pixel-variance / two-consecutive-passes check as the iOS one.
3. If a signing keystore secret is present, appends a `signingConfigs`/`versionCode` block to
   `android/app/build.gradle` as a **second, separate `android { }` block** (not a regex edit
   into Capacitor's own generated one — Gradle/Groovy layers each block's config onto the same
   extension object top-to-bottom, so this is structurally safe regardless of the exact template
   content, unlike a brace-matching regex would be — see PR #279 for why that distinction
   mattered on the iOS side), then runs `./gradlew bundleRelease` to produce a signed `.aab`.
4. If a Play Console service-account secret is also present, uploads that `.aab` to the
   **internal testing** track via the Google Play Developer Publishing API
   ([`r0adkll/upload-google-play`](https://github.com/r0adkll/upload-google-play)) — the direct
   equivalent of TestFlight's internal testing: immediate distribution to up to 100 testers, no
   Google review. (The 12-testers/14-consecutive-days requirement some newer personal Play
   Console accounts face only applies when later *promoting* from closed testing to production —
   not to internal testing itself.)
5. Uploads the `.aab`/debug `.apk` as workflow artifacts regardless of whether signing/upload
   ran, same "inspectable even if a later step is skipped or fails" reasoning as the iOS pipeline.

Both the signing and Play-upload steps are soft-gated on their respective secrets being present
(`SIGNING_ENABLED`/`PLAY_UPLOAD_ENABLED` in the workflow) — a repo that hasn't done the Play
Console/keystore setup yet still gets a working, smoke-tested debug build rather than every
dispatch failing outright, same pattern as push notifications' own soft gate on both pipelines.

### One-time Google Play Console / Firebase setup

- [ ] Register the app in **Play Console** with the same package name as `PRISM_REFERENCE_APP_ID`
      (e.g. `com.jonnymuir.prismreference`) — must match exactly, same as the iOS Bundle ID.
- [ ] Enroll in **Play App Signing** (Play Console → Setup → App signing). Unlike iOS's App Store
      Connect API key (which mints a fresh signing identity every run), Android app-signing needs
      a **persistent upload keystore** generated once, locally:
      ```bash
      keytool -genkeypair -v -keystore release-upload.keystore -alias prism-upload \
        -keyalg RSA -keysize 2048 -validity 10000
      ```
      Regenerating this keystore later would break every future update — back it up somewhere
      safe before it ever goes into GitHub Secrets. Play App Signing means Google holds the
      *final* signing key and re-signs what you upload with your upload keystore, so losing the
      upload keystore later is recoverable (Google can issue a replacement) rather than fatal,
      which a bare self-managed keystore setup would not allow.
- [ ] Create a **service account** (Google Cloud Console, in the same project Play Console is
      linked to), grant it access under Play Console → Setup → API access, and generate its JSON
      key.
- [ ] Firebase: register the **Android app** in the same Firebase project the iOS setup already
      uses (see `docs/PUSH_SETUP.md`) — same project, just add the Android app registration
      (package name must match again) to get `google-services.json`.

### GitHub repository configuration

**Secrets** (Settings → Secrets and variables → Actions → Secrets):

| Secret | Value |
|---|---|
| `GOOGLE_SERVICES_JSON` | Full contents of `google-services.json` (plain text, no base64 — same reasoning as iOS's `GOOGLE_SERVICE_INFO_PLIST`) |
| `ANDROID_UPLOAD_KEYSTORE` | Base64-encoded contents of the `.keystore`/`.jks` upload keystore (binary — base64 *is* genuinely needed here, unlike the plain-text config files above) |
| `ANDROID_KEYSTORE_PASSWORD` | Keystore password |
| `ANDROID_KEY_ALIAS` | Key alias inside the keystore (`prism-upload` if you used the `keytool` command above) |
| `ANDROID_KEY_PASSWORD` | Key password |
| `GOOGLE_PLAY_SERVICE_ACCOUNT_JSON` | Service account JSON key, granted access under Play Console → Setup → API access |

Reuses the same `PRISM_REFERENCE_APP_HOSTNAME`/`PRISM_REFERENCE_APP_NAME`/`PRISM_REFERENCE_APP_ID`/
`PRISM_REFERENCE_APP_VERSION` repo variables the iOS pipeline already reads — no separate
Android-specific variables needed.

### Running it

Actions tab → **Deploy to Play Store (Internal Testing)** → **Run workflow**. Every run's release
uses `github.run_number` as the Android `versionCode` — same "must be a strictly increasing
integer, and a fresh `cap add android` has no persisted state between CI runs to increment from
otherwise" reasoning as the iOS pipeline's own `CURRENT_PROJECT_VERSION` override.

First run without any of the signing/upload secrets set: inspect the uploaded debug `.apk`
artifact and the emulator smoke test screenshot to confirm the bundle itself builds and renders
correctly, before doing the Play Console/keystore setup above.
