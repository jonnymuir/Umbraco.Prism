## Publishing to the App Store and Google Play

- **Submit the app from the organisation whose content it shows.** Apple's App Review Guideline 4.2.6 rejects apps produced from a template or app-generation service unless the content provider submits them, so each tenant publishes under its own Apple Developer and Play Console accounts.
- **Privacy manifest.** `resources/PrivacyInfo.xcprivacy` is copied into the iOS project by `bootstrap-ios.sh`. It declares the data the enabled features collect (name and email from sign-in, plus the device token for push and photos and location for device capture). If your site collects anything else, add it there and in App Store Connect's privacy details.
- **Account deletion.** If members can create an account in the app, App Store review also expects a way to delete it from inside the app.

