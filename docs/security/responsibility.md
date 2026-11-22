# Security responsibility and assurance

Read this before you put Prism in front of real users. It says what Prism is, what it does not
promise, and what you must do yourself.

**Prism is open source software, licensed under the [MIT licence](../../LICENSE) and provided "as
is", without warranty of any kind. You are responsible for the security of every system you build
with it.** Authentication, multi-tenant isolation and authorisation are security controls that
protect your users and your data, and the person who deploys them is accountable for them. The
author and contributors accept no liability for loss or damage arising from the use of Prism, to
the fullest extent the licence and the law allow.

This page is a plain-language statement, not legal advice, and it does not change the licence.
Where the two differ, the licence governs. If liability matters to your organisation, ask your
own legal team.

## What Prism is

Prism is a library. It gives an Umbraco site per-tenant OpenID Connect sign-in, a member session,
runtime branding and mobile app generation. It sits inside your application, your hosting, your
identity providers and your network, none of which Prism controls. It is not a managed service,
a security product or a compliance programme.

## Multi-tenancy is a security boundary you must verify

Prism serves several tenants from one Umbraco instance and tries to keep them apart: one tenant's
members, sessions, branding and secrets must never reach another tenant. That separation is the
hardest thing Prism does, and a flaw in it is a cross-tenant data breach. Treat it as a
security-critical dependency, not as a feature that works because the documentation says so.

- Test tenant isolation yourself, with your own tenants, your own identity providers and your own
  hostnames, before go-live and after every upgrade.
- Do not assume that a passing Prism test suite covers your configuration. It covers Prism's.
- Decide, with your own architects, whether one shared instance is acceptable for your data, or
  whether your risk calls for separate deployments.

## Do not trust Prism's implementation: verify it

Prism's authors have reviewed their own code, and the result is published in the
[authentication and trust review](auth-trust-review.md). That is a self-review by the people who
wrote the code, assisted by AI tooling. It is not an independent penetration test, an audit or a
certification, and it will have missed things. Nothing in this repository should be read as
saying Prism is secure, certified, compliant or fit for any particular purpose.

Before you rely on Prism:

1. **Review the code that matters to you.** The review lists where Prism uses standard .NET
   libraries and where it implements a standard itself. Those home-made parts carry the most
   risk and deserve the closest reading.
2. **Commission your own security testing.** Run a penetration test against your deployed
   configuration, including tenant isolation, session handling, token validation, the mobile
   sign-in flows and your identity provider setup. [`Design/pentest-checklist.md`](../../Design/pentest-checklist.md)
   is a starting point, not a scope.
3. **Involve your own architecture and security teams.** They own your threat model, data
   classification, regulatory duties and risk acceptance. Prism cannot make those decisions.
4. **Apply your own assurance process.** If your organisation requires supplier assessment,
   software composition analysis, SAST, DAST or a secure development sign-off, run it on Prism as
   you would on any third-party dependency.
5. **Pin and review upgrades.** Read the changelog before upgrading, and retest after.

## Who is responsible for what

| Concern | Prism provides | You are responsible for |
|---|---|---|
| Sign-in | A per-tenant OIDC client built on the ASP.NET Core authentication handlers | Choosing, configuring and securing your identity providers, client registrations, redirect URIs and secrets |
| Sessions | A cookie-backed member session, with server-side storage you can move to Redis | Data Protection key storage and rotation, session store hardening, cookie and TLS settings at your edge |
| Tenant isolation | Hostname-based tenant resolution and per-tenant validation | Hostnames, proxy and CDN configuration, forwarded header trust, and verifying isolation in your deployment |
| Authorisation | Page access policies and the member's claims | The access rules you write, and every downstream API's own authorisation |
| Secrets | A vault reference model for client secrets | The vault, its access policy, rotation and audit |
| Mobile | A generated Capacitor app and biometric sign-in | Signing keys, store submission, device policy and the app's own review |
| Infrastructure | Nothing | Hosting, network, patching, logging, monitoring, backups and incident response |
| Data protection law | Nothing | Privacy notices, lawful basis, retention, subject rights and breach reporting |

## Where Prism follows the platform, and where it does not

Prism prefers standard .NET and ASP.NET Core mechanisms (the OpenID Connect and cookie handlers,
Data Protection, JWT bearer validation, `Microsoft.IdentityModel`, `IDistributedCache`) over its
own code, because those are widely reviewed and kept patched by the platform vendor. Prism
implements a standard itself only where the platform offers no suitable component, and each such
place is recorded in the [review](auth-trust-review.md) with its mitigation. A preference for
standard components is a design intent, not a guarantee that every path uses them or that they
are configured safely for you.

## Dependencies and the wider stack

Prism depends on Umbraco, ASP.NET Core and many NuGet and npm packages. A vulnerability in any of
them can affect your system, whatever Prism does. Keep Umbraco, the .NET runtime and all
dependencies current, and watch their advisories as well as Prism's.

## The reference site and demo credentials

`UmbracoPrism.TestSite`, `UmbracoPrism.MockBusinessApp`, `UmbracoPrism.AppHost` and the Keycloak
realm are demonstrations. They carry published demo credentials and relaxed settings. Do not
deploy them, or copy their configuration, into a system that holds real data.

## Reporting a vulnerability

Report it privately, as described in [SECURITY.md](../../SECURITY.md). Prism is maintained by one
person on a best-effort basis. There is no service level, no guaranteed response time and no
guarantee that a fix will be released.

## Pre-production checklist

- [ ] You have read the [authentication and trust review](auth-trust-review.md) and the
      [deployment security guide](../DEPLOYMENT_SECURITY.md).
- [ ] Your security and architecture teams have assessed Prism for your use and accepted the
      residual risk in writing.
- [ ] An independent penetration test of your deployed configuration has covered tenant
      isolation, sessions and token handling.
- [ ] Data Protection keys are persisted, protected and backed up
      (`Prism:DataProtection:RequireConfiguredKeys` is left on).
- [ ] Client secrets live in a vault, and no demo credential or demo setting remains.
- [ ] Your dependency and vulnerability scanning covers Prism and everything it brings in.
- [ ] You have a process for applying Prism, Umbraco and .NET security updates promptly.
- [ ] You have logging, monitoring and an incident response plan for your authentication layer.
