## Publishing to the App Store and Google Play

- **Submit the app from the organisation whose content it shows.** Apple's App Review Guideline 4.2.6 rejects apps produced from a template or app-generation service unless the content provider submits them, so each tenant publishes under its own Apple Developer and Play Console accounts.
- **Privacy manifest.** `resources/PrivacyInfo.xcprivacy` is copied into the iOS project by `bootstrap-ios.sh`. It declares the data the enabled features collect (name and email from sign-in, plus the device token for push and photos and location for device capture). If your site collects anything else, add it there and in App Store Connect's privacy details.
- **Account deletion.** If members can create an account in the app, App Store review also expects a way to delete it from inside the app.

- **App sign-in.** The app signs members in through the phone's sign-in sheet, and the site tells Apple and Google which app may receive the result. In the tenant's **Produce Mobile** settings, under **App Sign-In**, enter the Apple Team ID and, for Google Play, the app signing key's SHA-256 fingerprint from Play Console (App signing). The iOS App ID needs the Associated Domains capability, which Xcode adds when it signs with automatic signing. Until these are set, the app signs in inside the app.
