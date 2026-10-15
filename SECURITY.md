# Security policy

## Reporting a vulnerability

Please report vulnerabilities privately through
[GitHub's private vulnerability reporting](https://github.com/jonnymuir/Umbraco.Prism/security/advisories/new),
not in a public issue or pull request. Include the package version, your Umbraco version and
identity provider, what an attacker can do, and the steps to reproduce it.

This is a solo-maintained project, so reports are answered as soon as possible rather than
against a fixed timescale. Fixes ship in a new release, credited in the advisory unless you'd
rather not be named.

## Supported versions

Only the latest release receives security fixes.

## Scope

In scope: the `UmbracoPrism` package (`src/UmbracoPrism.Core`), the mobile apps it generates,
and the `UmbracoPrism.uSync` and `UmbracoPrism.Automate` projects that sites copy from this repo. Of particular interest: one tenant
reaching another tenant's members, sessions, branding or secrets; bypassing sign-in or a page
access policy; anything in the OIDC, token refresh or biometric sign-in flows that deviates from
the specifications; and secrets reaching a response, a log or the browser.

Out of scope: `UmbracoPrism.TestSite`, `UmbracoPrism.MockBusinessApp`, `UmbracoPrism.AppHost` and
the Keycloak demo realm. They are demo hosts with published demo credentials. A vulnerability
that they *demonstrate* in the package is in scope, so report it if in doubt.

Service-design features (journeys, caseworker queues, the blueprint editor) come from Wayfinder;
report those to [Wayfinder](https://github.com/jonnymuir/Wayfinder/security/advisories/new) or
[Wayfinder.Umbraco](https://github.com/jonnymuir/Wayfinder.Umbraco/security/advisories/new).
