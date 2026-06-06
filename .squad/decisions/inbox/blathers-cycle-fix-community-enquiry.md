# Decision: Join Gateway Pattern for Backward-Loop Workflows

**Author:** Blathers (Backend Dev)  
**Date:** 2026-06-06  
**Scope:** community-enquiry.json, information-request.json, prism-workflow-graph.ts

---

## Problem: Cycles break Kahn's longest-path algorithm

The "Get in Touch" (community-enquiry) and "Information Request" workflows both contain **backward-edge cycles**: routes that return from a later state or gateway back to the initial `collecting-details` / `collecting-info` state.

Specifically:
1. **Save-draft loop**: `collecting-details` → `route-save-draft` (Split) → `collecting-details`
2. **Request-changes loop**: `under-review` → `route-from-under-review` (Split) → `collecting-details`

The canvas layout algorithm uses **Kahn's longest-path algorithm** to assign vertical ranks (Y positions). This is a topological sort on the adjacency graph of stages and gateways. Topological sort **requires a Directed Acyclic Graph (DAG)**.

When a cycle exists, **no node in the cycle ever reaches in-degree 0**, so Kahn's algorithm never processes them. Every cyclic node stays at rank 0. All stages and gateways collapse to the same Y-coordinate, creating a horizontal sprawl instead of a top-to-bottom flow.

---

## Fix: Two-part solution

### Part 1 — JSON restructure: Insert a Join gateway on each backward loop

**Pattern:**
- Before: `earlier-state` ← `split-gateway` (routes backward)
- After:  `earlier-state` ← `join-return-to-form` (Join) ← `split-gateway`

**community-enquiry.json changes:**
- `route-save-draft` (Split) now routes to `join-return-to-form` instead of `collecting-details`
- `route-from-under-review` (Split) request-changes route now targets `join-return-to-form` instead of `collecting-details`
- New `join-return-to-form` (Join) gateway added with a single route back to `collecting-details`

**information-request.json:** identical structural fix (same pattern, same gateway names).

The gateway routing rules remain valid: state routes still target gateways; gateway routes still target states or other gateways.

### Part 2 — Layout algorithm fix: Remove backward edges from Join gateways before ranking

Even with the Join gateway in place, the adjacency graph still contains the cycle:
`collecting-details` → `route-save-draft` → `join-return-to-form` → `collecting-details`

The `join-return-to-form → collecting-details` route is added to the adjacency graph by the transitions loop (gateway outgoing routes), preventing `collecting-details` from being a DAG root.

**Fix in `prism-workflow-graph.ts`** (between the adjacency-build phase and Kahn's phase):

After all edges are added, iterate over every outgoing edge from a **Join gateway**. For each such edge, run a BFS from the target node to check whether the target can reach the Join gateway through the rest of the graph. If it can, the edge is a **backward edge** — remove it from the adjacency map and decrement `inDegree` for that target node.

This makes `collecting-details` (and equivalent initial states) have `inDegree = 0` again, restoring it as a DAG root. Kahn's algorithm then assigns correct ranks top-to-bottom.

The backward edge is still present in the `transitions` array and **rendered visually** as an upward-curving rail on the canvas, correctly conveying the "return to form" semantic to the author.

---

## Why Join gateways (not Split) for the merge point?

A Join gateway explicitly models a **merge / wait point**. Using it here:
- Signals to authors and tooling that this is where multiple backward paths converge before re-entering the form
- Is architecturally coherent: the Join holds the return-route semantic (runtime routes through it to `collecting-details`)
- Allows the layout algorithm's existing Join-gateway awareness to correctly classify and exclude the backward edge

Using a second Split gateway would not help — Split gateway anchor edges are always added to the adjacency graph, which recreates the cycle.

---

## Result

- `collecting-details` / `collecting-info` become DAG roots (in-degree 0)
- Stages rank correctly: initial form → submit/save gateway → review state → decision gateway → join/complete
- Canvas flows vertically top-to-bottom, matching the Payment Demo layout style
- Backward loop still renders as an upward rail in the canvas (correct visual semantics)
- All 809 backend tests pass; `dotnet build` clean
