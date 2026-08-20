# Service Blueprint Walkthroughs

Screenshot-driven walkthroughs demonstrating Umbraco.Prism end-to-end — from end-user service blueprint journeys through authoring, operations, and mobile delivery.

Each walkthrough shows real pages running in the TestSite, the fluent builder API and polymorphic JSON schema that power them, and what Prism does behind the scenes at each step.

---

## End-User Flows

Service design itself — citizen-facing journeys, caseworker worklists, and downstream support systems — is entirely Wayfinder's job now, composed onto ordinary Prism content via Wayfinder.Umbraco's packaged Block Grid blocks (see `docs/guides/support-systems.md` in the core Wayfinder repo). TestSite hosts two worked examples this way: an anonymous-first "Apply for a juggling licence" citizen journey, and a "Submit contributions file" + caseworker queue demo backed by a real downstream support system (Mock Business App).

### [Home Entry](home-entry.md)
How a user first encounters the Prism demo and navigates from the homepage hero, through the dashboard, into Wayfinder's Block Grid-composed stage and worklist pages.

---

## Authoring & Operations

These walkthroughs are aimed at developers and operators building or administering a Prism deployment.

### [Gateway-First Authoring](gateway-first-authoring.md)
How the gateway-and-route model works. Every move from one stage to another happens through a gateway. Worked example: the Leave Request 5-gateway fan-in pattern.

### [Authoring a Service Blueprint](authoring-a-service-blueprint.md)
How to wire the Prism Service Blueprint Editor into your Umbraco app — NuGet packages, DI registration, doctypes, route hijacking, Razor templates, and where to host the editor. The integration recipe for integrators starting from scratch.

### [Creating a Tenant](creating-a-tenant.md)
How to add a new tenant in the Umbraco backoffice — host binding, OIDC authority, branding — and how `PrismTenantMiddleware` picks it up without a restart.

### [Design System](design-system.md)
The Prism design system end-to-end: GDS-aligned Lit web components in Storybook, `@property` + `@prism` CSS annotations, and how design tokens flow from the backoffice into live CSS variables consumed by every component.

---

## Mobile & Notifications

### [Push Notifications](push-notifications.md)
End-to-end push notifications: VAPID key generation, browser subscription UI, triggering from the backoffice, and receiving on device. Covers both web push and Capacitor native push (FCM → APNs/FCM), with links to the canonical architecture and decision docs.

### [Building a Mobile App](building-a-mobile-app.md)
Building a Capacitor iOS/Android app from a Prism service blueprint. Covers the shell structure, biometric authentication, deep link handling, and high-level build steps for both platforms.

---

**Reference contract:** The reference business app (`src/UmbracoPrism.MockBusinessApp`) seeds exactly four demo service blueprints at runtime from authored sources (`src/UmbracoPrism.MockBusinessApp/service-blueprint-authored/`). These four service blueprints are the authoritative reference implementation:
- **planning** — Planning Application service blueprint
- **leave-request** — Leave Request service blueprint (demonstrates 5-gateway fan-in pattern)
- **community-enquiry** — Get in Touch contact form
- **information-request** — Information Request form

All four are available to the editor, front-end journey, and runtime engine. Downstream applications replace the reference repository with their own authored service blueprint store (filesystem, database, etc.) by implementing `ServiceBlueprintSource`. See [Embedding the Service Blueprint Editor](../guides/embedding-the-service-blueprint-editor.md) for details.

**Authoring:** All service blueprints use the gateway-and-route model. Every move from one stage to another happens through a gateway. See [Gateway-First Authoring](gateway-first-authoring.md) for the structural consequences and the fan-in pattern.
