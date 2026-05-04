# Workflow Walkthroughs

Screenshot-driven walkthroughs demonstrating Umbraco.Prism end-to-end — from end-user workflow journeys through authoring, operations, and mobile delivery.

Each walkthrough shows real pages running in the TestSite, the fluent builder API and polymorphic JSON schema that power them, and what Prism does behind the scenes at each step.

---

## End-User Flows

These walkthroughs follow a user through a complete workflow from login to confirmation. Ideal starting points for understanding what Prism looks like from a citizen or customer perspective.

### [Community Enquiry](community-enquiry.md)
Multi-section contact form with conditional radios, checkboxes, and validation.

### [Payment Demo](payment-demo.md)
Two-step workflow with Stripe integration, currency formatting, and the check-answers pattern.

### [Planning Notification](planning-notification.md)
Complex multi-page application with file upload, address lookup, and progressive disclosure. The most comprehensive end-user walkthrough.

### [Information Request](information-request.md)
Data request form with date picker, textarea, and conditional urgency options.

---

## Authoring & Operations

These walkthroughs are aimed at developers and operators building or administering a Prism deployment.

### [Workflow Administration](workflow-administration.md)
How to use the development-only workflow admin panel to inspect, edit, and manage workflow instances and definitions. Covers accessing the panel from the dashboard, viewing instances and state, editing definitions, manually advancing workflows, and resetting instances for testing.

### [Authoring a Workflow](authoring-a-workflow.md)
How to write a new workflow definition using the fluent builder API. Covers the polymorphic JSON model (`type` discriminator, `children[]`, `conditionalChildren`), loading seeds, hot reload, and client/server validation.

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

**Note:** All workflows use the polymorphic component model (`type` discriminator, `children[]` arrays, `conditionalChildren` on Radios/Checkboxes). The four end-user walkthroughs capture the current schema in production use; the authoring walkthrough explains how to create your own.
