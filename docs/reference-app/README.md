# Prism Reference App

The **Prism Reference App** is the mobile shell generated from `UmbracoPrism.TestSite` via the
package's mobile-generation pipeline (`docs/guides/mobile-app-ci-cd-pipeline.md`). It's a
Capacitor wrapper around the same demo Umbraco site used throughout this repo's docs and
walkthroughs, distributed through Apple TestFlight and the Google Play internal testing track
so the package's multi-tenant sign-in, branding, and native mobile features can be exercised on
a real device.

It is **not a commercial product**. It demonstrates what a host application built on
[Umbraco Prism](https://github.com/jonnymuir/Umbraco.Prism) can look like, and is currently only
available to a small list of invited internal testers.

- **Sign-in**: demo credentials via a Keycloak realm run for this repo (`demo@prism.local` /
  `password` — see the main [README](../../README.md) for the full credential table). There is
  no real account system or real user base behind it.
- **Data**: see the [Privacy Policy](privacy-policy.md) for exactly what the app touches.
- **Source**: the reference app has no code of its own beyond `UmbracoPrism.TestSite` plus the
  Capacitor bootstrap scripts every Prism-based mobile app gets — see
  `docs/guides/mobile-app-ci-cd-pipeline.md` for how it's built and shipped.

This page (and its privacy policy) exists mainly to satisfy app-store listing requirements for
the internal testing tracks; it isn't meant to be discovered outside that flow.
