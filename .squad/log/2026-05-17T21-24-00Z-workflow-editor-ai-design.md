# Session Log: Workflow editor AI design batch

**Date:** 2026-05-17T21:24:00Z  
**Spawned by:** User (Jonny Muir)  
**Directive:** Reuse existing AI tools like GitHub Copilot via MCP and skills so the workflow editor can participate in a conversational service-design workflow.  

## Summary

AI integration design batch evaluated by Tom Nook and Blathers. Established Copilot + MCP + skills architecture for conversational workflow/service design, preserving editor-first trust model and proposal-first review cycle.

## Decisions Made

1. **Copilot + MCP as conversational layer** (Tom Nook): Prefer this over bespoke AI stack; reuses strong general-purpose tools while keeping workflow intelligence in deterministic domain tools.
2. **Proposal-first Copilot integration** (Blathers): Thin MCP surface with `draft-proposal`, `validate`, `preview`, `diff`, `apply` verbs; human approval required before apply.
3. **User directive captured** (Jonny Muir): Motivation for reusing Copilot + MCP approach.

## North-Star Interaction Model

- Author asks in service-design language
- Copilot drafts structured proposal via workflow MCP tools
- Editor shows semantic diff, validation, and preview
- Author accepts, rejects, or partially applies changes
- Publish remains explicit editor-controlled step

## Build Order

1. Workflow-native editor surfaces and authored-model contract
2. Workflow MCP verbs
3. Copilot/skills integration
4. Richer history, replay, templates later

## Status

✅ Architecture and tool surface defined. Ready for implementation planning.
