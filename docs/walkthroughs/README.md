# Workflow Walkthroughs

Screenshot-driven walkthroughs demonstrating Umbraco.Prism end-to-end — from end-user workflow journeys through authoring, operations, and mobile delivery.

Each walkthrough shows real pages running in the TestSite, the fluent builder API and polymorphic JSON schema that power them, and what Prism does behind the scenes at each step.

---

## End-User Flows

These walkthroughs follow a user through a complete workflow from login to confirmation. The reference business app includes exactly four demo workflows seeded at runtime: **Community Enquiry**, **Information Request**, **Payment Demo**, and **Planning Application**. Each is available to the editor, the front-end journey, and the runtime engine.

### [Community Enquiry](community-enquiry.md)
Multi-section contact form with conditional radios, checkboxes, and validation. One of the four reference workflows.

### [Payment Demo](payment-demo.md)
Two-step workflow with currency formatting and the check-answers pattern. One of the four reference workflows.

### [Planning Application](planning-workflow-complete.md)
Complete end-to-end planning application workflow covering authoring, public entry, member continuation, and back-stage review. Demonstrates the full workflow lifecycle from editor through runtime. One of the four reference workflows.

### [Information Request](information-request.md)
Data request form with date picker, textarea, and conditional urgency options. One of the four reference workflows.

---

## Authoring & Operations

These walkthroughs are aimed at developers and operators building or administering a Prism deployment.

### [Workflow Administration](workflow-administration.md)
How to use the development-only workflow admin panel to inspect, edit, and manage workflow instances and definitions. Covers accessing the panel from the dashboard, viewing instances and state, editing definitions, manually advancing workflows, and resetting instances for testing.

> **Note:** This walkthrough covers the **development harness** used to simulate the reviewer/operator role during testing. The admin panel is where you play the "reviewer" actor. For complete, end-to-end workflows showing how users submit and reviewers approve, see [Payment Demo](payment-demo.md), [Community Enquiry](community-enquiry.md), and [Information Request](information-request.md) — each demonstrates the full submission → review → outcome cycle from both user and operator perspectives. The Workflow Admin panel is the tool you use to complete those cycles in the local demo.

### [Authoring a Workflow](authoring-a-workflow.md)
How to write a new workflow definition using the fluent builder API. Covers the polymorphic JSON model (`type` discriminator, `children[]`, `conditionalChildren`), loading seeds, hot reload, and client/server validation.

### [Planning Workflow Editor](planning-workflow-editor.md)
How to use the natural-language workflow editor to inspect and modify a planning permission workflow definition. Covers the dual-mode graph/list view, stage inspector, NL change requests, proposal diffs, and the authoring API contract. *(Wave 1 — screenshots pending.)*

### [Creating a Tenant](creating-a-tenant.md)
How to add a new tenant in the Umbraco backoffice — host binding, OIDC authority, branding — and how `PrismTenantMiddleware` picks it up without a restart.

### [Design System](design-system.md)
The Prism design system end-to-end: GDS-aligned Lit web components in Storybook, `@property` + `@prism` CSS annotations, and how design tokens flow from the backoffice into live CSS variables consumed by every component.

---

## Mobile & Notifications

### [Push Notifications](push-notifications.md)
End-to-end push notifications: VAPID key generation, browser subscription UI, triggering from the backoffice, and receiving on device. Covers both web push and Capacitor native push (FCM → APNs/FCM), with links to the canonical architecture and decision docs.

### [Building a Mobile App](building-a-mobile-app.md)
Building a Capacitor iOS/Android app from a Prism workflow. Covers the shell structure, biometric authentication, deep link handling, and high-level build steps for both platforms.

---

**Reference contract:** The reference business app (`src/UmbracoPrism.MockBusinessApp`) seeds exactly four demo workflows at runtime from authored sources (`src/UmbracoPrism.MockBusinessApp/workflow-authored/`). These four workflows are the authoritative reference implementation:
- **planning** — Planning Application workflow
- **community-enquiry** — Get in Touch contact form
- **information-request** — Information Request form
- **payment-demo** — Payment Demo workflow

All four are available to the editor, front-end journey, and runtime engine. Downstream applications replace the reference repository with their own authored workflow store (filesystem, database, etc.) by implementing `IAuthoredWorkflowStore`. See [Reference Business App README](../../src/UmbracoPrism.MockBusinessApp/README.md) for details.

**Authoring:** All workflows use the polymorphic component model (`type` discriminator, `children[]` arrays, `conditionalChildren` on Radios/Checkboxes). The authoring walkthrough explains how to create your own.
