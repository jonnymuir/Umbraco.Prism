# Delete Your Account — Prism Reference App

The **Umbraco Prism Reference App** doesn't manage accounts of its own — sign-in goes through an
external identity provider (a demo Keycloak realm, or a Microsoft Entra ID tenant for the
production reference app), and there's no separate account record kept inside the app beyond
what your session needs while you're signed in. See the
[Reference App overview](README.md) and [Privacy Policy](privacy-policy.md) for what the app
does hold onto (a push notification token, if you enabled notifications).

## How to request deletion

Open an issue on the [project repository](https://github.com/jonnymuir/Umbraco.Prism/issues)
stating:

1. The identity provider account you signed in with (e.g. your `@prism.local` demo username, or
   your Entra ID email).
2. That you'd like your push notification registration (if any) removed.

We'll confirm removal of any push token tied to that identity within a few days.

## What this doesn't cover

Deleting your sign-in identity itself (the account in Keycloak or Entra ID) isn't something this
app can do on your behalf — that's managed by whichever identity provider you signed in through,
not by the Prism Reference App. For the demo Keycloak realm this project runs, accounts are
periodically reset anyway as part of normal demo-environment upkeep.
