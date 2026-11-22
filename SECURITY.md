# Security policy

## Responsibility

Prism is open source under the MIT licence and is provided "as is", without warranty. Securing a
deployment is the responsibility of whoever deploys it. Prism has had no independent audit or
penetration test, and nothing here is a promise that it is secure or fit for a purpose. See
[Security responsibility and assurance](docs/security/responsibility.md).

## Reporting a vulnerability

Please report vulnerabilities privately through
[GitHub's private vulnerability reporting](https://github.com/jonnymuir/Umbraco.Prism/security/advisories/new),
not in a public issue or pull request. Include the package version, your Umbraco version and
identity provider, what an attacker can do, and the steps to reproduce it.

This is a solo-maintained project, so reports are answered as soon as possible rather than
against a fixed timescale. Fixes ship in a new release, credited in the advisory unless you'd
rather not be named.

## Supported versions

Only the latest release receives security fixes, on a best-effort basis with no service level.

## Scope

In scope: the `UmbracoPrism`, `UmbracoPrism.Automate` and `UmbracoPrism.uSync` packages, and the
mobile apps Prism generates. Of particular interest: one tenant reaching another tenant's
members, sessions, branding or secrets; bypassing sign-in or a page access policy; anything in the OIDC, token refresh or biometric sign-in flows that deviates from
the specifications; and secrets reaching a response, a log or the browser.

## Where Prism implements a standard itself

Most of Prism's authentication uses ASP.NET Core and Microsoft libraries. These parts are Prism's
own code, so they are the best places to look: the authorization code redemption, multi-issuer
bearer validation and the signing-key cache, tenant resolution from the request hostname, the
biometric credential protocol, the mobile sign-in handover, and the member session's lifetime and
re-check. The [authentication and trust review](docs/security/auth-trust-review.md) maps each layer
to a library or to Prism's own code, and lists the open findings.

Out of scope: `UmbracoPrism.TestSite`, `UmbracoPrism.MockBusinessApp`, `UmbracoPrism.AppHost` and
the Keycloak demo realm. They are demo hosts with published demo credentials. A vulnerability
that they *demonstrate* in the package is in scope, so report it if in doubt.

Service-design features (journeys, caseworker queues, the blueprint editor) come from Wayfinder;
report those to [Wayfinder](https://github.com/jonnymuir/Wayfinder/security/advisories/new) or
[Wayfinder.Umbraco](https://github.com/jonnymuir/Wayfinder.Umbraco/security/advisories/new).
