# Isabelle — PT2 Razor Hardening Decision Log

**Date:** 2026-04-30  
**Branch:** `sec/pt2-razor-hardening`  
**Findings addressed:** SEC-PT2-007, SEC-PT2-008

---

## SEC-PT2-007 — Accordion `Content` Razor trap

### Approach taken

Injected `IWorkflowContentSanitizer` at the view layer via `@inject` in
`_PrismComponent-Accordion.cshtml` and routed `accordionSection.Content` through
`Sanitizer.Sanitize()` before it reaches `@Html.Raw`.

**Why view-layer `@inject` rather than the engine seam:**

- Today no producer populates `accordionSection.Content`, so adding sanitization
  at `BuildComponents` would be no-op with no test surface.
- The mission guidance explicitly preferred "as close to the render site as possible
  (defence in depth — even if a producer bypasses the engine seam, the view-layer
  sanitizer catches it)."
- `IWorkflowContentSanitizer` is already registered as a singleton in DI
  (`PrismComposer`), so injection is zero-boilerplate.
- This mirrors the precedent used by the other component partials (Body, InsetText,
  WarningText, Panel, Details) — all of which use `@Html.Raw(Model.Component.Content)`
  and rely on the sanitizer seam for safety. The Accordion was the only one not
  yet wired.

**Note on Panel:** Panel has the same latent trap (no producer sets `Content` today).
It was not in scope for this branch. Suggest raising as SEC-PT2-007b or addressing
in the same engine-seam pass if a producer is ever wired.

### Tests added

`AccordionContentSanitizationTests` (4 tests):
- `<script>` tag stripped; legitimate body paragraph preserved
- `<img onerror=>` stripped (`img` not on GDS allowlist)
- `onclick` on allowed `<a>` stripped; safe `href` preserved
- Legitimate rich text (h3, p, ul, a) passes through intact

---

## SEC-PT2-008 — VinylRecord RTE `@Html.Raw`

### Approach taken

Injected `IWorkflowContentSanitizer` at the view layer via `@inject` and
`@using UmbracoPrism.Shared.Services.Sanitization` in `VinylRecord.cshtml`,
routing the Umbraco RTE `description` field through `Sanitizer.Sanitize()`
before it reaches `@Html.Raw`.

**Why the same singleton (GDS allowlist), not a separate instance:**

- Standard TinyMCE output for an album description is: paragraphs, bold/italic,
  unordered lists, and external links. All of these are in the GDS allowlist.
- The mission guidance was clear: do not widen the GDS allowlist; if VinylRecord
  needs richer formatting, propose it as a separate decision.

### Open question for Jonny: VinylRecord allowlist breadth

**Does the VinylRecord RTE description need anything outside the GDS allowlist?**

The GDS allowlist permits: `h2`, `h3`, `h4`, `p`, `ul`, `ol`, `li`, `blockquote`,
`br`, `strong`, `em`, `b`, `i`, `code`, `abbr`, `span`, `a` + `http/https/mailto/tel`.

Elements that a music catalogue might plausibly want but which are **not** permitted:

| Element | Typical use case | Risk |
|---------|-----------------|------|
| `img` | Album artwork, tour poster | Medium — src/srcset can be abused; needs careful URI restriction |
| `table` | Track listing with durations | Low — no script surface, but complex allowlist for `thead/tbody/tr/td/th` |
| `h1` | Top-level album heading | Very low — but h1 should come from the template, not the RTE body |
| `figure`/`figcaption` | Image with caption | Low |

**Recommendation:** For now the GDS allowlist is sufficient — VinylRecord is a demo
page and content is operator-authored with Umbraco's TinyMCE, which already restricts
what editors can insert. If the VinylRecord use case grows (real customer-facing
catalogue), revisit with a separate `IRteContentSanitizer` registered with a broader
allowlist. **Do not widen the shared `IWorkflowContentSanitizer` allowlist** — it is
used by workflow content where strict GDS alignment is a requirement.

### Tests added

`VinylRecordRteSanitizationTests` (5 tests):
- `<script>` stripped; album description paragraph preserved
- `<img onerror=>` stripped (`img` not on allowlist)
- `<svg onload=>` stripped; safe sibling `<p>` preserved
- Legitimate TinyMCE output (p, strong, em, ul, a) passes through intact
- null/empty/whitespace inputs return empty string safely

---

## Build & test summary

- `dotnet build UmbracoPrism.sln -c Release`: **clean (0 errors, pre-existing warnings only)**
- `dotnet test … --filter "FullyQualifiedName~UmbracoPrism.Core.Tests"`:  
  **627 passed, 0 failed** (baseline was 618; +9 new tests across both findings)

---

## Commits

| SHA | Finding | Subject |
|-----|---------|---------|
| `03dba49` | PT2-007 | sec(pt2-007): sanitize Accordion Content via IWorkflowContentSanitizer |
| `6177137` | PT2-008 | sec(pt2-008): sanitize VinylRecord RTE through IWorkflowContentSanitizer |
