# Service Blueprint Walkthroughs

Screenshot-driven walkthroughs demonstrating Umbraco.Prism end-to-end — from end-user service blueprint journeys through authoring, operations, and mobile delivery.

Each walkthrough shows real pages running in the TestSite, the fluent builder API and polymorphic JSON schema that power them, and what Prism does behind the scenes at each step.

---

## End-User Flows

These walkthroughs follow a user through a complete service blueprint from login to confirmation. The reference business app includes exactly four demo service blueprints seeded at runtime: **Community Enquiry**, **Information Request**, **Payment Demo**, and **Planning Application**. Each is available to the editor, the front-end journey, and the runtime engine.

### [Community Enquiry](community-enquiry.md)
Multi-section contact form with conditional radios, checkboxes, and validation. One of the four reference service blueprints.

### [Payment Demo](payment-demo.md)
Two-step service blueprint with currency formatting and the check-answers pattern. One of the four reference service blueprints.

### [Planning Application](planning-service-blueprint-complete.md)
Complete end-to-end planning application service blueprint covering authoring, public entry, member continuation, and back-stage review. Demonstrates the full service blueprint lifecycle from editor through runtime. One of the four reference service blueprints.

### [Information Request](information-request.md)
Data request form with date picker, textarea, and conditional urgency options. One of the four reference service blueprints.

---

## Authoring & Operations

These walkthroughs are aimed at developers and operators building or administering a Prism deployment.

### [Gateway-First Authoring](gateway-first-authoring.md)
How the gateway-and-route model works. Every move from one stage to another happens through a gateway. Worked example: the Leave Request 5-gateway fan-in pattern.

### [Service Blueprint Administration](service-request-administration.md)
How to use the development-only service desk panel to inspect, edit, and manage service requests and definitions. Covers accessing the panel from the dashboard, viewing instances and state, editing definitions, manually advancing service blueprints, and resetting instances for testing.

> **Note:** This walkthrough covers the **development harness** used to simulate the reviewer/operator role during testing. The admin panel is where you play the "reviewer" actor. For complete, end-to-end service blueprints showing how users submit and reviewers approve, see [Payment Demo](payment-demo.md), [Community Enquiry](community-enquiry.md), and [Information Request](information-request.md) — each demonstrates the full submission → review → outcome cycle from both user and operator perspectives. The Service Desk panel is the tool you use to complete those cycles in the local demo.

### [Authoring a Service Blueprint](authoring-a-service-blueprint.md)
How to wire the Prism Service Blueprint Editor into your Umbraco app — NuGet packages, DI registration, doctypes, route hijacking, Razor templates, and where to host the editor. The integration recipe for integrators starting from scratch.

### [Planning Service Blueprint Editor](planning-service-blueprint-editor.md)
A tour of the editor itself — the vertical-lanes canvas, stage inspector, validation rail, JSON Definition tab, and how authors save and publish a service blueprint. *(Wave 1 — screenshots pending.)*

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
