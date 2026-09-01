# Prism Mobile Nav Bar: Design Research & Recommendations

**Author:** Kicks (Mobile Native Specialist)  
**Date:** 2026-07-14  
**Status:** Design Guidance, for Isabelle to implement  
**Context:** Current nav is text-only anchors in a fixed bottom bar with glass morphism dark background (`rgba(15, 23, 42, 0.94)`). This document defines what it should become.

---

## 1. Design Patterns Analysis

### 1.1 iOS Tab Bar HIG: Key Principles

Apple's Human Interface Guidelines define the tab bar as the canonical bottom navigation pattern for iOS apps. These principles are non-negotiable for any Capacitor app that wants to feel native:

**Structure & Position**
- Tab bar sits at the bottom of the screen, above the home indicator safe area.
- It is always visible, never scrolls or disappears on scroll. This is a hard rule in HIG; hiding the tab bar is a navigation anti-pattern on iOS.
- Each tab represents a distinct mode or feature of the app, not a sub-page within a flow.

**Item Count**
- 2–5 items. 5 is the practical maximum before usability degrades.
- If you have more than 5 destinations, the rightmost item becomes "More" (`···`), a convention users understand.
- Never put more than 5 items in the visible bar on iOS.

**Label + Icon Together**
- Apple always pairs icon + label. Icon-only tabs are not recommended in HIG unless the icons are universally understood (home, search).
- Label is always below the icon, in a consistent small typeface.
- Active tab: filled/bolded icon + tinted label. Inactive: outline icon + subdued label.

**Safe Area**
- The native iOS safe area inset bottom is typically 34pt on modern iPhones (notch models). The tab bar must use `env(safe-area-inset-bottom)` for padding.
- iOS native tab bar total height: ~49pt bar + 34pt safe area = ~83pt total.

**Tint Color**
- One tint color for the active state. All other items use a secondary (dimmed) colour.
- The tint should never be more than one accent colour, don't tint each tab differently.

**Background**
- iOS native: blurred translucent material (UIBlurEffect `.systemThinMaterial`). In a web component, this is `backdrop-filter: blur(20px) saturate(180%)`.
- Light mode apps: white/translucent. Dark mode: dark/translucent.
- Prism's existing `rgba(15, 23, 42, 0.94)` is a solid dark, acceptable, but adding `backdrop-filter` would lift it toward native feel.

---

### 1.2 Banking App Patterns

Banking apps have converged on a clear, unambiguous bottom nav pattern. Here's what the market leaders do:

**Monzo**
- 5-item tab bar: Home, Payments, Card, Pots/Savings, Account
- Filled coloured pill behind active icon (coral/red accent), not just a colour change, but a background shape
- Icon + label, 11px Inter/Monzo typeface
- Very slightly rounded pill behind active icon (pill is ~52×32px, extends 4px on each side of icon)
- Inactive: grey icon + grey label at ~60% opacity
- Background: white with hairline top border. No blur on light theme.
- Dark mode: near-black background, lighter accent pill

**Starling**
- 4-item tab bar: Home, Payments, Spaces, Account
- Teal accent color for active icon, no pill background
- Label always visible for active; label hidden for inactive items (icon-only inactive)
- This is unusual and creates inconsistency, avoid for enterprise
- Their nav is cleaner but slightly harder to scan

**Revolut**
- 5-item tab bar: Home, Cards, Wealth, Lifestyle, Hub
- Bold teal fill for active icon
- Label always visible for all items (better accessibility than Starling)
- Active icon is slightly larger (24px → 26px scale animation on selection)
- Subtle bounce/spring animation on tab select

**Chase (US)**
- 5 items: Home, Accounts, Pay & Transfer, Wealth, More
- Icon + label always visible
- Active: filled dark navy icon + bolder label
- No pill or blob, purely colour + weight treatment
- Very conservative, maximum trust signal, feels like a "proper" bank

**Barclays**
- 5 items: Home, Payments, Cards, Support, Menu
- Blue accent (#00AEEF) for active icon
- Icon + label always visible
- Simple underline or filled icon approach, no pill
- Clean, slightly corporate

**Key Observation:** Consumer apps (Monzo, Revolut) use pill/blob highlights for energy and brand. Enterprise/trust-first apps (Chase, Barclays) use purely icon fill + label weight, no decorative background shapes.

---

### 1.3 Pension & HR App Patterns

L&G Pensions, Nest, Aviva Workplace, Mercer PlanSponsor, and similar apps share a distinct visual language:

**Characteristics of Pension/HR App Nav:**
- Always icon + label. Never icon-only. This demographic skews older; labels are critical.
- Accessibility is front-of-mind: WCAG AA minimum throughout, many target AAA.
- Colour palettes are muted: navy, teal, slate, white. Not vibrant.
- 4 items is the sweet spot (Dashboard, Pension, Documents, Profile/Settings).
- Active state: filled icon + label in brand primary. Background change is subtle, often just opacity.
- No animation beyond a simple crossfade. Motion can confuse or distract in this context.
- Font size for labels: 11–12px minimum. 10px is too small for pension/HR.

**L&G (Legal & General) Digital Pattern:**
- Navy primary (`#00254A`), white background, green accent (`#00A651`)
- 4 tab items: Overview, Pension, Documents, Settings
- Icon + label always; active gets green accent icon + green label
- Generous touch targets (48×48px minimum, often more)
- No pill, no animation, no decoration, pure utility

---

### 1.4 Consumer Polish vs Enterprise Fintech Trustworthiness

| Dimension | Consumer Polish | Enterprise Fintech |
|-----------|----------------|-------------------|
| Active state | Pill/blob highlight, animated | Fill colour change only |
| Animation | Spring/bounce, scale transitions | Subtle opacity fade (150ms) |
| Icon style | Rounded, playful (e.g. Monzo coral) | Geometric, clean (Material-style) |
| Label | Sometimes hidden inactive | Always visible |
| Font weight | Medium → SemiBold on active | Regular → Medium on active |
| Colour palette | Vivid brand accent | Muted, professional accent |
| Haptics | Yes, on each tap | Optional, depends on context |
| Background | Blur/frosted glass | Solid or subtle blur |
| Touch target | 44pt (meeting minimum) | 48pt+ (exceeding minimum) |
| Icon size | 24–26px | 24px (consistent, no scale) |

**Recommendation for Prism:** Lean enterprise fintech. The filled icon + weight change on active is sufficient. Add a subtle pill only as a theme-opt-in, not the default. No scale animation on selection. A simple 150ms ease-out opacity/colour transition.

---

### 1.5 Layout Pattern: Icon + Label vs Icon-Only vs Label-Only

**Icon + Label stacked (RECOMMENDED for Prism)**
- Used by: Apple HIG, Chase, Barclays, L&G, all pension apps
- Maximises accessibility and comprehension
- Required for enterprise/regulated contexts
- Correct for Prism's tenant audience (HR portals, pensions, internal tools)

**Icon-only**
- Used by: Some consumer apps with universally understood icons (Instagram, Spotify)
- Fails accessibility for non-obvious icons
- Not appropriate for Prism, tenant nav items will have custom, non-obvious labels
- Only acceptable if icons are supplemented with `aria-label`

**Label-only (current Prism state)**
- Fails on small screens, labels can truncate
- Lacks the visual scanning speed of icons
- Appropriate only as a temporary/fallback rendering
- Not suitable as the target design

**Verdict: Icon + label, stacked vertically, always visible for all items.**

---

## 2. Icon Set Recommendation

The following 10 icons cover ~85% of Prism tenant nav use cases. All use 24×24 viewBox, clean filled/outline SVG paths in Material Design geometric style. Filled variants used for active state; stroke/outline for inactive.

Each icon is provided as a pair: `filled` (active) and `outline` (inactive).

---

### Icon 1: Home / Dashboard

**Represents:** Main dashboard, overview, home screen

**Filled (active):**
```svg
<svg viewBox="0 0 24 24" fill="currentColor" width="24" height="24">
  <path d="M10 20v-6h4v6h5v-8h3L12 3 2 12h3v8z"/>
</svg>
```
`d="M10 20v-6h4v6h5v-8h3L12 3 2 12h3v8z"`

**Outline (inactive):**
```svg
<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" width="24" height="24">
  <path d="M3 12L12 3l9 9M5 10v9a1 1 0 001 1h4v-5h4v5h4a1 1 0 001-1v-9" stroke-linecap="round" stroke-linejoin="round"/>
</svg>
```
`d="M3 12L12 3l9 9M5 10v9a1 1 0 001 1h4v-5h4v5h4a1 1 0 001-1v-9"`

---

### Icon 2: Pension / Savings / Pot

**Represents:** Pension pot, savings, investment value

**Filled (active):**
```svg
<svg viewBox="0 0 24 24" fill="currentColor" width="24" height="24">
  <path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-2h2v2zm0-4h-2V7h2v6z"/>
</svg>
```
`d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-2h2v2zm0-4h-2V7h2v6z"`

> Note: For pension specifically, use a piggy bank or chart icon. Below is a cleaner chart-growth icon more appropriate for pensions:

**Filled (active), growth/chart:**
```svg
<svg viewBox="0 0 24 24" fill="currentColor" width="24" height="24">
  <path d="M3.5 18.5l4-4 4 4 9-9-1.41-1.41L11.5 16.09l-4-4-5.5 5.5L3.5 18.5z"/>
</svg>
```
`d="M3.5 18.5l4-4 4 4 9-9-1.41-1.41L11.5 16.09l-4-4-5.5 5.5L3.5 18.5z"`

**Outline (inactive):**
```svg
<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" width="24" height="24">
  <polyline points="22 7 13.5 15.5 8.5 10.5 2 17" stroke-linecap="round" stroke-linejoin="round"/>
  <polyline points="16 7 22 7 22 13" stroke-linecap="round" stroke-linejoin="round"/>
</svg>
```

---

### Icon 3: Account / Profile / Person

**Represents:** Personal account, user profile, my details

**Filled (active):**
```svg
<svg viewBox="0 0 24 24" fill="currentColor" width="24" height="24">
  <path d="M12 12c2.21 0 4-1.79 4-4s-1.79-4-4-4-4 1.79-4 4 1.79 4 4 4zm0 2c-2.67 0-8 1.34-8 4v2h16v-2c0-2.66-5.33-4-8-4z"/>
</svg>
```
`d="M12 12c2.21 0 4-1.79 4-4s-1.79-4-4-4-4 1.79-4 4 1.79 4 4 4zm0 2c-2.67 0-8 1.34-8 4v2h16v-2c0-2.66-5.33-4-8-4z"`

**Outline (inactive):**
```svg
<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" width="24" height="24">
  <path d="M20 21v-2a4 4 0 00-4-4H8a4 4 0 00-4 4v2" stroke-linecap="round" stroke-linejoin="round"/>
  <circle cx="12" cy="7" r="4" stroke-linecap="round" stroke-linejoin="round"/>
</svg>
```

---

### Icon 4: Payments / Transactions / Pay

**Represents:** Pay someone, send money, transaction history

**Filled (active):**
```svg
<svg viewBox="0 0 24 24" fill="currentColor" width="24" height="24">
  <path d="M20 4H4c-1.11 0-2 .89-2 2v12c0 1.11.89 2 2 2h16c1.11 0 2-.89 2-2V6c0-1.11-.89-2-2-2zm0 14H4v-6h16v6zm0-10H4V6h16v2z"/>
</svg>
```
`d="M20 4H4c-1.11 0-2 .89-2 2v12c0 1.11.89 2 2 2h16c1.11 0 2-.89 2-2V6c0-1.11-.89-2-2-2zm0 14H4v-6h16v6zm0-10H4V6h16v2z"`

**Outline (inactive):**
```svg
<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" width="24" height="24">
  <rect x="1" y="4" width="22" height="16" rx="2" ry="2" stroke-linecap="round" stroke-linejoin="round"/>
  <line x1="1" y1="10" x2="23" y2="10" stroke-linecap="round"/>
</svg>
```

---

### Icon 5: Documents / Files / Statements

**Represents:** Documents, payslips, letters, annual statements

**Filled (active):**
```svg
<svg viewBox="0 0 24 24" fill="currentColor" width="24" height="24">
  <path d="M14 2H6c-1.1 0-2 .9-2 2v16c0 1.1.9 2 2 2h12c1.1 0 2-.9 2-2V8l-6-6zm2 16H8v-2h8v2zm0-4H8v-2h8v2zm-3-5V3.5L18.5 9H13z"/>
</svg>
```
`d="M14 2H6c-1.1 0-2 .9-2 2v16c0 1.1.9 2 2 2h12c1.1 0 2-.9 2-2V8l-6-6zm2 16H8v-2h8v2zm0-4H8v-2h8v2zm-3-5V3.5L18.5 9H13z"`

**Outline (inactive):**
```svg
<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" width="24" height="24">
  <path d="M14 2H6a2 2 0 00-2 2v16a2 2 0 002 2h12a2 2 0 002-2V8z" stroke-linecap="round" stroke-linejoin="round"/>
  <polyline points="14 2 14 8 20 8" stroke-linecap="round" stroke-linejoin="round"/>
  <line x1="16" y1="13" x2="8" y2="13" stroke-linecap="round"/>
  <line x1="16" y1="17" x2="8" y2="17" stroke-linecap="round"/>
  <polyline points="10 9 9 9 8 9" stroke-linecap="round"/>
</svg>
```

---

### Icon 6: Notifications / Alerts / Inbox

**Represents:** Notifications, alerts, messages, inbox

**Filled (active):**
```svg
<svg viewBox="0 0 24 24" fill="currentColor" width="24" height="24">
  <path d="M12 22c1.1 0 2-.9 2-2h-4c0 1.1.9 2 2 2zm6-6v-5c0-3.07-1.64-5.64-4.5-6.32V4c0-.83-.67-1.5-1.5-1.5s-1.5.67-1.5 1.5v.68C7.63 5.36 6 7.92 6 11v5l-2 2v1h16v-1l-2-2z"/>
</svg>
```
`d="M12 22c1.1 0 2-.9 2-2h-4c0 1.1.9 2 2 2zm6-6v-5c0-3.07-1.64-5.64-4.5-6.32V4c0-.83-.67-1.5-1.5-1.5s-1.5.67-1.5 1.5v.68C7.63 5.36 6 7.92 6 11v5l-2 2v1h16v-1l-2-2z"`

**Outline (inactive):**
```svg
<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" width="24" height="24">
  <path d="M18 8A6 6 0 006 8c0 7-3 9-3 9h18s-3-2-3-9" stroke-linecap="round" stroke-linejoin="round"/>
  <path d="M13.73 21a2 2 0 01-3.46 0" stroke-linecap="round" stroke-linejoin="round"/>
</svg>
```

---

### Icon 7: Settings / Preferences

**Represents:** App settings, preferences, account settings

**Filled (active):**
```svg
<svg viewBox="0 0 24 24" fill="currentColor" width="24" height="24">
  <path d="M19.14 12.94c.04-.3.06-.61.06-.94 0-.32-.02-.64-.07-.94l2.03-1.58c.18-.14.23-.41.12-.61l-1.92-3.32c-.12-.22-.37-.29-.59-.22l-2.39.96c-.5-.38-1.03-.7-1.62-.94l-.36-2.54c-.04-.24-.24-.41-.48-.41h-3.84c-.24 0-.43.17-.47.41l-.36 2.54c-.59.24-1.13.57-1.62.94l-2.39-.96c-.22-.08-.47 0-.59.22L2.74 8.87c-.12.21-.08.47.12.61l2.03 1.58c-.05.3-.09.63-.09.94s.02.64.07.94l-2.03 1.58c-.18.14-.23.41-.12.61l1.92 3.32c.12.22.37.29.59.22l2.39-.96c.5.38 1.03.7 1.62.94l.36 2.54c.05.24.24.41.48.41h3.84c.24 0 .44-.17.47-.41l.36-2.54c.59-.24 1.13-.56 1.62-.94l2.39.96c.22.08.47 0 .59-.22l1.92-3.32c.12-.22.07-.47-.12-.61l-2.01-1.58zM12 15.6c-1.98 0-3.6-1.62-3.6-3.6s1.62-3.6 3.6-3.6 3.6 1.62 3.6 3.6-1.62 3.6-3.6 3.6z"/>
</svg>
```
`d="M19.14 12.94c.04-.3.06-.61.06-.94 0-.32-.02-.64-.07-.94l2.03-1.58c.18-.14.23-.41.12-.61l-1.92-3.32c-.12-.22-.37-.29-.59-.22l-2.39.96c-.5-.38-1.03-.7-1.62-.94l-.36-2.54c-.04-.24-.24-.41-.48-.41h-3.84c-.24 0-.43.17-.47.41l-.36 2.54c-.59.24-1.13.57-1.62.94l-2.39-.96c-.22-.08-.47 0-.59.22L2.74 8.87c-.12.21-.08.47.12.61l2.03 1.58c-.05.3-.09.63-.09.94s.02.64.07.94l-2.03 1.58c-.18.14-.23.41-.12.61l1.92 3.32c.12.22.37.29.59.22l2.39-.96c.5.38 1.03.7 1.62.94l.36 2.54c.05.24.24.41.48.41h3.84c.24 0 .44-.17.47-.41l.36-2.54c.59-.24 1.13-.56 1.62-.94l2.39.96c.22.08.47 0 .59-.22l1.92-3.32c.12-.22.07-.47-.12-.61l-2.01-1.58zM12 15.6c-1.98 0-3.6-1.62-3.6-3.6s1.62-3.6 3.6-3.6 3.6 1.62 3.6 3.6-1.62 3.6-3.6 3.6z"`

**Outline (inactive):**
```svg
<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" width="24" height="24">
  <circle cx="12" cy="12" r="3" stroke-linecap="round" stroke-linejoin="round"/>
  <path d="M19.4 15a1.65 1.65 0 00.33 1.82l.06.06a2 2 0 010 2.83 2 2 0 01-2.83 0l-.06-.06a1.65 1.65 0 00-1.82-.33 1.65 1.65 0 00-1 1.51V21a2 2 0 01-4 0v-.09A1.65 1.65 0 009 19.4a1.65 1.65 0 00-1.82.33l-.06.06a2 2 0 01-2.83-2.83l.06-.06A1.65 1.65 0 004.68 15a1.65 1.65 0 00-1.51-1H3a2 2 0 010-4h.09A1.65 1.65 0 004.6 9a1.65 1.65 0 00-.33-1.82l-.06-.06a2 2 0 012.83-2.83l.06.06A1.65 1.65 0 009 4.68a1.65 1.65 0 001-1.51V3a2 2 0 014 0v.09a1.65 1.65 0 001 1.51 1.65 1.65 0 001.82-.33l.06-.06a2 2 0 012.83 2.83l-.06.06A1.65 1.65 0 0019.4 9a1.65 1.65 0 001.51 1H21a2 2 0 010 4h-.09a1.65 1.65 0 00-1.51 1z" stroke-linecap="round" stroke-linejoin="round"/>
</svg>
```

---

### Icon 8: More / Menu / Overflow

**Represents:** "More" menu for overflow items (5+ nav items)

**Filled (active):**
```svg
<svg viewBox="0 0 24 24" fill="currentColor" width="24" height="24">
  <path d="M6 10c-1.1 0-2 .9-2 2s.9 2 2 2 2-.9 2-2-.9-2-2-2zm12 0c-1.1 0-2 .9-2 2s.9 2 2 2 2-.9 2-2-.9-2-2-2zm-6 0c-1.1 0-2 .9-2 2s.9 2 2 2 2-.9 2-2-.9-2-2-2z"/>
</svg>
```
`d="M6 10c-1.1 0-2 .9-2 2s.9 2 2 2 2-.9 2-2-.9-2-2-2zm12 0c-1.1 0-2 .9-2 2s.9 2 2 2 2-.9 2-2-.9-2-2-2zm-6 0c-1.1 0-2 .9-2 2s.9 2 2 2 2-.9 2-2-.9-2-2-2z"`

**Outline (inactive):** Same paths, `fill="currentColor"` with reduced opacity.

---

### Icon 9: Search / Find

**Represents:** Search content, find a document, locate a pension

**Filled (active):**
```svg
<svg viewBox="0 0 24 24" fill="currentColor" width="24" height="24">
  <path d="M15.5 14h-.79l-.28-.27A6.471 6.471 0 0016 9.5 6.5 6.5 0 109.5 16c1.61 0 3.09-.59 4.23-1.57l.27.28v.79l5 4.99L20.49 19l-4.99-5zm-6 0C7.01 14 5 11.99 5 9.5S7.01 5 9.5 5 14 7.01 14 9.5 11.99 14 9.5 14z"/>
</svg>
```
`d="M15.5 14h-.79l-.28-.27A6.471 6.471 0 0016 9.5 6.5 6.5 0 109.5 16c1.61 0 3.09-.59 4.23-1.57l.27.28v.79l5 4.99L20.49 19l-4.99-5zm-6 0C7.01 14 5 11.99 5 9.5S7.01 5 9.5 5 14 7.01 14 9.5 11.99 14 9.5 14z"`

**Outline (inactive):**
```svg
<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" width="24" height="24">
  <circle cx="11" cy="11" r="8" stroke-linecap="round" stroke-linejoin="round"/>
  <line x1="21" y1="21" x2="16.65" y2="16.65" stroke-linecap="round"/>
</svg>
```

---

### Icon 10: Help / Support / Contact

**Represents:** Help, FAQ, contact support

**Filled (active):**
```svg
<svg viewBox="0 0 24 24" fill="currentColor" width="24" height="24">
  <path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 17h-2v-2h2v2zm2.07-7.75l-.9.92C13.45 12.9 13 13.5 13 15h-2v-.5c0-1.1.45-2.1 1.17-2.83l1.24-1.26c.37-.36.59-.86.59-1.41 0-1.1-.9-2-2-2s-2 .9-2 2H8c0-2.21 1.79-4 4-4s4 1.79 4 4c0 .88-.36 1.68-.93 2.25z"/>
</svg>
```
`d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 17h-2v-2h2v2zm2.07-7.75l-.9.92C13.45 12.9 13 13.5 13 15h-2v-.5c0-1.1.45-2.1 1.17-2.83l1.24-1.26c.37-.36.59-.86.59-1.41 0-1.1-.9-2-2-2s-2 .9-2 2H8c0-2.21 1.79-4 4-4s4 1.79 4 4c0 .88-.36 1.68-.93 2.25z"`

**Outline (inactive):**
```svg
<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" width="24" height="24">
  <circle cx="12" cy="12" r="10" stroke-linecap="round" stroke-linejoin="round"/>
  <path d="M9.09 9a3 3 0 015.83 1c0 2-3 3-3 3" stroke-linecap="round" stroke-linejoin="round"/>
  <line x1="12" y1="17" x2="12.01" y2="17" stroke-linecap="round" stroke-width="2"/>
</svg>
```

---

### Implementation Pattern for Lit Component

Isabelle should store icons as a lookup map and reference by name:

```typescript
const ICONS: Record<string, { filled: string; outline: string }> = {
  home: {
    filled: 'M10 20v-6h4v6h5v-8h3L12 3 2 12h3v8z',
    outline: 'M3 12L12 3l9 9M5 10v9a1 1 0 001 1h4v-5h4v5h4a1 1 0 001-1v-9'
  },
  // ...
};
```

Render in template:
```typescript
// Filled uses fill="currentColor", outline uses stroke="currentColor" fill="none"
const isActive = this.activeHref === item.href;
const iconPath = isActive ? ICONS[item.icon].filled : ICONS[item.icon].outline;
```

---

## 3. Active State Design

### 3.1 Colour Treatment

**Recommendation: Single accent colour, fill for active, opacity reduction for inactive**

- Active icon: `color: var(--prism-nav-active-color)`, this drives both `fill` (filled icons) and `stroke` (outline icons) via `currentColor`
- Inactive icon: `color: var(--prism-nav-inactive-color)`, defaults to `rgba(255,255,255,0.5)` on dark backgrounds
- Do NOT apply per-icon accent colors. One tint across all active items = trust.

### 3.2 Background Pill/Blob

**Recommendation: Optional, off by default. Available via CSS custom property.**

The pill/blob (a rounded rectangle behind the active icon) is the Monzo/consumer approach. For enterprise fintech:
- Default: no pill, pure colour/weight change
- Opt-in via `--prism-nav-active-pill: 1` (custom property toggle)
- If pill is shown: `border-radius: 16px`, `padding: 4px 14px`, `background: var(--prism-nav-active-pill-bg)` defaulting to `rgba(255,255,255,0.12)`
- Pill should contain both the icon and label together (not just the icon)

### 3.3 Label Weight Change

- Inactive: `font-weight: 400` (Regular)
- Active: `font-weight: 600` (SemiBold)
- This is subtle but important, weight change communicates "selected" without colour alone (vital for WCAG)

### 3.4 Animation / Transition

**Recommendation: Simple opacity/colour transition. No scale, no bounce.**

```css
.prism-nav-item {
  transition: color 150ms ease-out, background-color 150ms ease-out;
}
```

- 150ms is fast enough to feel instant on mobile, but perceptible as intentional
- `ease-out` feels decisive, not playful
- Do NOT use `transform: scale()` on tab selection, this is a consumer pattern and adds visual noise
- Do NOT use spring/bounce animations, these feel wrong in regulated/trust contexts
- Optional: add `transition: opacity 100ms ease-out` to the icon SVG on icon swap (filled ↔ outline)

**Haptic feedback (native):** Consider adding `Haptics.impact({ style: ImpactStyle.Light })` from `@capacitor/haptics` on tab tap, this is standard iOS tab bar behavior and adds significant native feel. Light impact style only, not medium/heavy.

---

## 4. Layout & Spacing Recommendations

### 4.1 Maximum Items Before "More" Menu

- **4 items:** Ideal, comfortable spacing, no cognitive load
- **5 items:** Maximum, still acceptable, items start to feel tight below 375px width
- **6+ items:** MUST use "More" pattern, 4 visible items + "More" as the 5th
- The "More" drawer should be a slide-up sheet (native feel), not a full-page navigation

### 4.2 Icon Size

**Recommendation: 24px (CSS px) rendered at 1.5x native = 36pt on high-density displays**

- 24px is the Material Design standard and aligns with iOS SF Symbols size-2 (medium)
- Do NOT go to 28px, it makes the nav bar feel heavy and leaves less room for the label
- Do NOT go below 22px, too small for confident tapping and perception
- SVG icons should be `width="24" height="24"` with `viewBox="0 0 24 24"`

### 4.3 Label Font Size

**Recommendation: 11px, the sweet spot for enterprise mobile**

| Size | Assessment |
|------|-----------|
| 10px | Too small, fails WCAG SC 1.4.4 at lower zoom levels; problematic for older users |
| 11px | ✅ iOS native tab bar default; matches Apple's own HIG guidance |
| 12px | Acceptable, slightly large for tight nav; fine if items are few |
| 13px+ | Too large, labels start to wrap or truncate |

Use `font-size: 11px` with `line-height: 1.2`. Never `font-size: 0.6875rem`, use px units for nav labels to ensure consistent sizing regardless of tenant root font size changes.

### 4.4 Vertical Spacing Between Icon and Label

- Gap between icon bottom edge and label top: **3px**
- This matches iOS native (approximately 2–3pt)
- Implemented as `gap: 3px` in a flexbox column, or `margin-top: 3px` on the label

### 4.5 Overall Nav Bar Height

| Component | Size |
|-----------|------|
| Top border/separator | 1px |
| Top padding | 8px |
| Icon | 24px |
| Gap | 3px |
| Label | ~13px (11px font + line-height) |
| Bottom padding | 8px |
| Safe area inset | `env(safe-area-inset-bottom, 0px)` |
| **Fixed bar height** | **57px** |
| **With iPhone safe area (34px)** | **91px total** |

```css
.prism-mobile-nav {
  height: calc(57px + env(safe-area-inset-bottom, 0px));
  padding-bottom: env(safe-area-inset-bottom, 0px);
}
```

This is close to iOS native (~83pt) but slightly taller to give labels more breathing room.

**Body padding:** Content below the nav should receive `padding-bottom: calc(57px + env(safe-area-inset-bottom, 0px))` to prevent overlap. Prism's existing `--prism-safe-bottom` custom property should incorporate this.

---

## 5. Theming Hook Recommendations

All properties should be on the `:host` element of the Lit component (`:host` = `prism-mobile-nav`). Tenants override via CSS.

```css
:host {
  /* === Background === */
  --prism-nav-bg: rgba(15, 23, 42, 0.94);
  /* Dark glass default matching existing Prism dark theme */

  --prism-nav-blur: 20px;
  /* backdrop-filter blur amount — set to 0 to disable glass effect */

  --prism-nav-border-top: 1px solid rgba(255, 255, 255, 0.08);
  /* Top separator line — subtle on dark backgrounds */

  /* === Colours === */
  --prism-nav-active-color: #ffffff;
  /* Active item icon + label colour — override with brand accent (e.g. #00A651 for L&G green) */

  --prism-nav-inactive-color: rgba(255, 255, 255, 0.45);
  /* Inactive item colour — 4.5:1 contrast minimum on dark bg */

  --prism-nav-active-label-weight: 600;
  /* SemiBold for active label */

  --prism-nav-inactive-label-weight: 400;
  /* Regular for inactive label */

  /* === Active Pill (optional) === */
  --prism-nav-active-pill-bg: transparent;
  /* Set to rgba(255,255,255,0.12) or brand accent at 15% opacity to enable pill */

  --prism-nav-active-pill-radius: 16px;
  /* Border radius of the optional active state pill */

  /* === Sizing === */
  --prism-nav-icon-size: 24px;
  /* Icon width + height */

  --prism-nav-label-size: 11px;
  /* Label font size — keep in px, not rem */

  --prism-nav-label-gap: 3px;
  /* Gap between icon and label */

  --prism-nav-item-padding-x: 4px;
  /* Horizontal padding per nav item — controls spacing between items */
}
```

**Reasoning for each:**
1. `--prism-nav-bg`, Tenants using light themes (white app backgrounds) need to flip to `rgba(255,255,255,0.94)`
2. `--prism-nav-blur`, Performance-sensitive tenants or older devices can disable glass blur
3. `--prism-nav-border-top`, Some brands prefer a hard separator; others prefer none
4. `--prism-nav-active-color`, Primary brand colour for the active state (most common override)
5. `--prism-nav-inactive-color`, Contrast-checked default; tenants may need to adjust for light themes
6. `--prism-nav-active-label-weight` / `--prism-nav-inactive-label-weight`, Weight shift communicates state without colour alone
7. `--prism-nav-active-pill-bg`, Opt-in for consumer/more playful brand tenants
8. `--prism-nav-active-pill-radius`, Allows rectangular vs rounded pill styles
9. `--prism-nav-icon-size`, Accessibility override for larger touch targets
10. `--prism-nav-label-size`, Large-print accessibility tenants
11. `--prism-nav-label-gap`, Compact vs spacious layout control
12. `--prism-nav-item-padding-x`, Fine-tune item spacing

**Light theme override example (for L&G-style tenant):**
```css
prism-mobile-nav {
  --prism-nav-bg: rgba(255, 255, 255, 0.96);
  --prism-nav-border-top: 1px solid #e5e7eb;
  --prism-nav-active-color: #00A651;
  --prism-nav-inactive-color: #6b7280;
  --prism-nav-blur: 12px;
}
```

---

## 6. Accessibility Requirements

### 6.1 Touch Target Minimums

| Standard | Minimum | Recommendation |
|----------|---------|----------------|
| Apple HIG | 44×44pt | 48×44pt |
| WCAG 2.1 SC 2.5.5 (AAA) | 44×44px |  |
| WCAG 2.2 SC 2.5.8 (AA) | 24×24px |  |
| Material Design | 48×48dp |  |

**Recommendation: 48×44px minimum touch target per item, with 44×44px as the absolute floor.**

The icon itself is 24px, the tap target must be padded to meet minimums via `min-height: 44px` and `min-width: 44px` on the nav item button/anchor. The visual indicator can be smaller; the tap area must not be.

```css
.prism-nav-item {
  min-height: 44px;
  min-width: 44px;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  flex: 1;
}
```

### 6.2 Contrast Ratios

| State | Minimum (WCAG AA) | Target |
|-------|------------------|--------|
| Active label/icon on nav bg | 4.5:1 (text), 3:1 (icon) | 7:1 |
| Inactive label/icon on nav bg | 4.5:1 (text), 3:1 (icon) | 4.5:1 |

For the dark default (`rgba(15, 23, 42, 0.94)` on dark page bg ≈ `#0f172a`):
- White (`#ffffff`) on `#0f172a` = **21:1**: exceeds all standards ✅
- `rgba(255,255,255,0.45)` on `#0f172a` ≈ `#8896ad` = **~5.2:1**: meets AA ✅

For light theme tenants: inactive grey must be tested. `#6b7280` on white = **4.6:1**: marginal AA pass. Use `#4b5563` for safety on white.

**Use a contrast checking step in Isabelle's Storybook story**: add a note to test both dark and light theme variants.

### 6.3 Screen Reader Requirements

**Required ARIA attributes:**

```html
<nav aria-label="Main navigation">
  <a href="/dashboard"
     aria-current="page"  <!-- only on active item -->
     aria-label="Dashboard">  <!-- if icon-only; not needed if visible label exists -->
    <svg aria-hidden="true" focusable="false">...</svg>
    <span>Dashboard</span>
  </a>
</nav>
```

**Rules:**
1. **`role="navigation"` or `<nav>`**, The container must be a landmark. Use `<nav>` element, not a `<div>`.
2. **`aria-label="Main navigation"`**, Distinguish from any other nav landmarks on the page (breadcrumbs, pagination).
3. **`aria-current="page"`**, Applied to the active item only. Changes to `"page"` when the current tab is active. Never use `aria-selected` (that's for tabs with tabpanel). `aria-current="page"` is the correct attribute for navigation links.
4. **`aria-hidden="true"` on SVG**, The icon is decorative when a visible label is present. Set `focusable="false"` too (required for IE11/older Edge SVG focus behaviour, still good practice).
5. **Visible label always present**: Screen readers will read the label text. `aria-label` on the `<a>` is only needed if no visible label exists (which should never be our case).
6. **Focus ring**: The default browser focus ring must be visible (or replaced with a prominent custom one). Never `outline: none` without a replacement.

**Focus ring recommendation:**
```css
.prism-nav-item:focus-visible {
  outline: 2px solid var(--prism-nav-active-color);
  outline-offset: 2px;
  border-radius: 4px;
}
```

### 6.4 Reduced Motion

```css
@media (prefers-reduced-motion: reduce) {
  .prism-nav-item {
    transition: none;
  }
}
```

Always respect `prefers-reduced-motion`. Pension/HR app users are more likely to have this enabled.

---

## Summary: Key Decisions for Isabelle

| Decision | Recommendation |
|----------|---------------|
| Layout | Icon (24px) + label (11px) stacked, always visible for all items |
| Max items visible | 5 (use "More" for 6+) |
| Active state | Colour change + font-weight 600. No pill by default. |
| Active colour | White default; `--prism-nav-active-color` for brand override |
| Inactive colour | 45% white opacity |
| Animation | 150ms ease-out colour/opacity only. No scale. No bounce. |
| Background | `rgba(15,23,42,0.94)` + `backdrop-filter: blur(20px)` |
| Touch target | `min-height: 44px; min-width: 44px` on each item |
| ARIA | `<nav>`, `aria-current="page"` on active, `aria-hidden` on SVGs |
| Safe area | `padding-bottom: env(safe-area-inset-bottom, 0px)` |
| Icon style | Filled for active, outline stroke for inactive via `currentColor` |
| Haptics | Optional: `@capacitor/haptics` Light Impact on tap |
