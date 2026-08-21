# Bulk data review

How the NJF Contributions Team submits a monthly file of member contributions, gets back only the
rows that need attention, corrects them in place, and resubmits — Wayfinder's bulk data review
capability (see [`docs/guides/bulk-data-review.md`](https://github.com/jonnymuir/Wayfinder/blob/main/docs/guides/bulk-data-review.md)
in the core Wayfinder repo), backed by a real downstream support system (Mock Business App), all
composed from ordinary Prism content via Wayfinder.Umbraco's packaged Block Grid blocks.

## The story

The **National Juggling Federation (NJF)** submits its monthly member contributions to **Mock
Business App** — a genuinely separate downstream application, not something Wayfinder or Prism
hosts. Historically, "here's the file back, annotated with what's wrong" meant someone opening it
in a spreadsheet, scrolling for the flagged rows, fixing them by hand, and re-uploading the whole
thing. Bulk data review is Wayfinder's answer: a paginated review screen that shows only the rows
that need attention, lets you correct them in place, and resubmits a corrected version of the
*whole* file — because Mock Business App's own contract never changes, it always expects the
whole thing — without anyone touching a spreadsheet.

## Act 1 — submitting the file

An NJF Contributions Team member signs in and opens **Submit contributions file** from their
dashboard — a plain GOV.UK upload page, one file field.

This month's file has five members. Three are completely fine. One has a genuine data problem — a
membership tier ("Bogus") that doesn't exist. One has a contribution that's unusually high for its
tier — not wrong exactly, but worth a second look.

Submitting lands **directly on the wait screen** — the same join-gateway wait/poll mechanism the
citizen-facing journey's own support-system calls use. Navigating away (to the caseworker queue,
say) doesn't lose it: the submission stays there, tagged **Waiting**.

## Act 2 — only the rows that need attention

Mock Business App — a genuinely separate ASP.NET app, running on its own port, that knows nothing
about Wayfinder's internals — validates the file for real and sends back the same five rows, each
with a matched member ID and (for two of them) an error or warning.

The review screen shows a summary — 1 error, 1 warning, 3 accepted — then **only the rows that
need attention**, as cards, fetched by the browser itself after the page loads. There's no
**Accept and finish** button anywhere on this page: one row still has a genuine error, and the
blueprint's own declared rule (`contributionsErrorCount = 0`) means that route doesn't exist yet.

## Act 3 — correcting a row, without reloading the page

The caseworker fixes the tier on the flagged card directly — types "Recreational" over "Bogus".
There's no "Save" button: the correction **autosaves** shortly after typing stops, and the card's
own status line says so ("Pending resubmission").

Clicking **Resubmit corrected file** sends the *whole* file back to Mock Business App — built from
the corrected dataset, not the original upload. It's a genuine loop through the same two systems:
the same split gateway, the same wait screen, the same review stage, all over again.

## Act 4 — a warning still needs an explicit yes

Mock Business App genuinely re-validates the corrected file. The tier error is gone. The
out-of-band contribution warning is still there, and **Accept and finish** now appears — errors
block, warnings don't. But clicking it doesn't finish straight away: with a warning still on
record, that button leads to a **Confirm before finishing** screen first, with an explicit **"Yes,
accept with warnings"** — not a silent nod. A file with zero warnings never sees this screen at
all.

Confirming lands on a plain confirmation page: **Contributions file accepted**, with the warning
still on record, exactly as it should be.

## What's actually composing this

None of the above is hand-coded into a controller. The citizen/caseworker pages are `wayfinderServicePage`
content nodes — an ordinary CMS editor drops Wayfinder.Umbraco's packaged
`wayfinderServiceRequestStage`/`wayfinderServiceRequestWorklist` Block Grid blocks onto them, the
same way they'd compose any other page. The blueprint itself
([`bulk-contributions.json`](../../src/UmbracoPrism.TestSite/service-blueprints/bulk-contributions.json))
declares one queue (`njf-upload`, assign-to-initiator — a submission comes back to whoever started
it, all the way through review, correction, and resubmission, never a teammate), a
`bulk-dataset-materialize`/`bulk-dataset-ingest` action pair, and a
`support-system-call` to `mock-business-app-contributions` — the exact same declarative mechanism
every other Wayfinder blueprint uses, authored in the same visual editor.

Mock Business App's own validation rules
([`ContributionsValidation.cs`](../../src/UmbracoPrism.MockBusinessApp/Services/SupportSystem/ContributionsValidation.cs))
mirror the core Wayfinder repo's own `SafetyNetUnderwriting` reference implementation: duplicate/missing
member references, unrecognised tiers, a fire-endorsement contribution floor, under-18/date-of-birth
consistency, and a contribution-band warning.

## Try it yourself

```
dotnet run --project src/UmbracoPrism.AppHost
```

Sign in as `njf-caseworker@prism.local` / `password` (the NJF Contributions Team's only roster
member — see `NjfContributionsTeam`'s own remarks for why `demo@prism.local` deliberately isn't
one), and look for **Submit contributions file** on the dashboard. Upload
[`njf-contributions-sample.csv`](../../src/UmbracoPrism.Client/tests/walkthroughs/fixtures/njf-contributions-sample.csv)
— the exact file the executable spec uses, five rows, with Cara Delgado's bad tier and Dev Patel's
out-of-band contribution already in it. Any CSV with the header
`memberRef,memberName,tier,fireEndorsement,under18,dob,monthlyContribution` will do more broadly.

---

**Executable spec:** This walkthrough is executed on every PR by
[`bulk-data-review.walkthrough.spec.ts`](../../src/UmbracoPrism.Client/tests/walkthroughs/bulk-data-review.walkthrough.spec.ts).
Screenshots above regenerate via the [`Capture Walkthrough Screenshots`](../../.github/workflows/capture-screenshots.yml)
workflow (manual dispatch). See [`walkthroughs-as-executable-specs`](../../.claude/skills/walkthroughs-as-executable-specs/SKILL.md)
for the policy.
