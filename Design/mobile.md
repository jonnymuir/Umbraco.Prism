# Design Brief: Prism Mobile Scaffolding

## 1. Objective
To provide Umbraco users with a "Zero-Code" path to generating native mobile app shells (iOS/Android) that are dynamically branded and synced with their Prism tenant configuration.

## 2. Constraints & Criteria
* **No Manual Config:** The user should not have to edit `plist` or `xml` files.
* **Branding Fidelity:** The app must use the same CSS/Variables defined in the Prism Backoffice.
* **Store Compliant:** The shell must follow Apple/Google guidelines for "Hybrid" apps (requiring unique branding and functionality).
* **Environment:** Compatible with Umbraco v14+ (Management API / Bellissima UI).

## 3. Technical Requirements
* **Engine:** Capacitor.js (latest stable).
* **Backoffice:** Custom `umb-workspace-view` added to the Prism Tenant Workspace.
* **Server-Side:** .NET `System.IO.Compression` for dynamic bundle generation.
* **Identity:** Must pass the Prism stateless auth token to the WebView.

## 4. Solution Concept
### Phase A: The Backoffice Extension
- Create a new tab in the Prism Tenant UI called **"App Shell"**.
- Input fields for: App ID, App Name, Version, App Icon (Media Picker), and Splash (Media Picker).
- Button: `Generate & Download App Bundle`.

### Phase B: The Bundle Generator
- A C# Service (`IMobileBundleService`) that:
  - Fetches a pre-packaged Capacitor template from the filesystem.
  - Generates a new `capacitor.config.ts` string based on the Tenant data.
  - Uses the `ZipArchive` class to create an in-memory zip of the project.
  - Serves the file as an `ActionResult`.

### Phase C: The "Mobile Context" Middleware
- Prism's `PrismMiddleware` will check for the header `X-Prism-Platform: Mobile`.
- If present, Prism injects a `.prism-mobile` class into the `<body>` tag of the rendered page.
- This allows the developer to write: `.prism-mobile nav { display: none; }` to instantly "app-ify" their web layout.

## 5. Success Metric
A user should be able to go from "New Tenant" to "Running on Android Emulator" in under 5 minutes without opening a code editor.