# Mobile App CI/CD Pipeline (TestFlight & Play Store)

Automates the equivalent of the Umbraco backoffice's "Produce Mobile" action end-to-end: generate
the Capacitor starter bundle, bootstrap the native iOS/Android project, sign it, and upload it to
TestFlight (and, once added, the Play Store's internal testing track) — fully from GitHub Actions,
with no human running a script by hand.

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
- [ ] Generate an **App Store Connect API Key**: Users and Access → Integrations → App Store
      Connect API → generate a key with the **App Manager** role (needed both to let `xcodebuild`
      resolve signing and to upload to TestFlight). Note its **Key ID** and **Issuer ID**, and
      download the `.p8` file once — Apple only lets you download it once, so store it somewhere
      safe until it's in GitHub Secrets.
- [ ] Confirm the Apple Developer Program membership is a paid one covering Distribution.

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

## Android (Play Store) — planned, not yet built

The same shape, as a second workflow (`.github/workflows/deploy-play-internal.yml`,
`ubuntu-latest`, no macOS needed): the same bundle generation step (the bundle is
platform-agnostic — it ships both `bootstrap-ios.sh` and `bootstrap-android.sh`), then
`npm run bootstrap:android`, then `./gradlew bundleRelease` to produce a signed `.aab`, uploaded to
the Play Console's internal testing track via the Google Play Developer Publishing API.

The one place Android genuinely can't mirror iOS: there's no "API key issues a fresh certificate
every run" equivalent for Android's own app-signing key, so it needs a **persistent upload
keystore** generated once, locally, via `keytool -genkeypair` — regenerating it would break every
future update. Enrolling in **Play App Signing** (Play Console) lets Google hold the final signing
key and re-sign what you upload, so losing the upload keystore later is recoverable (Google can
issue a replacement) rather than fatal, which a bare self-managed keystore setup would not allow.

Expected secrets, once built:

| Secret | Value |
|---|---|
| `ANDROID_UPLOAD_KEYSTORE` | Base64-encoded contents of the `.jks` upload keystore |
| `ANDROID_KEYSTORE_PASSWORD` | Keystore password |
| `ANDROID_KEY_ALIAS` | Key alias inside the keystore |
| `ANDROID_KEY_PASSWORD` | Key password |
| `GOOGLE_PLAY_SERVICE_ACCOUNT_JSON` | Service account JSON key, granted access under Play Console → Setup → API access |
