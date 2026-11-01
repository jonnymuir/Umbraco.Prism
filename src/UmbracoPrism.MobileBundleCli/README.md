# UmbracoPrism.MobileBundle

A `dotnet tool` that generates the mobile app bundle for an
[Umbraco Prism](https://github.com/jonnymuir/Umbraco.Prism) site: the Capacitor iOS and Android
projects that the backoffice's **Produce Mobile** button builds, from command-line settings
instead. Use it to build and ship a Prism site's app from CI.

## Install

As a local tool, pinned in your repo:

```bash
dotnet new tool-manifest
dotnet tool install UmbracoPrism.MobileBundle
```

Use the same version as the `UmbracoPrism` package your site runs.

## Use

```bash
dotnet prism-mobile-bundle --hostname www.example.com --output mobile-bundle.zip \
  --app-name "Example" --app-id com.example.app --version 1.0.0 --saved-pages true
```

The zip holds the app project and its bootstrap scripts. `dotnet prism-mobile-bundle --help` lists every
setting: start URL, icon and splash, push notifications, biometric sign-in, device capture, saved
pages and journey status.

From there, follow
[Building a mobile app](https://github.com/jonnymuir/Umbraco.Prism/blob/main/docs/walkthroughs/building-a-mobile-app.md)
and the [mobile app CI/CD pipeline guide](https://github.com/jonnymuir/Umbraco.Prism/blob/main/docs/guides/mobile-app-ci-cd-pipeline.md),
which build the generated project and upload it to TestFlight and Google Play.
