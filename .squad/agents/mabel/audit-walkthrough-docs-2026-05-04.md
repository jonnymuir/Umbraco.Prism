# WALKTHROUGH AUDIT REPORT
## Mabel | Technical Writer & Release Manager
## Date: 2026-05-04T11:46:55.877+01:00

---

## SCOPE OF AUDIT

Verified:
- Current walkthrough documentation claims and completeness
- Existing screenshot coverage (21 screenshots across 10 directories)
- Executable spec infrastructure (9 Playwright specs)
- Test support and capture tooling
- Missing coverage gaps and documentation claims

---

## CURRENT STATE SUMMARY

### Walkthroughs Documented (10 total)
1. README.md — Index (52 lines)
2. authoring-a-workflow.md — Developer guide (387 lines) **[PARTIAL CAPTURE]**
3. building-a-mobile-app.md — iOS/Android guide (326 lines) **[PARTIAL CAPTURE]**
4. community-enquiry.md — End-user workflow (103 lines) ✅ Complete
5. creating-a-tenant.md — Operator guide (213 lines) **[PARTIAL CAPTURE]**
6. design-system.md — Design tokens walkthrough (301 lines) **[PARTIAL CAPTURE]**
7. information-request.md — End-user workflow (98 lines) ✅ Complete
8. payment-demo.md — End-user workflow (78 lines) ✅ Complete
9. planning-notification.md — Complex end-user workflow (432 lines) ✅ Complete
10. push-notifications.md — Feature guide (296 lines) **[PARTIAL CAPTURE]**

### Screenshot Coverage
- **Total screenshots:** 21 PNG files
- **Shared screenshots:** 2 (01-homepage.png: 1280×9447px, 02-dashboard.png: 1280×2428px)
- **Workflow-specific screenshots:** 19 across 8 workflows
- **Coverage by walkthrough:**
  - planning-notification: 9 screenshots (complete happy path)
  - community-enquiry: 4 screenshots
  - payment-demo: 3 screenshots
  - information-request: 3 screenshots
  - authoring-a-workflow: 0 screenshots (pending backoffice)
  - building-a-mobile-app: 0 screenshots (pending)
  - creating-a-tenant: 0 screenshots (pending backoffice)
  - design-system: 0 screenshots (pending backoffice)
  - push-notifications: 0 screenshots (pending backoffice)

### Executable Spec Status
All 9 specs exist and run on PR CI:
- authoring-a-workflow.walkthrough.spec.ts (39 lines) — minimal
- building-a-mobile-app.walkthrough.spec.ts (37 lines) — minimal
- community-enquiry.walkthrough.spec.ts (91 lines) — full happy path
- creating-a-tenant.walkthrough.spec.ts (37 lines) — minimal
- design-system.walkthrough.spec.ts (34 lines) — minimal
- information-request.walkthrough.spec.ts (55 lines) — full happy path
- payment-demo.walkthrough.spec.ts (61 lines) — full happy path
- planning-notification.walkthrough.spec.ts (125 lines) — full happy path + validation
- push-notifications.walkthrough.spec.ts (34 lines) — minimal

---

## KEY FINDINGS

### Issue 1: Homepage Screenshot Excessive Height
**Current state:** 01-homepage.png is 1280 × 9447 pixels
**Impact:** Full-page screenshot captures entire scrollable content, overwhelming in docs
**Referenced by:** planning-notification.md, information-request.md, community-enquiry.md, payment-demo.md
**Severity:** 🟡 Medium (affects 4 walkthroughs)

**Recommendation:**
- Crop to 1280 × 2200–2400px (header + hero section + key CTAs visible)
- Show enough to understand branding and navigation structure
- This is a **capture tooling concern** (Playwright `clip` region or post-capture crop)

### Issue 2: Dashboard Screenshot Could Be More Compact
**Current state:** 02-dashboard.png is 1280 × 2428 pixels
**Impact:** Reasonable height but could be tightened depending on content
**Referenced by:** planning-notification.md, information-request.md
**Severity:** 🟢 Low (acceptable but worth reviewing)

**Recommendation:**
- Verify it shows enough workflow tiles to be representative (checking current run)
- If it shows 3+ workflow tiles, consider whether it could be cropped to ~2000px
- This is a **capture tooling concern**

### Issue 3: Missing Admin/Operator Workflow Walkthrough
**Current state:** No walkthrough documents the MockBusinessApp admin panel or workflow state management
**Gap:** Users cannot see how to:
- Navigate to `/admin/workflow` from the TestSite dashboard
- View all workflow instances for a definition
- Manually advance a workflow instance to test different states
- Reset instances for testing
- View workflow definition JSON and edit it live

**Claim in docs:** planning-notification.md mentions "View the Engine Logs" in Aspire Dashboard but does NOT document the admin panel itself
**Severity:** 🔴 High (admin operations are not documented)

**Recommendation:**
- Create **new walkthrough:** `docs/walkthroughs/workflow-administration.md`
- Document the `/admin/workflow` panel as a demo/testing tool
- Include 4–6 screenshots:
  1. Dashboard → (navigation to admin panel if available, or direct to localhost:7245/admin/workflow)
  2. Workflow instance list view
  3. Instance details / state transition UI
  4. Definition editor (if needed)
  5. Reset workflow instance flow
- This is a **documentation gap** (the feature exists in code; docs are missing)

### Issue 4: Backoffice Walkthroughs Have Pending Captures
**Status:** Several walkthroughs contain placeholder comments for missing backoffice screenshots:

| Walkthrough | Pending captures | Status |
|---|---|---|
| authoring-a-workflow.md | 01-backoffice-workflow-key.png | Needed: show Content → new Workflow Page property |
| creating-a-tenant.md | 01-backoffice-login.png, 02-prism-dashboard.png, 03-new-tenant-modal.png, 04-branding-tab.png | Critical: 4 backoffice screenshots |
| building-a-mobile-app.md | 02-backoffice-biometric-setting.png | Needed: show Prism Dashboard biometric toggle |
| design-system.md | 04-branding-editor.png | Needed: show Branding tab in Prism Dashboard |
| push-notifications.md | 03-backoffice-send-notification.png | Needed: show Announcements section |

**Severity:** 🟡 Medium (affects 5 walkthroughs, all backoffice flows)

**Recommendation:**
- These specs are **not automated** (they require Umbraco backoffice navigation, which is harder to automate end-to-end)
- **Solution:** Create **one shared backoffice screenshot** sequence OR minimal manual procedure in each walkthrough:
  - Option A: Document as "<!-- manual capture: requires Umbraco backoffice login -->" with clear steps
  - Option B: Create a separate spec file for backoffice flows using a logged-in backoffice session
- **Tooling concern:** If automated, need Umbraco BFF login + backoffice element navigation in Playwright

### Issue 5: No Mechanism to Hide Mobile Helper in Walkthrough Sessions
**Current state:** prism-mobile-nav tests exist; no mention of hiding the component during walkthrough capture
**Concern:** If the TestSite renders a mobile navigation helper, it will appear in full-page screenshots, cluttering walkthrough images
**Severity:** 🟠 Medium (speculative — depends on whether helper is visible in walkthrough viewport)

**Recommendation:**
- **Verify:** Check if current screenshots show a mobile helper element
- **If visible:** Add to test environment setup (test-only CSS class or feature flag to hide mobile helpers during CAPTURE_SCREENSHOTS=1)
- **If not visible:** No action needed; may already be hidden by viewport size or CSS media query
- This is a **capture tooling concern** (test session configuration, not product change)

### Issue 6: Shared Screenshots Create Multi-Doc Dependencies
**Current state:** 01-homepage.png and 02-dashboard.png are used by 4 walkthroughs each
**Impact:** Any update to shared screenshots affects all dependent walkthroughs simultaneously
**Severity:** 🟡 Medium (maintenance risk)

**Recommendation:**
- Document this dependency in `.squad/skills/docs-walkthrough-screenshots/SKILL.md` (add section: "Shared Screenshots")
- When refreshing shared screenshots via `CAPTURE_SCREENSHOTS=1`, verify they still align with all 4 walkthroughs' narratives
- Consider if a tighter crop of homepage/dashboard would reduce visual noise without losing context

---

## EDITORIAL CLAIMS vs. REALITY

### What Walkthroughs Claim vs. What's Implemented

| Claim | Reality | Gap? |
|---|---|---|
| "All workflows use the polymorphic component model" | ✅ Confirmed in schema | No |
| "Authoring a workflow" demonstrates fluent builder API | ✅ Documented in code examples, 387 lines | Backoffice binding screenshots missing |
| "Creating a tenant requires Umbraco backoffice" | ✅ Confirmed; full flow documented | Screenshots pending (4 total) |
| "Design System shows token pipeline" | ✅ Documented, but backoffice editor screenshot missing | Screenshot pending |
| "Push notifications can be triggered from backoffice" | ✅ Confirmed in code, endpoint documented | Screenshot pending |
| "End-to-end workflows work (4 demos)" | ✅ All 4 have complete specs and screenshots | No gap |
| "Workflow definitions are JSON seedable" | ✅ Confirmed; seeds in workflow-seeds/ | No gap |
| "MockBusinessApp engine powers workflows" | ✅ Confirmed; admin panel at /admin/workflow | **Not documented in walkthroughs** |

---

## SUMMARY OF CHANGES RECOMMENDED

### Immediate (High Priority)

1. **Create workflow-administration.md walkthrough** (6–8 screenshots)
   - Show `/admin/workflow` instance list, state management, definition editor
   - **Responsibility:** Documentation (Mabel) + optional Playwright spec (if automatable)
   - **Tooling:** Capture tooling should make `/admin/workflow` accessible in walkthrough sessions

2. **Crop homepage screenshot to ~2200–2400px**
   - Update `docs/images/walkthroughs/shared/01-homepage.png`
   - **Responsibility:** Capture tooling (Playwright crop config or post-process script)
   - **Automated:** Yes, via CAPTURE_SCREENSHOTS workflow with clip region

3. **Add missing backoffice screenshots**
   - creating-a-tenant.md: 4 critical captures (login, dashboard, new tenant modal, branding)
   - authoring-a-workflow.md: 1 capture (Workflow Key property in Content)
   - building-a-mobile-app.md, design-system.md, push-notifications.md: 1 each
   - **Responsibility:** Capture tooling (automated) or manual procedure (documented in markdown)
   - **Tooling:** May require Umbraco backoffice login automation in Playwright

### Medium Priority

4. **Verify/hide mobile helper in walkthrough sessions**
   - Check if prism-mobile-nav appears in current screenshots
   - If visible, disable via environment variable or test-only CSS class
   - **Responsibility:** Test setup tooling (Playwright config or test fixture)

5. **Document shared screenshot dependencies**
   - Add guidance to `.squad/skills/docs-walkthrough-screenshots/SKILL.md` on maintaining shared screenshots
   - List which walkthroughs depend on each shared image

---

## TOOLING vs. PRODUCT CHANGES (Decision Matrix)

| Concern | Tooling Fix? | Product Fix? | Decision |
|---|---|---|---|
| Homepage too tall | ✅ Crop in Playwright | ❌ No UI change needed | **Tooling only:** Update screenshot capture to use clip region |
| Mobile helper visible in screenshots | ✅ Hide via test config | ❌ No UI change needed | **Tooling only:** Add CSS class or feature flag during CAPTURE_SCREENSHOTS=1 |
| Admin workflow not documented | ❌ Not tooling | ✅ Doc addition | **Documentation only:** Write workflow-administration.md |
| Missing backoffice captures | ✅ Playwright spec OR ❌ Manual | ❌ No UI change | **Tooling + Documentation:** Automate if possible, document manual steps if not |
| Dashboard screenshot height | ✅ Verify/crop if needed | ❌ No UI change | **Tooling:** Check if crop improves clarity |

---

## VERIFICATION CHECKLIST FOR IMPLEMENTATION

When implementing changes, verify:

- [ ] Homepage screenshot cropped; file size reduced from 9447px
- [ ] Dashboard screenshot verified; height acceptable or cropped
- [ ] workflow-administration.md created with 6+ admin panel screenshots
- [ ] All "<!-- pending capture -->" comments updated or removed
- [ ] Mobile helper (if visible) hidden during CAPTURE_SCREENSHOTS=1 sessions
- [ ] Shared screenshot dependencies documented
- [ ] All backoffice walkthroughs now have at least 1 screenshot each
- [ ] Specs that don't automate backoffice flows have clear "manual capture" instructions
- [ ] All walkthrough specs still pass PR CI (CAPTURE_SCREENSHOTS=0 mode)

---

END AUDIT REPORT
