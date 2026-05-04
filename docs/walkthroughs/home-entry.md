# Home Entry Walkthrough

How an end user first encounters the Prism demo and navigates to their first workflow.

## Overview

The home entry journey (`home-entry`) documents the authenticated and unauthenticated views of the Prism TestSite homepage and the path from the homepage hero into the dashboard and workflow hub. It demonstrates:

- **Unauthenticated hero** – The landing page a new visitor sees before signing in
- **Personalised hero** – The signed-in view with a welcome message and direct dashboard link
- **Homepage → Dashboard → Workflow hub** – The full entry path to starting a workflow

## Unauthenticated Homepage

![Signed-out homepage hero — "Your account, your way" heading with Sign In call-to-action](../images/walkthroughs/home-entry/01-signed-out-hero.png)

Before signing in, the homepage presents the product hero with a **Sign In** call-to-action. No dashboard or workflow navigation is shown — protected content is not accessible until the OIDC flow is completed.

## Authenticated Homepage

![Signed-in homepage hero — personalised welcome and "Go to Dashboard" link visible](../images/walkthroughs/home-entry/02-signed-in-hero.png)

After signing in via Keycloak, the hero updates to show a personalised welcome message ("Welcome back, Demo User") and replaces the Sign In link with:

- **Go to Dashboard** – direct entry point to the member dashboard
- **Sign Out** – accessible from the navigation

## Dashboard

![Member dashboard — "View Workflows" and "Start Workflow" links, and the Mock Business App API tester](../images/walkthroughs/home-entry/03-dashboard.png)

The `/dashboard` route (protected by `[Authorize]`) shows the member dashboard with two primary workflow entry points:

1. **View Workflows** (`/my-workflows`) – lists all active and completed workflow instances for the signed-in member
2. **Start Workflow** – navigates directly to the first seeded workflow entry point (`/get-in-touch`)

## Workflow Hub

![My Workflows page — workflow instance list](../images/walkthroughs/home-entry/04-workflow-hub.png)

The workflow hub at `/my-workflows` shows all workflow instances owned by the signed-in member. On a freshly seeded environment the list is empty; after starting a workflow an **In Progress** entry appears.

---

[← Back to Walkthroughs](README.md)

---

**Executable spec:** This walkthrough is executed on every PR by [`home-entry.walkthrough.spec.ts`](../../src/UmbracoPrism.Client/tests/walkthroughs/home-entry.walkthrough.spec.ts). Screenshots above regenerate via the [`Capture Walkthrough Screenshots`](../../.github/workflows/capture-screenshots.yml) workflow (manual dispatch). See [`walkthroughs-as-executable-specs`](../../.squad/skills/walkthroughs-as-executable-specs/SKILL.md) for the policy.
