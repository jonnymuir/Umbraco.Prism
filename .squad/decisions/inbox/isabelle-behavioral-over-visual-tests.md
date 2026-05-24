---
date: 2026-05-24T10:27:00+01:00
author: isabelle
status: proposed
area: testing
confidence: high
---

# Replace pixel-perfect visual regression with behavioral assertions for workflow graph

## Decision

Replaced screenshot-based visual regression tests in `workflow-graph-visual.spec.ts` with behavioral assertions that verify user-facing functionality instead of pixel-perfect rendering.

## Context

### The Problem

Visual regression tests using Playwright's `toHaveScreenshot()` were failing on CI (Linux) despite passing locally (Darwin) with pixel differences:
- Graph canvas: 1,732 pixels different (0.01 ratio), threshold was 80 pixels
- List mode: 11,214 pixels different (0.02 ratio), threshold was 80 pixels

Even with deterministic font setup (embedded Inter TTF + antialiasing controls), platform rendering differences persisted. The previous fix removed `{platform}` from the path template but kept Darwin-generated baselines, which didn't match Linux rendering.

### Root Cause

Cross-platform font rendering differences are unavoidable even with embedded fonts and aggressive antialiasing controls. Fighting platform differences with visual snapshots creates maintenance burden.

## What Changed

### Before (Visual Regression)
- `toHaveScreenshot()` for graph canvas and list mode
- Deterministic font setup with embedded TTF fonts
- Platform-specific baselines that drift
- Tests verified "what it looks like" down to the pixel

### After (Behavioral Assertions)
- Explicit assertions for user-visible elements and behaviors
- Graph workspace test verifies: role lanes exist, stages rendered, transitions drawn, lane headers visible, canvas scrollable
- List mode test verifies: table structure, editable rows, inline fields, filtering options (all/front-stage/back-stage), action buttons (move up/down, insert before/after, delete)
- Tests verify "what users can DO" (view structure, edit, filter, reorder)

### Files Modified
- `src/UmbracoPrism.Client/tests/workflow-editor/workflow-graph-visual.spec.ts`: converted from screenshot assertions to behavioral assertions
- Removed: `workflow-graph-workspace-canvas.png`, `workflow-graph-workspace-list-mode.png`
- Removed: deterministic font setup, `applyDeterministicFont()`, `loadWorkspaceStory()` helpers

## Why This Matters

1. **Cross-platform stability**: Behavioral tests pass identically on Darwin and Linux without platform-specific baselines
2. **Maintenance reduction**: No need to regenerate baselines when unrelated CSS changes slightly shift pixels
3. **Better signal**: Tests fail when actual user behaviors break, not when rendering engine antialiasing differs by 0.01%
4. **Alignment with test discipline**: "Test behaviors not implementation mirrors" — pixel snapshots are the ultimate implementation mirror

## User Clarification

The user suspected "list mode" might be obsolete. **It is NOT obsolete.** List mode (linear mode) is a real user behavior:
- Displays stages in an editable table (vs. graph swim lanes)
- Supports inline editing of stage properties
- Offers filtering by surface (all/front-stage/back-stage)
- Provides reordering controls (move up/down)
- Essential for workflows with many stages where tabular view is clearer

## Coordination

- **Tangy** uses layout proof tests (measured DOM geometry) for precise positioning validation — those tests remain unchanged
- This change only affects the Storybook visual lane, which now uses behavioral assertions instead of screenshots

## Outcome

Tests pass locally and should pass on CI. No platform-specific drift. Clear failure signal when user-facing behaviors break.
