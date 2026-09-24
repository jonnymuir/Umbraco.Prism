# Privacy Policy — Prism Reference App

_Effective 24 September 2026_

This app is a **reference and internal-testing build** for the open-source
[Umbraco Prism](https://github.com/jonnymuir/Umbraco.Prism) package — it demonstrates
multi-tenant sign-in, branding, and mobile packaging, and is not a commercial product. It's
currently only available to a small list of invited internal testers. See the
[Reference App overview](README.md) for what it is.

## What this app is

Prism Reference is a Capacitor-wrapped shell around a demo Umbraco site used to test and
showcase the Umbraco Prism package's sign-in, branding, and mobile-packaging features. It is not
intended for general public use.

## Information we collect

- **Sign-in identity.** When you sign in, the app receives standard OpenID Connect claims (name,
  email, username) from the demo identity provider (Keycloak) used to run this test environment.
- **Push notification token.** If notifications are enabled on your device, a device token is
  registered with Firebase Cloud Messaging solely to deliver demo notifications from this app.
- **Biometric unlock.** If you enable biometric sign-in, your device's own biometric system
  (Face ID / fingerprint) handles this locally. No biometric data is ever collected, transmitted,
  or stored by the app or its servers.

## What we don't do

- No advertising, ad networks, or ad identifiers.
- No sale or sharing of your data with third parties.
- No analytics or tracking beyond basic, non-identifying server logs needed to operate the demo
  environment.

## Data retention

Accounts and content in the demo environment can be reset at any time without notice, as is
normal for a test/reference deployment. Nothing in this app should be treated as a permanent
record.

## Contact

Questions about this policy or the data this reference app handles can be raised as an issue on
the [project repository](https://github.com/jonnymuir/Umbraco.Prism/issues).
