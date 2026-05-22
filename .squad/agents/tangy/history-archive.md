# Archive: Tangy — Earlier Issues #64–#68

## Issues #64–#68 Quality Gate Summary (2026-05-18)

### Issue #64: Copy/Paste keyboard shortcuts
- Seven-seam gate: authoring tests, client build, Storybook CI, graph keyboard, action editor, copy/paste contract, planning smoke
- Status: Acceptance-complete; all gates passed

### Issue #65: Workflow validation and error reporting
- Seven-seam gate: authoring tests, client build, Storybook CI, graph keyboard, action editor, validation contract, planning smoke
- Status: Acceptance-complete; validation rail surfaces errors, save blocking works, dedication contract passed
- Note: Unrelated retry-only flake in older action-editor keyboard/forms spec

### Issue #66: Help and shortcut discoverability
- Six-seam gate: client build, Storybook CI, graph keyboard, action editor, help/shortcuts contract, planning smoke
- Status: Acceptance-complete; toolbar help button visible, F1 opens shortcut reference, shared catalog drives all surfaces, keyboard/screen-reader paths work

### Issue #67: Stage preview editing
- Six-seam gate: authoring tests, client build, Storybook CI, graph keyboard, preview contract, planning smoke
- Status: Acceptance-complete; stage renders in preview panel, auto-updates on edits, surface selector works, read-only enforced, loading feedback present

### Issue #68: Simulate workflow path execution
- Seven-seam gate: authoring tests, client build, Storybook CI, graph keyboard, validation rail, simulation contract, planning smoke
- Status: Acceptance-complete; simulation panel owns state, graph highlights from host only, happy-path/rejection/blocker flows covered
- Note: Non-slice blocker identified (empty planning.workflow.json seed → API 500s; environment remediation, not feature gap)

## Key Learnings from Earlier Gates

### Pattern: Quality gate structure
Each slice owns seven seams (or variant count) including:
- Focused .NET authoring/engine tests
- Client build + Storybook CI across browsers with axe
- Keyboard contract (workflow-graph-keyboard.spec.ts)
- Slice-specific Playwright contract
- Live planning workflow smoke

### Pattern: Honest acceptance boundary
- Distinguish slice-specific gaps from surrounding environment noise
- Document expected seams that are currently missing
- Call out infrastructure vs. feature blockers explicitly
- Treat retry-only flakes as non-evidence unless they propagate

### Pattern: Shared surfaces reduce drift
- Shared shortcut catalog drives toolbar affordances, help modal, tests
- Shared validation pass drives rail, save state, jump links
- Shared simulation state in host owner prevents component-local drift
- Reusing authoring catalog in runtime registry avoids duplication

---

**Archive created:** 2026-05-18T20:48:37Z by Scribe  
**Reason:** History.md summarization (original file 17,780 bytes)
