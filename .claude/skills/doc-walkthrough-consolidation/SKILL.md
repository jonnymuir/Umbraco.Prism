---
name: "doc-walkthrough-consolidation"
description: "Consolidate duplicate walkthroughs into a single canonical location, preserving detailed narrative"
domain: "documentation"
confidence: "high"
source: "mabel-doc-review-042026"
---

## Context

When a detailed walkthrough exists in a top-level doc location alongside a brief reference in a dedicated `walkthroughs/` directory, consolidate into the walkthroughs folder as the canonical home.

## Problem Pattern

- `docs/workflow-walkthrough.md` (388 lines, extensive step-by-step narrative)
- `docs/walkthroughs/planning-notification.md` (60 lines, brief overview)

Both describe the same workflow but live in different locations, causing discoverability confusion and maintenance split.

## Solution Pattern

### 1. Identify the Consolidation Target
- **Walkthroughs folder** (`docs/walkthroughs/`) is the canonical home for workflow walkthroughs
- It contains a README.md index and sibling walkthroughs (e.g., community-enquiry, payment-demo, information-request)
- This folder structure groups related content together

### 2. Merge Content Intelligently
- Extract the detailed, step-by-step narrative from the top-level file
- Preserve technical explanations and behind-the-scenes details
- Integrate any schema examples or key takeaways from the brief version
- Ensure the merged version maintains narrative flow

### 3. Update All References
- Find all inbound links: `grep -rn "workflow-walkthrough\|docs/workflow-walkthrough" --include="*.md"`
- Update README.md references (check both main README and ASPIRE_DEV.md)
- Verify internal hyperlinks work post-consolidation

### 4. Delete the Top-Level File
- Remove the now-redundant top-level file (e.g., `docs/workflow-walkthrough.md`)
- Content is preserved in the walkthroughs/ version

### 5. Update Walkthrough Index
- Ensure `docs/walkthroughs/README.md` lists the consolidated walkthrough
- Verify links and descriptions are up-to-date

## Implementation Notes

- **Screenshot directories:** Each walkthrough may have `docs/images/walkthroughs/{workflow-name}/` subdirectory for visual assets
- **Relative links:** Use relative paths from the Markdown file location (e.g., `../images/walkthroughs/planning-notification/01-initial.png`)
- **Consistency:** Apply the same narrative structure across all consolidated walkthroughs for consistency

## Example

**Before:**
```
docs/
  workflow-walkthrough.md              ← detailed walkthrough (388 lines)
  walkthroughs/
    README.md                          ← index
    planning-notification.md           ← brief overview (60 lines)
    community-enquiry.md
    payment-demo.md
    information-request.md
```

**After:**
```
docs/
  walkthroughs/
    README.md                          ← index (lists consolidated walkthrough)
    planning-notification.md           ← merged, comprehensive (now ~600 lines)
    community-enquiry.md
    payment-demo.md
    information-request.md
```

**Link updates:**
```
# Before
→ [Full walkthrough: docs/workflow-walkthrough.md](docs/workflow-walkthrough.md)

# After
→ [Full walkthrough: docs/walkthroughs/planning-notification.md](docs/walkthroughs/planning-notification.md)
```

## Benefits

- **Single source of truth:** One canonical location per walkthrough
- **Better discoverability:** Walkthroughs folder groups all examples together
- **Easier maintenance:** One file to update, not two
- **Consistent structure:** All walkthroughs follow the same pattern and location
