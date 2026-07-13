# Skill: scope-reset audit

When a user issues a hard scope reset on an accumulated feature ("take it back to a simple design", "remove X", "lock the model"), do **audit + plan first, never code**. The audit is the deliverable.

## When to use

- User says some variant of "we've drifted, take it back to simple"
- A specific surface (component, modal, endpoint) is named for removal
- The minimal model is restated as plain rules ("X only happens through Y")

## Steps

1. **Pin the new rules verbatim.** Quote the directive into the decision draft so future agents can verify intent.
2. **Inventory the surface.** List every file in the target package and classify each as KEEP / DELETE / SIMPLIFY against the minimal model. Include backend + frontend + tests + stories + docs in the same pass — one of them is always the silent straggler.
3. **Confirm "should already be gone" claims.** Don't trust the directive's "we've already removed this" — grep production code, tests, walkthroughs, and design docs separately. Squad metadata (agent histories, orchestration logs, skills) is *not* a residue concern; design docs and walkthroughs are.
4. **Disambiguate doubled-purpose abstractions.** Some types (e.g. patch envelopes, event buses) carry both the UI narrative being cut and an unrelated infrastructure role. Call this out explicitly so the follow-up agent doesn't over-delete.
5. **Encode the new rule as a server-side validation.** A UI-only enforcement of a model rule will rot. Identify which validator owns the rule and what diagnostic codes already exist.
6. **Slice deletions first.** The smaller the surface before the model/visual edits, the safer those edits become. First slice = remove the named-cut features; later slices = lock model + visuals + tidy types.
7. **Use plain product language in slices.** Never name files in slice titles; describe the user-visible change. Implementation lives in the follow-up spawn.
8. **Flag risks and open questions** for the requester. Anywhere the mandate is ambiguous (e.g. "do we also remove the backend protocol that shared a name with the UI feature?"), ask, don't assume.

## Anti-patterns

- Writing code in the audit pass. The audit's value is that nothing has changed yet.
- Treating squad metadata as production residue and trying to scrub agent histories.
- Conflating a type's name with its responsibility — e.g. deleting `ProposalEnvelope` because the proposal-diff UI is going, when it is also the save protocol.
- Slicing by file ("Edit prism-workflow-editor.ts"). Slice by user-visible outcome.

## Deliverables

- One decision file in `.squad/decisions/inbox/` with: pinned directive, canonical model, validation rules, visual contract, sliced plan, supersedes list, open questions.
- History.md learnings entry that captures surprises (most useful when the next agent thinks they already understand the surface).
