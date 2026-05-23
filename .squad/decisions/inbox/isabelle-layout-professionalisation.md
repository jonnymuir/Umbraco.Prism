### 2026-05-23T08:30:10+01:00: Workflow editor tabbed layout redesign

**By:** Isabelle (Frontend Dev & Accessibility Lead)  
**Decision:** Restructured workflow editor to use tabbed layout with Canvas as primary tab

#### What Changed

**1. Tab Structure**
- Moved main editing workspace (outline + canvas + inspector) into a "Canvas" tab
- Canvas is now the default/primary tab alongside Validation, Preview, Simulation, and Help
- This gives the editing surface full vertical expansion instead of being constrained by fixed-height confidence panels

**2. Confidence Tabs Enhancement**
- Added `canvas` to `ConfidenceTab` type union in `prism-confidence-tabs.ts`
- Changed default active tab from `validation` to `canvas`
- Tab bar now reads: Canvas | Validation | Preview | Simulation | Help

**3. Layout Benefits**
- Editor workspace can expand vertically as much as needed
- No more 280px fixed-height constraint on the bottom panel
- Outline, graph, and inspector all get more space
- Follows user's suggestion: "have the editor as a tab itself"

#### Why

User feedback: "the actual editing surface itself is very small and not visible... I wonder whether we just add the editor to the interface and therefore have the screen can vertically expand as far as it needs to give space for the content. I.e. don't put the tabs under the editor, have the editor as a tab itself."

This change:
- Prioritizes the canvas as the primary surface
- Removes vertical space constraints from the editing workspace
- Makes confidence tools secondary/supportive rather than always-visible
- Gives authors more breathing room for complex workflows

#### Impact

- **Shell:** Already simplified (removed heavy hero section, guidance text, made API base direct)
- **Editor:** Canvas workspace now tab-based, gains full vertical height
- **CSS:** Removed fixed 280px height constraint on confidence panel
- **TypeScript:** Clean build (minor unused variable warnings in shell, non-blocking)
- **Accessibility:** Tablist ARIA structure maintained, keyboard navigation unchanged
- **Stories:** Build successful, all component imports resolve

#### Trade-offs

- Confidence tools (validation, preview, simulation) now require tab switch instead of always-visible
- However, validation badge on tab provides visual feedback for errors/warnings
- Canvas being default means authors land in the workspace first (expected behavior)

#### Next Steps

- Test keyboard navigation between tabs (Tab/Shift+Tab, arrow keys if implemented)
- Verify screen reader announces tab switches correctly
- Consider adding keyboard shortcut to jump to Validation tab (e.g., Ctrl+Shift+V)
